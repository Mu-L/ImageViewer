﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using ImageFramework.DirectX;
using SharpDX.DXGI;

namespace ImageFramework.Utility
{
    internal class TextureCache : ITextureCache
    {
        private readonly Stack<ITexture> textures = new Stack<ITexture>(2);
        private readonly ITexture templateTex;
        private readonly Format format;
        private readonly bool createUav;

        public TextureCache(ITexture templateTexture, Format format = Format.R32G32B32A32_Float, bool createUav = true)
        {
            this.templateTex = templateTexture;
            this.createUav = createUav;
            this.format = format;
        }

        /// <summary>
        /// returns one unused texture if available. creates a new texture if not textures were available
        /// </summary>
        /// <returns></returns>
        public ITexture GetTexture()
        {
            if (textures.Count > 0)
            {
                var tex = textures.Pop();
                if (IsCompatibleWith(tex)) return tex; // all good

                textures.Push(tex);
                Clear(); // faulty textures in cache => clear (should not happen normally)
            }

            // make new texture with the current configuration
            return templateTex.Create(templateTex.LayerMipmap, templateTex.Size,
                format, createUav);
        }

        /// <summary>
        /// stores the textures for later use
        /// </summary>
        /// <param name="tex"></param>
        public void StoreTexture(ITexture tex)
        {
            Debug.Assert(IsCompatibleWith(tex));
            
            // can be used for later
            textures.Push(tex);
        }

        public bool IsCompatibleWith(ITexture tex)
        {
            Debug.Assert(tex != null);
            return templateTex.HasSameDimensions(tex);
        }

        /// <summary>
        /// disposes all textures
        /// </summary>
        private void Clear()
        {
            foreach (var tex in textures)
            {
                tex.Dispose();
            }
            textures.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
