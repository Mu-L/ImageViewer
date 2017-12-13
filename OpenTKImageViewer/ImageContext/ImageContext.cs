﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTKImageViewer.glhelper;
using OpenTKImageViewer.Tonemapping;
using OpenTKImageViewer.Utility;

namespace OpenTKImageViewer.ImageContext
{
    public delegate void ChangedLayerHandler(object sender, EventArgs e);

    public delegate void ChangedMipmapHanlder(object sender, EventArgs e);

    public delegate void ChangedImagesHandler(object sender, EventArgs e);

    public delegate void ChangedFilteringHandler(object sender, EventArgs e);

    public delegate void ChangedGrayscaleHandler(object sender, EventArgs e);

    public class ImageContext
    {
        #region Structures and Enums

        private class ImageData
        {
            public ImageLoader.Image image;
            public TextureArray2D TextureArray2D;

            public ImageData(ImageLoader.Image image)
            {
                this.image = image;
            }
        }

        public enum GrayscaleMode
        {
            Disabled,
            Red,
            Green,
            Blue,
            Alpha
        }

        #endregion

        #region Private Member

        private readonly List<ImageData> images = new List<ImageData>();
        private uint activeMipmap = 0;
        private uint activeLayer = 0;
        private bool linearInterpolation = false;
        private GrayscaleMode grayscale = GrayscaleMode.Disabled;
        private readonly ImageConfiguration[] finalTextures = new ImageConfiguration[2];
        private Sampler samplerLinear;
        private Sampler samplerLinearMip;
        private Sampler samplerNearest;
        private Sampler samplerNearestMip;

        #endregion

        #region Public Properties

        public enum SplitViewMode
        {
            Vertical,
            Horizontal
        }

        public SplitViewMode SplitView { get; set; } = SplitViewMode.Vertical;

        public bool LinearInterpolation
        {
            get { return linearInterpolation; }
            set
            {
                if (value == linearInterpolation) return;
                linearInterpolation = value;
                OnChangedFiltering();
            }
        }

        public GrayscaleMode Grayscale
        {
            get { return grayscale; }
            set
            {
                if (value == grayscale) return;
                grayscale = value;
                OnChangedGrayscale();
            }
        }

        public uint ActiveMipmap
        {
            get { return activeMipmap; }
            set
            {
                if (value != activeMipmap && value < GetNumMipmaps())
                {
                    activeMipmap = value;
                    OnChangedMipmap();
                }
            }
        }

        public uint ActiveLayer
        {
            get { return activeLayer; }
            set
            {
                if (value != activeLayer && value < GetNumLayers())
                {
                    activeLayer = value;
                    OnChangedLayer();
                }
            }
        }

        public TonemapperManager Tonemapper { get; } = new TonemapperManager();

        public TextureCache TextureCache { get; }
        #endregion

        #region Public Getter

        public ImageConfiguration GetImageConfiguration(int id)
        {
            return finalTextures[id];
        }

        public TextureArray2D GetImageTexture(int id)
        {
            return images[id].TextureArray2D;
        }

        public int GetNumImages()
        {
            return images.Count;
        }

        public int GetNumMipmaps()
        {
            if (images.Count == 0)
                return 0;
            return images[0].image.GetNumMipmaps();
        }

        public int GetNumLayers()
        {
            if (images.Count == 0)
                return 0;
            return images[0].image.Layers.Count;
        }

        public int GetWidth(int mipmap)
        {
            Debug.Assert(images.Count != 0);
            return images[0].image.GetWidth(mipmap);
        }

        public int GetHeight(int mipmap)
        {
            Debug.Assert(images.Count != 0);
            return images[0].image.GetHeight(mipmap);
        }

        public string GetFilename(int image)
        {
            Debug.Assert((uint)(image) < images.Count);
            return images[image].image.Filename;
        }

        /// <summary>
        /// checks if all images are grayscale images
        /// </summary>
        /// <returns>true if all images are grayscale images</returns>
        public bool HasOnlyGrayscale()
        {
            return images.All(imageData => imageData.image.IsGrayscale());
        }

        /// <summary>
        /// checks if any image has alpha channel
        /// </summary>
        /// <returns>true if any image has alpha channel</returns>
        public bool HasAlpha()
        {
            return images.Any(imageData => imageData.image.HasAlpha());
        }

        /// <summary>
        /// checks if any image is hdr
        /// </summary>
        /// <returns>true if any image is hdr</returns>
        public bool HasHdr()
        {
            return images.Any(imageData => imageData.image.IsHdr());
        }

