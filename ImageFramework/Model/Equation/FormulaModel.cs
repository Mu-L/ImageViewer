﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ImageFramework.Annotations;

namespace ImageFramework.Model.Equation
{
    public class FormulaModel : INotifyPropertyChanged
    {
        public FormulaModel(int defaultId)
        {
            Debug.Assert(defaultId >= 0);

            this.formula = $"I{defaultId}";
            this.Converted = ConvertFormula(this.formula);
        }

        // the formula which is displayed
        private string formula;

        public string Formula
        {
            get => formula;
            set
            {
                if(value == null || formula.Equals(value)) return;

                var oldFirst = FirstImageId;
                var oldMax = MaxImageId;
                var oldMin = MinImageId;
                var imgLast = HasImages;

                var converted = ConvertFormula(value);
                var changed = !converted.Equals(Converted);

                // does it result in the same conversion?
                Converted = converted;
                formula = value;
                if(changed)
                    OnPropertyChanged(nameof(Converted));
                if(oldFirst != FirstImageId)
                    OnPropertyChanged(nameof(FirstImageId));
                if(oldMax != MaxImageId)
                    OnPropertyChanged(nameof(MaxImageId));
                if(oldMin != MinImageId)
                    OnPropertyChanged(nameof(MinImageId));
                if(imgLast != HasImages)
                    OnPropertyChanged(nameof(HasImages));

                OnPropertyChanged(nameof(Formula));
            }
        }

        // the id of the first image that was used in the equation
        public int FirstImageId { get; private set; }

        // the highest image id that was used in the equation
        public int MaxImageId { get; private set; }

        // the lowest id that was used in this equation
        public int MinImageId { get; private set; }

        // indicates if any images were used in the formula
        public bool HasImages { get; private set; }

        // the converted formula
        public string Converted { get; private set; }

        public struct TestResults
        {
            /// if null, test is valid
            public string Error;
            /// max image id
            public int MaxId;
            // min image id
            public int MinId;
            // indicates if images were used in the formula
            public bool HasImages;
            // id of the first used image
            public int FirstId;
        }

        /// <summary>
        /// tests if the given formula is valid
        /// </summary>
        /// <param name="f">formula to test</param>
        /// <returns>null if valid, error string if invalid</returns>
        public TestResults TestFormula(string f)
        {
            try
            {
                var eq = new HlslEquation(f);
                eq.GetHlslExpression();

                return new TestResults
                {
                    MaxId = eq.MaxImageId,
                    FirstId = eq.FirstImageId,
                    MinId = eq.MinImageId,
                    HasImages = eq.HasImageId
                };
            }
            catch (Exception e)
            {
                return new TestResults
                {
                    Error = e.Message
                };
            }
        }

        /// <summary>
        /// convertes the formula into an hlsl expression.
        /// </summary>
        /// <param name="f">image formula</param>
        /// <returns>hlsl expression</returns>
        /// <exception cref="Exception">on conversion failure</exception>
        private string ConvertFormula(string f)
        {
            var eq = new HlslEquation(f);
            var res = eq.GetHlslExpression();
            FirstImageId = eq.FirstImageId;
            MaxImageId = eq.MaxImageId;
            MinImageId = eq.MinImageId;
            HasImages = eq.HasImageId;
            return res;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
