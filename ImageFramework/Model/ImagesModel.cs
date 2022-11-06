﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using ImageFramework.Annotations;
using ImageFramework.DirectX;
using ImageFramework.ImageLoader;
using ImageFramework.Model.Scaling;
using ImageFramework.Model.Shader;
using ImageFramework.Utility;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Texture3D = ImageFramework.DirectX.Texture3D;

namespace ImageFramework.Model
{
    public class ImagesModel : INotifyPropertyChanged, IDisposable
    {
        private MitchellNetravaliScaleShader scaleShader;

        private Size3[] dimensions = null;

        public class MipmapMismatch : Exception
        {
            public MipmapMismatch(string message) : base(message)
            { }
        }

        public class ImageData : IDisposable
        {
            public ITexture Image { get; private set; }
            public int NumMipmaps => Image.NumMipmaps;
            public int NumLayers => Image.NumLayers;
            public bool IsHdr => Image.Format == Format.R32G32B32A32_Float;
            public string Filename { get; }
            // name that will be displayed
            public string Alias { get; internal set; }
            public GliFormat OriginalFormat { get; private set; }

            public DateTime? LastModified { get; private set; }

            public bool IsFile => LastModified != null;

            /// <param name="image">valid texture</param>
            /// <param name="isFile">indicates if the texture was loaded from a file (should be false when the image was somehow converted, since this is being used to determine if "overwrite" is a possible operation)</param>
            /// <param name="filename">original filename or proposed filename (when not directly imported)</param>
            /// <param name="alias">display name</param>
            /// <param name="originalFormat">original internal format</param>
            internal ImageData(ITexture image, bool isFile, [CanBeNull] string filename, [CanBeNull] string alias, GliFormat originalFormat)
            {
                Image = image;
                Filename = filename ?? "";
                OriginalFormat = originalFormat;
                Alias = (alias ?? System.IO.Path.GetFileNameWithoutExtension(Filename)) ?? "";
                if (isFile && File.Exists(Filename))
                {
                    LastModified = File.GetLastWriteTime(Filename);
                }
            }

            internal void GenerateMipmaps(int levels, ScalingModel scaling)
            {
                var tmp = Image.CloneWithMipmaps(levels);
                scaling.WriteMipmaps(tmp);
                Image.Dispose();
                Image = tmp;
            }

            internal void DeleteMipmaps()
            {
                var tmp = Image.CloneWithoutMipmaps();
                Image.Dispose();
                Image = tmp;
            }

            public void Dispose()
            {
                Image.Dispose();
            }

            public void Scale(Size3 size, MitchellNetravaliScaleShader shader, ScalingModel scaling)
            {
                var tmp = shader.Run((TextureArray2D) Image, size, scaling);
                LastModified = null;
                Image.Dispose();
                Image = tmp;
            }

            public void Pad(Size3 leftPad, Size3 rightPad, PaddingShader.FillMode fill, Models models)
            {
                var tmp = models.SharedModel.Padding.Run(Image, leftPad, rightPad, fill, models.Scaling,
                    models.SharedModel);
                LastModified = null;
                Image.Dispose();
                Image = tmp;
            }

            public void Replace(ITexture image, GliFormat originalFormat)
            {
                if (ReferenceEquals(image, Image)) return;

                Image.Dispose();
                Image = image;
                OriginalFormat = originalFormat;
            }
        }

        private readonly List<ImageData> images = new List<ImageData>();
        public IReadOnlyList<ImageData> Images { get; }

        // this property change will be triggered if the image order changes (and not if the number of images changes)
        public static string ImageOrder = nameof(ImageOrder);
        // this property change will be triggered if an images changed its alias
        public static string ImageAlias = nameof(ImageAlias);

        public int NumImages => Images.Count;
        public int NumMipmaps => Images.Count == 0 ? 0 : Images[0].NumMipmaps;
        public int NumLayers => Images.Count == 0 ? 0 : Images[0].NumLayers;

        /// <summary>
        /// true if any image is hdr
        /// </summary>
        public bool IsHdr => Images.Any(imageData => imageData.IsHdr);

        public Type ImageType => Images.Count == 0 ? null : Images[0].Image.GetType();

        // helper for many other models
        /// <summary>
        /// previous number of images
        /// </summary>
        public int PrevNumImages { get; protected set; } = 0;

        public Size3 Size => Images.Count == 0 ? Size3.Zero : GetSize(0);
        public bool Is3D => Images.Count != 0 && images[0].Image.Is3D;

        /// size of the mipmap
        public Size3 GetSize(int mipmap)
        {
            Debug.Assert(Images.Count != 0);
            Debug.Assert(mipmap < NumMipmaps && mipmap >= 0);
            return dimensions[mipmap];
        }

        public int GetWidth(int mipmap)
        {
            Debug.Assert(Images.Count != 0);
            Debug.Assert(mipmap < NumMipmaps && mipmap >= 0);
            return dimensions[mipmap].Width;
        }

