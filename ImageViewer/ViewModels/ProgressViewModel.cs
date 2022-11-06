﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ImageFramework.Annotations;
using ImageFramework.Model;
using ImageFramework.Model.Progress;
using ImageViewer.Commands;
using ImageViewer.Commands.Helper;
using ImageViewer.Models;

namespace ImageViewer.ViewModels
{
    public class ProgressViewModel : INotifyPropertyChanged
    {
        private readonly ModelsEx models;

        public ProgressViewModel(ModelsEx models)
        {
            this.models = models;
            this.models.Progress.PropertyChanged += ProgressOnPropertyChanged;
            CancelCommand = new ActionCommand(() => models.Progress.CancelAsync());
        }

        private void ProgressOnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(ProgressModel.IsProcessing):
                    OnPropertyChanged(nameof(EnableProgress));
                    OnPropertyChanged(nameof(NotProcessing));
                    OnPropertyChanged(nameof(ProgressIndeterminate));
                    break;
                case nameof(ProgressModel.Progress):
                    OnPropertyChanged(nameof(ProgressValue));
                    OnPropertyChanged(nameof(ProgressIndeterminate));
                    break;
                case nameof(ProgressModel.What):
                    OnPropertyChanged(nameof(ProgressDescription));
                    break;
                case nameof(ProgressModel.LastError):
                    // TODO log
                    //if(!String.IsNullOrEmpty(models.Progress.LastError))
                    //    models.Window.ShowErrorDialog(models.Progress.LastError, "Task failed");
                    break;
            }
        }

        public Visibility EnableProgress => models.Progress.IsProcessing ? Visibility.Visible : Visibility.Collapsed;
        public bool NotProcessing => !models.Progress.IsProcessing;// && !models.Export.IsExporting;

        public float ProgressValue
        {
            get => models.Progress.Progress * 100.0f;
            // dont allow changes from the ui
            set => OnPropertyChanged(nameof(ProgressValue));
        }

        public bool ProgressIndeterminate => models.Progress.Progress == 0.0f;

        public string ProgressDescription => models.Progress.What;

        public ICommand CancelCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
