﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageFramework.Annotations;
using ImageFramework.DirectX;
using ImageFramework.ImageLoader;
using ImageFramework.Model.Export;
using ImageFramework.Model.Progress;
using ImageFramework.Model.Shader;
using ImageFramework.Utility;
using Microsoft.SqlServer.Server;
using SharpDX.DirectWrite;
using Format = SharpDX.DXGI.Format;

namespace ImageFramework.Model
{
    /// <summary>
    /// Used to create an animated diff video of two images
    /// </summary>
    /// <remarks>Exports .mp4 but is called GifModel because it was originally supposed to export gif instead of mp4</remarks>
    public class GifModel : IDisposable
    {
        private readonly GifShader shader;
        private readonly ProgressModel progressModel;

        public class Config
        {
            public int FramesPerSecond = 30;
            public int NumSeconds = 6;
            public int SliderWidth = 3;
            public string TmpDirectory; // filename without extension (frames will be save in BaseFilename000 - BaseFilenameXXX)
            public string Filename; // destination filename
            public string Label1; // name of the first image
            public string Label2; // name of the second image
            public TextureArray2D Left; // left image
            public TextureArray2D Right; // right image
            [CanBeNull] public TextureArray2D Overlay; // optional overlay texture
            [CanBeNull] public List<Float2> RepeatRange; // optional (sorted) range of segments that should be repeated
            public int RepeatRangeCount = 2; // how often are the repeat ranges repeated
        }

        internal GifModel(ProgressModel progressModel)
        {
            this.progressModel = progressModel;
            shader = new GifShader();
        }

        public void CreateGif(Config cfg, SharedModel shared)
        {
            Debug.Assert(cfg.Left != null);
            Debug.Assert(cfg.Right != null);
            Debug.Assert(cfg.Left.Size == cfg.Right.Size);
            Debug.Assert(!progressModel.IsProcessing);
            if (cfg.Overlay != null)
            {
                Debug.Assert(cfg.Left.Size == cfg.Overlay.Size);
            }

            var cts = new CancellationTokenSource();

            progressModel.AddTask(CreateGifAsync(cfg, progressModel.GetProgressInterface(cts.Token), shared), cts, false);
        }

        private async Task CreateGifAsync(Config cfg, IProgress progress, SharedModel shared)
        {
            // delay in milliseconds
            var numFrames = cfg.FramesPerSecond * cfg.NumSeconds;
            var left = cfg.Left;
            var right = cfg.Right;
            var overlay = cfg.Overlay;

            // size compatible?
            bool disposeImages = false;
            if ((left.Size.Width % 2) != 0 || (left.Size.Height % 2) != 0)
            {
                disposeImages = true;
                var pad = Size3.Zero;
                pad.X = left.Size.Width % 2;
                pad.Y = left.Size.Height % 2;
                left = (TextureArray2D)shared.Padding.Run(left, Size3.Zero, pad, PaddingShader.FillMode.Clamp, null, shared, false);
                right = (TextureArray2D)shared.Padding.Run(right, Size3.Zero, pad, PaddingShader.FillMode.Clamp, null, shared, false);
                if (overlay != null)
                    overlay = (TextureArray2D) shared.Padding.Run(overlay, Size3.Zero, pad,
                        PaddingShader.FillMode.Transparent, null, shared, false);

                Debug.Assert(left.Size.Width % 2 == 0 && left.Size.Height % 2 == 0);
            }

            // prepare parallel processing
            var numTasks = Environment.ProcessorCount;
            var tasks = new Task[numTasks];
            var images = new DllImageData[numTasks];

            try
            {
                var leftView = left.GetSrView(LayerMipmapSlice.Mip0);
                var rightView = right.GetSrView(LayerMipmapSlice.Mip0);
                var overlayView = overlay?.GetSrView(LayerMipmapSlice.Mip0);

                var curProg = progress.CreateSubProgress(0.9f);

                // prepare parallel processing
                for (int i = 0; i < numTasks; ++i)
                    images[i] = IO.CreateImage(new ImageFormat(Format.R8G8B8A8_UNorm_SRgb), left.Size,
                        LayerMipmapCount.One);
                int textSize = left.Size.Y / 18;
                float padding = textSize / 4.0f;

                // render frames into texture
                using (var frame = new TextureArray2D(LayerMipmapCount.One, left.Size,
                    Format.R8G8B8A8_UNorm_SRgb, false))
                {
                    var frameView = frame.GetRtView(LayerMipmapSlice.Mip0);
                    using (var d2d = new Direct2D(frame))
                    {
                        for (int i = 0; i < numFrames; ++i)
                        {
                            float t = (float)i / (numFrames - 1);
                            int borderPos = (int)(t * (frame.Size.Width - 1));
                            int idx = i % numTasks;

                            // render frame
                            shader.Run(leftView, rightView, overlayView, frameView, cfg.SliderWidth, borderPos,
                                frame.Size.Width, frame.Size.Height, shared.QuadShader, shared.Upload);

                            // add text
                            using (var c = d2d.Begin())
                            {
                                c.Text(new Float2(padding), new Float2(left.Size.X - padding, left.Size.Y - padding),
                                    textSize, Colors.White, cfg.Label1, TextAlignment.Leading);

                                c.Text(new Float2(padding), new Float2(left.Size.X - padding, left.Size.Y - padding), textSize,
                                        Colors.White, cfg.Label2, TextAlignment.Trailing);
                            }

                            // copy frame from gpu to cpu
                            var dstMip = images[idx].GetMipmap(LayerMipmapSlice.Mip0);
                            var dstPtr = dstMip.Bytes;
                            var dstSize = dstMip.ByteSize;

                            // wait for previous task to finish before writing it to the file
                            if (tasks[idx] != null) await tasks[idx];

                            frame.CopyPixels(LayerMipmapSlice.Mip0, dstPtr, (uint)dstSize);
                            var filename = $"{cfg.TmpDirectory}\\frame{i:D4}";

                            // write to file
                            tasks[idx] = Task.Run(() =>
                            {
                                try
                                {
                                    IO.SaveImage(images[idx], filename, "png", GliFormat.RGBA8_SRGB);
                                }
                                catch (Exception)
                                {
                                    // ignored (probably cancelled by user)
                                }
                            }, progress.Token);

                            curProg.Progress = i / (float)numFrames;
                            curProg.What = "creating frames";

                            progress.Token.ThrowIfCancellationRequested();
                        }
                    }
                }

                var totalFrames = WriteFileList(cfg);

                // wait for tasks to finish
                for (var i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i] != null) await tasks[i];
                    tasks[i] = null;
                }


