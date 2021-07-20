﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using ImageFramework.Annotations;
using ImageFramework.Model;
using ImageViewer.Commands;
using ImageViewer.Commands.Export;
using ImageViewer.Commands.Import;
using ImageViewer.Commands.Tools;
using ImageViewer.Commands.View;
using ImageViewer.Models;
using ImageViewer.ViewModels.Display;
using ImageViewer.ViewModels.Image;
using ImageViewer.ViewModels.Statistics;
using ImageViewer.ViewModels.Tools;

namespace ImageViewer.ViewModels
{
    public class ViewModels : INotifyPropertyChanged, IDisposable
    {
        private readonly ModelsEx models;

        public DisplayViewModel Display { get; }
        public ProgressViewModel Progress { get; }

        public ImagesViewModel Images { get; }
        public FiltersViewModel Filter { get; }

        public EquationsViewModel Equations { get; }

        public StatisticsViewModel Statistics { get; }

        public ScalingViewModel Scale { get; }

        public ZoomBoxViewModel ZoomBox { get; }

        public ArrowsViewModel Arrows { get; }

        private int selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => selectedTabIndex;
            set
            {
                if(value == selectedTabIndex) return;
                selectedTabIndex = value;
                OnPropertyChanged(nameof(SelectedTabIndex));
            }
        }

        public ViewModels(ModelsEx models)
        {
            this.models = models;

            // view models
            Display = new DisplayViewModel(models);
            Progress = new ProgressViewModel(models);
            Images = new ImagesViewModel(models);
            Equations = new EquationsViewModel(models);
            Filter = new FiltersViewModel(models);
            Statistics = new StatisticsViewModel(models);
            Scale = new ScalingViewModel(models);
            ZoomBox = new ZoomBoxViewModel(models);
            Arrows = new ArrowsViewModel(models);

            // commands
            OpenCommand = new OpenCommand(models);
            ImportCommand = new ImportCommand(models);
            ImportEquationImageCommand = new ImportEquationImageCommand(models);
            ExportCommand = new ExportCommand(models);
            ExportOverwriteCommand = new ExportOverwriteCommand(models);
            ReloadImagesCommand = new ReloadImagesCommand(models);
            ReplaceEquationImageCommand = new ReplaceEquationImageCommand(models);
            ExportConfigCommand = new ExportConfigCommand(models);
            ImportConfigCommand = new ImportConfigCommand(models);

            ShowPixelDisplayCommand = new ShowPixelDisplayCommand(models);
            ShowPixelColorCommand = new ShowPixelColorCommand(models);
            ShowScaleCommand = new ShowScaleCommand(models);
            ShowPaddingCommand = new ShowPaddingCommand(models);
            GenerateMipmapsCommand = new GenerateMipmapsCommand(models);
            DeleteMipmapsCommand = new DeleteMipmapsCommand(models);
            HelpCommand = new HelpDialogCommand(models);
            GifExportCommand = new GifExportCommand(models);
            ImportArrayCommand = new ImportArrayCommand(models);
            LatLongToCubemapCommand = new LatLongToCubemapCommand(models);
            CubemapToLatLongCommand = new CubemapToLatLongCommand(models);
            ArrayTo3DCommand = new ArrayTo3DCommand(models);
            Tex3DToArrayCommand = new Tex3DToArrayCommand(models);
            SelectNaNColorCommand = new SelectNaNColorCommand(models);

            ResizeCommand = new ResizeWindowCommand(models);
            SetThemeCommand = new SetThemeCommand(models);
            StartZoomboxCommand = new StartZoomboxCommand(models);
            RemoveZoomboxCommand = new RemoveZoomBoxCommand(models);
            StartArrowCommand = new StartArrowCommand(models);
            RemoveArrowCommand = new RemoveArrowCommand(models);

            AddFilterCommand = new AddFilterCommand(models, Filter);

            // key input
            models.Window.Window.KeyDown += WindowOnKeyDown;
            models.Images.PropertyChanged += ImagesOnPropertyChanged;
        }

        private void ImagesOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ImagesModel.NumImages):
                    SelectedTabIndex = 0; // set view to images tab
                    break;
            }
        }

        private void WindowOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            // dont steal text from textboxes (they don't set handled to true...)
            if (e.OriginalSource is TextBox) return;

            if (Display.HasPriorityKeyInvoked(e.Key))
                return;

            if (Filter.HasKeyToInvoke(e.Key))
            {
                // invoke the key
                Filter.InvokeKey(e.Key);

                if (Filter.ApplyCommand.CanExecute(null))
                    Filter.ApplyCommand.Execute(null);

                return;
            }

            if(Display.HasKeyToInvoke(e.Key))
                Display.InvokeKey(e.Key);
        }

        public void Dispose()
        {
            models?.Dispose();
        }

        public ICommand ResizeCommand { get; }

        public ICommand SetThemeCommand { get; }

        public ICommand OpenCommand { get; }

        public ICommand ImportCommand { get; }

        public ICommand ImportEquationImageCommand { get; }

        public ICommand ExportCommand { get; }
        public ICommand ExportOverwriteCommand { get; }
        public ICommand ShowPixelDisplayCommand { get; }

        public ICommand ShowPixelColorCommand { get; }

        public ICommand ShowScaleCommand { get; }
        public ICommand ShowPaddingCommand { get; }

        public ICommand GenerateMipmapsCommand { get; }
        public ICommand DeleteMipmapsCommand { get; }

        public ICommand HelpCommand { get; }

        public ICommand AddFilterCommand { get; }

        public ICommand GifExportCommand { get; }

        public ICommand ImportArrayCommand { get; }

        public ICommand LatLongToCubemapCommand { get; }
        public ICommand CubemapToLatLongCommand { get; }

        public ICommand SelectNaNColorCommand { get; }

        public ICommand ReloadImagesCommand { get; }

        public ICommand ReplaceEquationImageCommand { get; }

        public ICommand StartZoomboxCommand { get; }

        public ICommand RemoveZoomboxCommand { get; }
        
        public ICommand StartArrowCommand { get; }

        public ICommand RemoveArrowCommand { get; }

        public ICommand ArrayTo3DCommand { get; }

        public ICommand Tex3DToArrayCommand { get; }

        public ICommand ExportConfigCommand { get; }

        public ICommand ImportConfigCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
