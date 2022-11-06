﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImageFramework.ImageLoader
{
    internal static class Dll
    {
        public const string DllFilePath = @"DxImageLoader.dll";

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int image_open(string filename);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int image_allocate(uint format, int width, int height, int depth, int layer, int mipmap);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void image_release(int id);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void image_info(int id, out uint format, out uint originalFormat,
            out int nLayer, out int nMipmaps);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void image_info_mipmap(int id, int mipmap, out int width, out int height, out int depth);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr image_get_mipmap(int id, int layer, int mipmap, out ulong size);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool image_save(int id, string filename, string extension, uint format, int quality);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr get_export_formats(string extension, out int nFormats);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr get_error(out int length);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint ProgressDelegate([MarshalAs(UnmanagedType.R4)] float progress, [MarshalAs(UnmanagedType.LPStr)] string description);

        [DllImport(DllFilePath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void set_progress_callback([MarshalAs(UnmanagedType.FunctionPtr)] ProgressDelegate pDelegate);

        public static string GetError()
        {
            var ptr = get_error(out var length);
            return ptr.Equals(IntPtr.Zero) ? "" : Marshal.PtrToStringAnsi(ptr, length);
        }

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
    }
}