                // convert video
                await FFMpeg.ConvertAsync(cfg, progress.CreateSubProgress(1.0f), totalFrames);
            }
            finally
            {
                if(disposeImages)
                {
                    left.Dispose();
                    right.Dispose();
                    overlay?.Dispose();
                }

                // dispose images
                foreach (var dllImageData in images)
                {
                    dllImageData?.Dispose();
                }
            }
        }

        private int WriteFileList(Config cfg)
        {
            var numFrames = cfg.FramesPerSecond * cfg.NumSeconds;

            // convert repeat range to frame indices
            var frameRepeat = new Queue<Size2>();
            if (cfg.RepeatRange != null)
            {
                foreach (var float2 in cfg.RepeatRange)
                {
                    frameRepeat.Enqueue(new Size2
                    {
                        X = Utility.Utility.Clamp((int)Math.Ceiling(float2.X * (numFrames - 1)), 0, numFrames - 1),
                        Y = Utility.Utility.Clamp((int)Math.Floor(float2.Y * (numFrames - 1)), 0, numFrames - 1),
                    });
                }
            }

            // create file list
            var curFiles = new StringBuilder();
            int totalFrames = 0;
            for (int i = 0; i < numFrames; ++i)
            {
                curFiles.AppendLine($"file '{cfg.TmpDirectory}\\frame{i:D4}.png'");
                ++totalFrames;

                if (frameRepeat.Count > 0 && frameRepeat.Peek().Y == i)
                {
                    var cur = frameRepeat.Dequeue();
                    // repeat the specified segment
                    for (int rep = 0; rep < cfg.RepeatRangeCount; ++rep)
                    {
                        // backwards
                        for (int j = cur.Y - 1; j > cur.X; --j)
                        {
                            curFiles.AppendLine($"file '{cfg.TmpDirectory}\\frame{j:D4}.png'");
                            ++totalFrames;
                        }
                        // forwards
                        for (int j = cur.X; j <= cur.Y; ++j)
                        {
                            curFiles.AppendLine($"file '{cfg.TmpDirectory}\\frame{j:D4}.png'");
                            ++totalFrames;
                        }
                    }
                }
            }
            File.WriteAllText($"{cfg.TmpDirectory}\\files.txt", curFiles.ToString());

            return totalFrames;
        }

        public void Dispose()
        {
            shader?.Dispose();
        }
    }
}
