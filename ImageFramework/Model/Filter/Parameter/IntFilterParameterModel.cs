﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageFramework.Model.Filter.Parameter
{
    public class IntFilterParameterModel : FilterParameterModel<int>, IFilterParameter
    {
        public class IntParameterAction : ParameterAction
        {
            public IntParameterAction(int value, ModificationType modType) : base(value, modType)
            {
            }

            public override int Invoke(int value)
            {
                switch (ModType)
                {
                    case ModificationType.Add:
                        return value + OpValue;
                    case ModificationType.Multiply:
                        return value * OpValue;
                    case ModificationType.Set:
                        return OpValue;
                }

                return value;
            }
        }

        public IntFilterParameterModel(string name, string variableName, int min, int max, int defaultValue)
            : base(name, variableName, min, max, defaultValue)
        {
            currentValue = defaultValue;

            // default actions
            Actions[ActionType.OnAdd] = new IntParameterAction(1, ModificationType.Add);
            Actions[ActionType.OnSubtract] = new IntParameterAction(-1, ModificationType.Add);
        }

        private int currentValue;
        public override int Value
        {
            get => currentValue;
            set
            {
                var clamped = Math.Min(Math.Max(value, Min), Max);
                if (currentValue == clamped) return;

                currentValue = clamped;
                OnPropertyChanged(nameof(Value));
            }
        }

        public virtual ParameterType GetParamterType()
        {
            return ParameterType.Int;
        }

        public string GetShaderParameterType()
        {
            return "int";
        }

        public FilterParameterModelBase GetBase()
        {
            return this;
        }

        public BoolFilterParameterModel GetBoolModel()
        {
            throw new InvalidCastException();
        }

        public IntFilterParameterModel GetIntModel()
        {
            return this;
        }

        public FloatFilterParameterModel GetFloatModel()
        {
            throw new InvalidCastException();
        }

        public int StuffToInt()
        {
            return Value;
        }

        public override string StringValue
        {
            get => Value.ToString();
            set => Value = int.Parse(value);
        }
    }
}