        /// <summary>
        /// returns a byte array with the requested texture data. (this will use the final image)
        /// </summary>
        /// <param name="imageId">which image equation (0 or 1)</param>
        /// <param name="level">mipmap level</param>
        /// <param name="layer">which face</param>
        /// <param name="format">desired format</param>
        /// <param name="type">desired pixel type</param>
        /// <param name="width">out: width of the image</param>
        /// <param name="height">out: height of the image</param>
        /// <returns></returns>
        public byte[] GetCurrentImageData(int imageId ,int level, int layer, PixelFormat format, PixelType type, out int width,
            out int height)
        {
            width = GetWidth(level);
            height = GetHeight(level);
            if (finalTextures[imageId] == null)
                return null;

            if (finalTextures[imageId].DisplayTexture == null)
                return null;

            return finalTextures[imageId].DisplayTexture.GetData(level, layer, format, type, out width, out height);
        }

        /// <summary>
        /// returns a float array with the rgba data from the image
        /// </summary>
        /// <param name="imageId">which image equation (0 or 1)</param>
        /// <param name="level">mip map level</param>
        /// <returns></returns>
        public float[] GetStatisticsImageFloatData(int imageId, int level)
        {
            if (finalTextures[imageId] == null)
                return null;

            return finalTextures[imageId].GetStatisticsTexture().GetFloatData(level);
        }

