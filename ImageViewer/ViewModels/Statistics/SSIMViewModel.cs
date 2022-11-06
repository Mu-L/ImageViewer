﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ImageFramework.Annotations;
using ImageFramework.DirectX;
using ImageFramework.ImageLoader;
using ImageFramework.Model.Statistics;
using ImageFramework.Utility;
using ImageViewer.Models;
using ImageViewer.Models.Display;
using Microsoft.SqlServer.Server;
using Format = SharpDX.DXGI.Format;

namespace ImageViewer.ViewModels.Statistics
{
    public class SSIMViewModel : INotifyPropertyChanged
    {
        private readonly ModelsEx models;
        private readonly SSIMsViewModel parent;
        private readonly StatisticsViewModel statisticsViewModel;

        public SSIMViewModel(SSIMsViewModel parent, StatisticsViewModel statViewModel, ModelsEx models, int id)
        {
            this.parent = parent;
            this.statisticsViewModel = statViewModel;
            this.models = models;
            this.models.Display.PropertyChanged += DisplayOnPropertyChanged;
            statViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            this.id = id;
        }

        // tab should be visible and ssim should be enabled
        public bool UpdateSSIMValues => statisticsViewModel.IsVisible && statisticsViewModel.ShowSSIM;

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(StatisticsViewModel.IsVisible):
                case nameof(StatisticsViewModel.ShowSSIM):
                    // test if tab is visible and selected channel is correct
                    RecalculateSSIM(false);
                    break;
            }
        }

        private void DisplayOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DisplayModel.VisibleLayerMipmap):
                    RecalculateSSIM(false);
                    break;
            }
        }

        private int id;

        public int Id
        {
            get => id;
            set
            {
                if(id == value) return;
                id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        // text box properties
        public string Luminance { get; set; }
        public string Contrast { get; set; }
        public string Structure { get; set; }
        public string SSIM { get; set; }
        public string DSSIM { get; set; }

        private bool isValid = false;

        public bool IsValid
        {
            get => isValid;
            set
            {
                if(value == isValid) return;
                isValid = value;
                OnPropertyChanged(nameof(IsValid));
            }
        }

        private SSIMsViewModel.ImageSourceItem image1 = null;
        public SSIMsViewModel.ImageSourceItem Image1
        {
            get => image1;
            set
            {
                if (image1 == value) return;
                image1 = value;
                OnPropertyChanged(nameof(Image1));
                RecalculateSSIM(true);
            }
        }

        private SSIMsViewModel.ImageSourceItem image2 = null;
        public SSIMsViewModel.ImageSourceItem Image2
        {
            get => image2;
            set
            {
                if (image2 == value) return;
                image2 = value;
                OnPropertyChanged(nameof(Image2));
                RecalculateSSIM(true);
            }
        }

        public void UpdateImageSources()
        {
            // find matching source
            image1 = FindMatchingItem(Image1);
            image2 = FindMatchingItem(Image2);
            OnPropertyChanged(nameof(Image1));
            OnPropertyChanged(nameof(Image2));
            RecalculateSSIM(true);
        }

        private SSIMsViewModel.ImageSourceItem FindMatchingItem(SSIMsViewModel.ImageSourceItem src)
        {
            if (src == null) return null;

            foreach (var item in parent.ImageSources)
            {
                if (item.IsEquation == src.IsEquation
                    && item.Id == src.Id)
                    return item;
            }

            return null;
        }

        private readonly Dictionary<LayerMipmapRange, SSIMModel.Stats> cache = new Dictionary<LayerMipmapRange, SSIMModel.Stats>();

        private void RecalculateSSIM(bool invalidateCache)
        {
            if(invalidateCache)
                cache.Clear();

            if (!IsValidImage(image1) || !IsValidImage(image2))
            {
                // reset
                IsValid = false;
                return;
            }

            // ignore updates if not visible
            if (!UpdateSSIMValues) return;

            // try to obtain cached entry
            var lm = models.Display.VisibleLayerMipmap;
            if (!cache.TryGetValue(lm, out var stats))
            {
                // calculate stats and insert into cache
                var i1 = GetImage(image1);
                var i2 = GetImage(image2);

                Debug.Assert(i1 != null);
                Debug.Assert(i2 != null);

                stats = models.SSIM.GetStats(i1, i2, lm, parent.Settings);
                cache.Add(lm, stats);
            }

            Luminance = stats.Luminance.ToString(ImageFramework.Model.Models.Culture);
            Structure = stats.Structure.ToString(ImageFramework.Model.Models.Culture);
            Contrast = stats.Contrast.ToString(ImageFramework.Model.Models.Culture);
            SSIM = stats.SSIM.ToString(ImageFramework.Model.Models.Culture);
            DSSIM = stats.DSSIM.ToString(ImageFramework.Model.Models.Culture);

            IsValid = true;
            OnPropertyChanged(nameof(Luminance));
            OnPropertyChanged(nameof(Structure));
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(SSIM));
            OnPropertyChanged(nameof(DSSIM));
        }

        private bool IsValidImage(SSIMsViewModel.ImageSourceItem image)
        {
            if (image == null) return false;

            if (!image.IsEquation)
            {
                if (image.Id < models.Images.NumImages) return true;
                return false;
            }
            
            // test if equation valid
            if (!models.Pipelines[image.Id].IsValid) return false;
            if(models.Pipelines[image.Id].Image == null) return false;
            return true;
        }

        private ITexture GetImage(SSIMsViewModel.ImageSourceItem image)
        {
            if (image.IsEquation) return models.Pipelines[image.Id].Image;
            return models.Images.Images[image.Id].Image;
        }

        public void ImportLuminance()
        {
            Debug.Assert(IsValid);
            var i1 = GetImage(image1);
            var i2 = GetImage(image2);
            var tex = models.Images.CreateEmptyTexture(Format.R32G32B32A32_Float, true);
            models.SSIM.GetLuminanceTexture(i1, i2, tex, parent.Settings);
            models.Images.AddImage(tex, false, null, GliFormat.RGBA32_SFLOAT);
        }

        public void ImportContrast()
        {
            Debug.Assert(IsValid);
            var i1 = GetImage(image1);
            var i2 = GetImage(image2);
            var tex = models.Images.CreateEmptyTexture(Format.R32G32B32A32_Float, true);
            models.SSIM.GetContrastTexture(i1, i2, tex, parent.Settings);
            models.Images.AddImage(tex, false, null, GliFormat.RGBA32_SFLOAT);
        }

        public void ImportStructure()
        {
            Debug.Assert(IsValid);
            var i1 = GetImage(image1);
            var i2 = GetImage(image2);
            var tex = models.Images.CreateEmptyTexture(Format.R32G32B32A32_Float, true);
            models.SSIM.GetStructureTexture(i1, i2, tex, parent.Settings);
            models.Images.AddImage(tex, false, null, GliFormat.RGBA32_SFLOAT);
        }

        public void ImportSSIM()
        {
            Debug.Assert(IsValid);
            var i1 = GetImage(image1);
            var i2 = GetImage(image2);
            var tex = models.Images.CreateEmptyTexture(Format.R32G32B32A32_Float, true);
            models.SSIM.GetSSIMTexture(i1, i2, tex, parent.Settings);
            models.Images.AddImage(tex, false, null, GliFormat.RGBA32_SFLOAT);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void OnPipelineImageChanged(int pipeId)
        {
            if (image1 == null || image2 == null) return;
            bool recompute = (image1.IsEquation && image1.Id == pipeId) || (image2.IsEquation && image2.Id == pipeId);
            if (!recompute) return;

            RecalculateSSIM(true);
        }

        public void OnSettingsChanged()
        {
            if (image1 == null || image2 == null) return;
            RecalculateSSIM(true);
        }
    }
}
