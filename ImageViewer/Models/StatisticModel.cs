﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ImageFramework.Annotations;
using ImageFramework.Model;
using ImageFramework.Model.Shader;
using ImageFramework.Model.Statistics;
using ImageFramework.Utility;
using ImageViewer.Models.Display;

namespace ImageViewer.Models
{
    public class StatisticModel : INotifyPropertyChanged
    {
        private readonly ImageFramework.Model.Models models;
        private readonly DisplayModel display;
        private readonly ImagePipeline pipe;

        public StatisticModel(ImageFramework.Model.Models models, DisplayModel display, int index)
        {
            this.models = models;
            this.display = display;
            pipe = this.models.Pipelines[index];
            pipe.PropertyChanged += PipeOnPropertyChanged;
            display.PropertyChanged += DisplayOnPropertyChanged;
        }

        private void DisplayOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DisplayModel.VisibleLayerMipmap):
                    UpdateStatistics();
                    break;
            }
        }

        private void PipeOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ImagePipeline.Image):
                    cache.Clear(); // image colors have changed => invalidate cache
                    UpdateStatistics();
                    break;
            }
        }

        private void UpdateStatistics()
        {
            if (pipe.Image != null)
            {
                // try to obtain from cache
                if(cache.TryGetValue(display.VisibleLayerMipmap, out var stats))
                    Stats = stats;
                else
                {
                    // calculate once and put into cache
                    Stats = models.Stats.GetStatisticsFor(pipe.Image, display.VisibleLayerMipmap);
                    cache.Add(display.VisibleLayerMipmap, Stats);
                }
                
                OnPropertyChanged(nameof(Stats));
            }
            else
            {
                Stats = DefaultStatistics.Zero;
                OnPropertyChanged(nameof(Stats));
            }
        }

        private readonly Dictionary<LayerMipmapRange, DefaultStatistics> cache = new Dictionary<LayerMipmapRange, DefaultStatistics>();
        public DefaultStatistics Stats { get; private set; } = DefaultStatistics.Zero;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
