﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageFramework.Model.Filter.Parameter
{
    public interface IFilterParameter
    {
        ParameterType GetParamterType();

        string GetShaderParameterType();

        FilterParameterModelBase GetBase();

        /// <summary>
        /// returns filter model is IsBool() throws an exception otherwise
        /// </summary>
        /// <returns></returns>
        BoolFilterParameterModel GetBoolModel();

        /// <summary>
        /// returns filter model is IsInt() throws an exception otherwise
        /// </summary>
        /// <returns></returns>
        IntFilterParameterModel GetIntModel();

        /// <summary>
        /// returns filter model is IsFloat() throws an exception otherwise
        /// </summary>
        /// <returns></returns>
        FloatFilterParameterModel GetFloatModel();

        /// <summary>
        /// stuffes the value into an integer variable (used for constant buffer parameter uploads)
        /// </summary>
        /// <returns></returns>
        int StuffToInt();
    }
}