        public int GetHeight(int mipmap)
        {
            Debug.Assert(Images.Count != 0);
            Debug.Assert(mipmap < NumMipmaps && mipmap >= 0);
            return dimensions[mipmap].Height;
        }

        public int GetDepth(int mipmap)
        {
            Debug.Assert(Images.Count != 0);
            Debug.Assert(mipmap < NumMipmaps && mipmap >= 0);
            return dimensions[mipmap].Depth;
        }

        /// returns true if the dimension match with the internal images 
        public bool HasMatchingProperties(ITexture tex)
        {
            if (tex.NumLayers != NumLayers) return false;
            if (tex.NumMipmaps != NumMipmaps) return false;
            if (tex.Size != Size) return false;
            if (tex.GetType() != ImageType) return false;
            return true;
        }

        public ImagesModel(MitchellNetravaliScaleShader scaleShader)
        {
            this.scaleShader = scaleShader;
            Images = images;
        }

        /// <summary>
        /// tries to add the image to the current collection.
        /// </summary>
        /// <param name="image">valid texture</param>
        /// <param name="isFile">indicates if the texture was loaded from a file</param>
        /// <param name="filename">original filename or proposed filename (when not directly imported)</param>
        /// <param name="originalFormat">original internal format</param>
        /// <param name="alias">display name (if null this will be determined from the filename)</param>
        /// <exception cref="ImagesModel.MipmapMismatch">will be thrown if everything except the number of mipmaps matches</exception>
        /// <exception cref="Exception">will be thrown if the images does not match with the current set of images</exception>
        public void AddImage(ITexture image, bool isFile, [CanBeNull] string filename, GliFormat originalFormat, [CanBeNull] string alias = null)
        {
            if (Images.Count == 0) // first image
            {
                InitDimensions(image);
                images.Add(new ImageData(image, isFile, filename, alias, originalFormat));
                PrevNumImages = 0;
                OnPropertyChanged(nameof(NumImages));
                OnPropertyChanged(nameof(NumLayers));
                OnPropertyChanged(nameof(NumMipmaps));
                OnPropertyChanged(nameof(IsHdr));
                OnPropertyChanged(nameof(Size));
                OnPropertyChanged(nameof(ImageType));
            }
            else // test if compatible with previous images
            {
                TestCompability(image, (alias ?? filename) ?? "");

                // remember old properties
                var isHdr = IsHdr;

                images.Add(new ImageData(image, isFile, filename, alias, originalFormat));
                PrevNumImages = NumImages - 1;
                OnPropertyChanged(nameof(NumImages));

                if (isHdr != IsHdr)
                    OnPropertyChanged(nameof(IsHdr));
            }
        }

        /// <summary>
        /// replaces an existing image. (Name will be kept)
        /// </summary>
        /// <param name="idx">replace idx</param>
        /// <param name="image">new image</param>
        /// <param name="originalFormat">new format</param>
        public void ReplaceImage(int idx, ITexture image, GliFormat originalFormat)
        {
            Debug.Assert(idx < NumImages);
            TestCompability(image, images[idx].Filename);

            // remember old properties
            var isHdr = IsHdr;

            images[idx].Replace(image, originalFormat);
            OnPropertyChanged(nameof(ImageOrder));

            if (isHdr != IsHdr)
                OnPropertyChanged(nameof(IsHdr));
        }

        /// <summary>
        /// changed alias of the specified image
        /// </summary>
        public void RenameImage(int idx, string newAlias)
        {
            Debug.Assert(idx < NumImages);
            if (images[idx].Alias == newAlias) return;
            images[idx].Alias = newAlias;
            OnPropertyChanged(nameof(ImageAlias));
        }

        public void Clear()
        {
            PrevNumImages = NumImages;
            Dispose();
            images.Clear();

            // everything was resettet
            OnPropertyChanged(nameof(NumImages));
            OnPropertyChanged(nameof(NumLayers));
            OnPropertyChanged(nameof(NumMipmaps));
            OnPropertyChanged(nameof(Size));
            OnPropertyChanged(nameof(ImageType));
        }

        public void DeleteImage(int imageId)
        {
            Debug.Assert(imageId >= 0 && imageId < NumImages);

            // remember old properties
            var isHdr = IsHdr;

            // delete old data
            images[imageId].Dispose();
            images.RemoveAt(imageId);

            PrevNumImages = NumImages + 1;
            OnPropertyChanged(nameof(NumImages));

            if (isHdr != IsHdr)
                OnPropertyChanged(nameof(IsHdr));

            if (NumImages == 0)
            {
                // everything was resettet
                OnPropertyChanged(nameof(NumLayers));
                OnPropertyChanged(nameof(NumMipmaps));
                OnPropertyChanged(nameof(Size));
                OnPropertyChanged(nameof(ImageType));
            }
        }

