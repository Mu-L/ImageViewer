﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using ImageFramework.DirectX;
using ImageFramework.DirectX.Query;
using ImageFramework.DirectX.Structs;
using ImageFramework.Model;
using ImageFramework.Model.Equation;
using ImageFramework.Model.Filter;
using ImageFramework.Model.Progress;
using ImageFramework.Model.Scaling;
using ImageFramework.Utility;
using SharpDX.Direct3D11;

namespace ImageFramework.Controller
{
    /// <summary>
    /// controller that assures that the pipeline images are up to date
    /// </summary>
    internal class PipelineController
    {


        private readonly Models models;
        public PipelineController(Models models)
        {
            this.models = models;
            this.models.Images.PropertyChanged += ImagesOnPropertyChanged;

            foreach (var pipe in models.Pipelines)
            {
                pipe.PropertyChanged += (sender, e) => PipelineOnPropertyChanged(pipe, e);
                pipe.Color.PropertyChanged += (sender, e) => PipelineFormulaOnPropertyChanged(pipe, pipe.Color, e);
                pipe.Alpha.PropertyChanged += (sender, e) => PipelineFormulaOnPropertyChanged(pipe, pipe.Alpha, e);
            }

            this.models.Filter.PropertyChanged += FilterOnPropertyChanged;
            this.models.Filter.ParameterChanged += FilterOnParameterChanged;
            this.models.Scaling.PropertyChanged += ScalingOnPropertyChanged;
        }

        private void ScalingOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ScalingModel.Minify): 
                    // mipmap technique has changed => mipmaps need to be recomputed
                    if (models.Images.NumMipmaps > 1)
                    {
                        foreach (var pipeline in models.Pipelines)
                        {
                            if(pipeline.RecomputeMipmaps)
                                pipeline.HasChanges = true;
                        }
                    }
                    break;
            }
        }

        private void FilterOnParameterChanged(object sender, FiltersModel.ParameterChangeEventArgs args)
        {
            foreach (var pipeline in models.Pipelines)
            {
                if (pipeline.UseFilter)
                {
                    pipeline.HasChanges = true;
                }
            }
        }

        private void FilterOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(FiltersModel.Filter): // all equations with filter enabled must be recomputed
                    foreach (var pipe in models.Pipelines)
                    {
                        if (pipe.UseFilter)
                        {
                            pipe.HasChanges = true;
                        }
                    }
                    break;
            }
        }

        private void PipelineFormulaOnPropertyChanged(ImagePipeline pipe, FormulaModel formula, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(FormulaModel.Converted):
                    // verify if the new formula is still valid
                    UpdateFormulaValidity(pipe, models.Images.NumImages);
                    pipe.HasChanges = true;
                    break;
            }
        }

        public async Task UpdateImagesAsync(IProgress progress)
        {
            var args = new ImagePipeline.UpdateImageArgs
            {
                Models = models,
                Filters = null,
            };

            var numCompute = models.Pipelines.Count(pipe => pipe.HasChanges && pipe.IsValid && pipe.IsEnabled);

            try
            {
                var curCompute = 0;

                for (var i = 0; i < models.Pipelines.Count; i++)
                {
                    var pipe = models.Pipelines[i];
                    if (pipe.HasChanges && pipe.IsValid && pipe.IsEnabled)
                    {
                        args.Filters = pipe.UseFilter ? GetPipeFilters(i) : null;

                        await pipe.UpdateImageAsync(args, progress.CreateSubProgress((++curCompute) / (float)numCompute));
                    }
                }
            }
            catch
            {
                models.OnPipelineUpdate(this, false);
                throw;
            }
            models.OnPipelineUpdate(this, true);
        }

        /// <summary>
        /// returns all filters that are enabled for this pipline
        /// </summary>
        private List<FilterModel> GetPipeFilters(int index)
        {
            var res = new List<FilterModel>();
            foreach (var filter in models.Filter.Filter)
            {
                if(filter.IsEnabledFor(index))
                    res.Add(filter);
            }

            return res;
        }

        private void ImagesOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ImagesModel.NumImages):
                    bool wasDeletion = models.Images.PrevNumImages > models.Images.NumImages;
                    // update valid status
                    foreach (var pipe in models.Pipelines)
                    {
                        UpdateFormulaValidity(pipe, models.Images.NumImages);
                        if (wasDeletion)
                            pipe.HasChanges = true;
                    }
                    break;
                case nameof(ImagesModel.ImageOrder):
                case nameof(ImagesModel.NumMipmaps):
                case nameof(ImagesModel.NumLayers):
                case nameof(ImagesModel.Size):
                    // all images must be recomputed
                    foreach (var pipe in models.Pipelines)
                    {
                        pipe.HasChanges = true;
                    }
                    break;
            }
        }

        private void PipelineOnPropertyChanged(ImagePipeline sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ImagePipeline.HasChanges):
                    if (sender.HasChanges)
                    {
                        sender.ResetImage(models.TextureCache);
                    }
                    break;
                case nameof(ImagePipeline.IsValid):
                    if (sender.IsValid && sender.HasChanges)
                    {
                        // TODO shedule work?
                    }
                    break;
            }
        }

        private void UpdateFormulaValidity(ImagePipeline pipe, int numImages)
        {
            pipe.IsValid = pipe.Color.MaxImageId < numImages && pipe.Alpha.MaxImageId < numImages;
        }
    }
}