        /// <summary>
        /// binds the texture that should be used for pixel displaying (status bar).
        /// </summary>
        /// <param name="imageId"></param>
        /// <param name="slot"></param>
        /// <param name="layer"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool BindStatisticsTexture(int imageId, int slot, int layer, int level)
        {
            return finalTextures[imageId].BindStatisticsTexture(slot, layer, level);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// tries to add the image. Throws Exception if image could not be added
        /// </summary>
        /// <param name="image"></param>
        public void AddImage(ImageLoader.Image image)
        {
            if(IsImageProcessing())
                throw new Exception("Images cannot be added while an operation is running");

            // only add if layout is consistent
            if (images.Count > 0)
            {
                var i = images[0].image;
                if (image.Layers.Count != i.Layers.Count)
                    throw new Exception($"Inconsistent amount of layers. Expected {i.Layers.Count} got {image.Layers.Count}");

                if (image.GetNumMipmaps() != i.GetNumMipmaps())
                    throw new Exception($"Inconsistent amount of mipmaps. Expected {i.GetNumMipmaps()} got {image.GetNumMipmaps()}");

                // test mipmaps
                for (int level = 0; level < image.GetNumMipmaps(); ++level)
                {
                    if (image.GetWidth(level) != i.GetWidth(level) || image.GetHeight(level) != i.GetHeight(level))
                        throw new Exception($"Inconsistent mipmaps dimension. Expected {i.GetWidth(level)}x{i.GetHeight(level)}" +
                                            $" got {image.GetWidth(level)}x{image.GetHeight(level)}");
                }
            }

            images.Add(new ImageData(image));
            OnChangedImages();
            if(HasOnlyGrayscale())
                Grayscale = GrayscaleMode.Red;
        }

        /// <summary>
        /// disposes all opengl resources
        /// </summary>
        public void Dispose()
        {
            foreach (var img in images)
            {
                img.TextureArray2D.Dispose();
            }
            foreach (var imageConfiguration in finalTextures)
            {
                imageConfiguration.Dispose();
            }
            Tonemapper.Dispose();
            TextureCache.Clear();
            samplerLinear?.Dispose();
            samplerLinearMip?.Dispose();
            samplerNearest?.Dispose();
            samplerNearestMip?.Dispose();
        }

        /// <summary>
        /// returns number of visible final textures (selected in the image dialog)
        /// </summary>
        /// <returns>[0,2]</returns>
        public int GetNumActiveImages()
        {
            return finalTextures.Count(imageConfiguration => imageConfiguration.Active);
        }

        /// <summary>
        /// bind the final texture
        /// </summary>
        /// <param name="imageId">id of the finals image (0 or 1)</param>
        /// <param name="slot">binding slot</param>
        public void BindFinalTextureAs2DSamplerArray(int imageId, int slot)
        {
            Debug.Assert(finalTextures[imageId].Active);
            if (finalTextures[imageId].DisplayTexture == null)
                return;
            finalTextures[imageId].DisplayTexture.Bind(slot);
            BindSampler(slot, finalTextures[imageId].DisplayTexture.HasMipmaps());
        }

        /// <summary>
        /// bind the final texture
        /// </summary>
        /// <param name="imageId">id of the finals image (0 or 1)</param>
        /// <param name="slot">binding slot</param>
        public void BindFinalTextureAsCubeMap(int imageId,  int slot)
        {
            Debug.Assert(finalTextures[imageId].Active);
            if (finalTextures[imageId].DisplayTexture == null)
                return;
            finalTextures[imageId].DisplayTexture.BindAsCubemap(slot);
            BindSampler(slot, finalTextures[imageId].DisplayTexture.HasMipmaps());
        }

        public void BindSampler(int unit, bool hasMipmaps)
        {
            BindSampler(unit, hasMipmaps, LinearInterpolation);
        }

        public void BindSampler(int unit, bool hasMipmaps, bool linear)
        {
            if(hasMipmaps)
            {
                if (linear)
                    samplerLinearMip.Bind(unit);
                else
                    samplerNearestMip.Bind(unit);
            }
            else
            {
                if (linear)
                    samplerLinear.Bind(unit);
                else
                    samplerNearest.Bind(unit);
            }

        }

        /// <summary>
        /// should be called before drawing the final image in order to update its contents if required
        /// </summary>
        /// <return>true if image is ready to be drawn, false if image has to be processed</return>
        public bool Update()
        {
            if (images.Count == 0)
                return true;

            // create gpu textures for newly added images
            foreach (var imageData in images)
            {
                if (imageData.TextureArray2D == null)
                    imageData.TextureArray2D = new TextureArray2D(imageData.image);
            }

            // create sampler object
            if(samplerLinear == null)
            {
                samplerLinear = new Sampler(TextureMinFilter.Linear, TextureMagFilter.Linear);
                samplerLinearMip = new Sampler(TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear);
                samplerNearest = new Sampler(TextureMinFilter.Nearest, TextureMagFilter.Nearest);
                samplerNearestMip = new Sampler(TextureMinFilter.NearestMipmapNearest, TextureMagFilter.Nearest);
            }

            foreach (var imageConfiguration in finalTextures)
            {
                if (!imageConfiguration.Update())
                    return false; // something has to be updated and cannot be drawn
            }

            return true;
        }

        private IStepable FindStepable()
        {
            foreach (var imageConfiguration in finalTextures)
            {
                if (imageConfiguration.TonemappingStepable != null)
                    return imageConfiguration.TonemappingStepable;
            }
            return null;
        }

        /// <summary>
        /// finds the first final texture that is marked active
        /// </summary>
        /// <returns>id of the active texture or -1 if nothing is active</returns>
        public int GetFirstActiveTexture()
        {
            for(int i = 0; i < finalTextures.Length; ++i)
                if (finalTextures[i].Active)
                    return i;
            return -1;
        }

        public float GetImageProcess()
        {
            // find first stepable
            var tonemappingStepable = FindStepable();
            if (tonemappingStepable == null)
                return 1.0f;
            return (float)tonemappingStepable.CurrentStep();
        }

        public string GetImageLoadingDescription()
        {
            var tonemappingStepable = FindStepable();
            if (tonemappingStepable == null)
                return "";
            return tonemappingStepable.GetDescription();
        }

        public bool IsImageProcessing()
        {
            var tonemappingStepable = FindStepable();
            return tonemappingStepable != null;
        }

        public void AbortImageProcessing()
        {
            if (!IsImageProcessing()) return;
            
            foreach (var imageConfiguration in finalTextures)
            {
                imageConfiguration.AbortImageCalculation();
            }
        }

        #endregion

        #region Events

        public event ChangedLayerHandler ChangedLayer;
        public event ChangedMipmapHanlder ChangedMipmap;
        public event ChangedImagesHandler ChangedImages;
        public event ChangedFilteringHandler ChangedFiltering;
        public event ChangedGrayscaleHandler ChangedGrayscale;

        private void OnChangedLayer()
        {
            ChangedLayer?.Invoke(this, EventArgs.Empty);
        }

        private void OnChangedMipmap()
        {
            ChangedMipmap?.Invoke(this, EventArgs.Empty);
        }

        private void OnChangedImages()
        {
            ChangedImages?.Invoke(this, EventArgs.Empty);
        }

        private void OnChangedFiltering()
        {
            ChangedFiltering?.Invoke(this, EventArgs.Empty);
        }

        private void OnChangedGrayscale()
        {
            ChangedGrayscale?.Invoke(this, EventArgs.Empty);
        }

        #endregion


        public ImageContext(List<ImageLoader.Image> images)
        {
            TextureCache = new TextureCache(this);

            for (var i = 0; i < finalTextures.Length; ++i)
            {
                finalTextures[i] = new ImageConfiguration(this, i)
                // only first is active by default
                { Active = i == 0};
            }
                

            // on changed events
            Tonemapper.ChangedSettings += (sender, args) =>
            {
                foreach (var toneConfiguration in finalTextures)
                {
                    // only recompute if tonemappers would be used
                    if (toneConfiguration.UseTonemapper)
                        toneConfiguration.RecomputeImage = true;
                }
            };
            if (images != null)
            {
                foreach (var image in images)
                {
                    AddImage(image);
                }
            }
        }
    }
}