        /// <summary>
        /// moves the image from index 1 to index 2
        /// </summary>
        /// <param name="idx1">current image index</param>
        /// <param name="idx2">index after moving the image</param>
        public void MoveImage(int idx1, int idx2)
        {
            Debug.Assert(idx1 >= 0);
            Debug.Assert(idx2 >= 0);
            Debug.Assert(idx1 < NumImages);
            Debug.Assert(idx2 < NumImages);
            if (idx1 == idx2) return;

            var i = images[idx1];
            images.RemoveAt(idx1);
            images.Insert(idx2, i);

            OnPropertyChanged(nameof(ImageOrder));
        }

        /// <summary>
        /// generates mipmaps for all images
        /// </summary>
        public void GenerateMipmaps(ScalingModel scaling)
        {
            Debug.Assert(NumMipmaps == 1);

            // compute new mipmap levels
            var levels = Size.MaxMipLevels;
            if (levels == NumMipmaps) return;

            foreach (var image in Images)
            {
                image.GenerateMipmaps(levels, scaling);
            }

            // recalc dimensions array
            InitDimensions(Images[0].Image);

            OnPropertyChanged(nameof(NumMipmaps));
        }

        public void DeleteMipmaps()
        {
            Debug.Assert(NumMipmaps > 1);
            foreach (var image in Images)
            {
                image.DeleteMipmaps();
            }
            // refresh dimensions array
            InitDimensions(Images[0].Image);

            OnPropertyChanged(nameof(NumMipmaps));
        }

        /// <summary>
        /// scales all images to the given dimensions
        /// </summary>
        public void ScaleImages(Size3 size, ScalingModel scaling)
        {
            if (NumImages == 0) return;
            if (Size == size) return;
            if (ImageType != typeof(TextureArray2D))
                throw new Exception("scaling is only supported for 2D images");
            
            var prevMipmaps = NumMipmaps;

            foreach (var imageData in Images)
            {
                imageData.Scale(size, scaleShader, scaling);
            }

            InitDimensions(images[0].Image);

            OnPropertyChanged(nameof(Size));
            if(prevMipmaps != NumMipmaps)
                OnPropertyChanged(nameof(NumMipmaps));
        }

        public void PadImages(Size3 leftPad, Size3 rightPad, PaddingShader.FillMode fill, Models models)
        {
            if (NumImages == 0) return;
            if (leftPad == Size3.Zero && rightPad == Size3.Zero) return;

            var prevMipmaps = NumMipmaps;

            foreach (var imageData in Images)
            {
                imageData.Pad(leftPad, rightPad, fill, models);
            }

            InitDimensions(images[0].Image);

            OnPropertyChanged(nameof(Size));
            if(prevMipmaps != NumMipmaps)
                OnPropertyChanged(nameof(NumMipmaps));
        }

        protected void InitDimensions(ITexture image)
        {
            dimensions = new Size3[image.NumMipmaps];
            for (var i = 0; i < image.NumMipmaps; ++i)
            {
                dimensions[i] = image.Size.GetMip(i);
            }
        }

        /// <summary>
        /// tests if the image properties match with the current image configuration
        /// </summary>
        private void TestCompability(ITexture image, string name)
        {
            Debug.Assert(NumImages > 0);
            if (image.GetType() != ImageType)
                throw new Exception($"{name}: Incompatible with internal texture types. Expected {ImageType.Name} but got {image.GetType().Name}");

            if (image.NumLayers != NumLayers)
                throw new Exception(
                    $"{name}: Inconsistent amount of layers. Expected {NumLayers} got {image.NumLayers}");

            if (image.Size != Size)
            {
                if (Size.Depth > 1)
                    throw new Exception(
                        $"{name}: Image resolution mismatch. Expected {Size.X}x{Size.Y}x{Size.Z} but got {image.Size.X}x{image.Size.Y}x{image.Size.Z}");

                throw new Exception(
                    $"{name}: Image resolution mismatch. Expected {Size.X}x{Size.Y} but got {image.Size.X}x{image.Size.Y}");
            }


            if (image.NumMipmaps != NumMipmaps)
                throw new ImagesModel.MipmapMismatch(
                    $"{name}: Inconsistent amount of mipmaps. Expected {NumMipmaps} got {image.NumMipmaps}");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            foreach (var imageData in Images)
            {
                imageData.Dispose();
            }
        }

        public ITexture CreateEmptyTexture(Format format, bool createUav, bool createRtv = true)
        {
            Debug.Assert(images.Count != 0);
            return images[0].Image.Create(new LayerMipmapCount(NumLayers, NumMipmaps), Size, format, createUav, createRtv);
        }

        /// <summary>
        /// returns image alias or empty string if id is invalid
        /// </summary>
        public string GetImageAlias(int id)
        {
            if (id < 0 || id >= NumImages) return "";
            return Images[id].Alias;
        }
    }
}
