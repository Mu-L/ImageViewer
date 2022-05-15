﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageFramework.DirectX;
using ImageFramework.ImageLoader;
using ImageFramework.Model;
using ImageFramework.Model.Export;
using ImageFramework.Model.Progress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX.Direct2D1;

namespace FrameworkTests.Model
{
    [TestClass]
    public class FFmpegTest
    {
        [ClassInitialize]
        public static void Init(TestContext context)
        {
            Assert.IsTrue(FFMpeg.IsAvailable(), "FFMPEG is not available => FFMPEG tests cannot be executed");
        }

        [TestMethod]
        public void MovieMetadata()
        {
            var data = FFMpeg.GetMovieMetadata(TestData.Directory + "einstein/movie.mp4");

            Assert.AreEqual(30, data.FramesPerSecond);
            Assert.AreEqual(60, data.FrameCount);
        }

        [TestMethod]
        public void MovieImport()
        {
            var models = new Models();

            var data = FFMpeg.GetMovieMetadata(TestData.Directory + "einstein/movie.mp4");

            var task = FFMpeg.ImportMovie(data, models.Progress.GetProgressInterface(new CancellationToken()));
            task.Wait();


        }

        [TestMethod]
        public void DiffGifExport()
        {
            var models = new Models();

            var img1 = IO.LoadImageTexture(TestData.Directory + "einstein/ref.jpg");
            var img2 = IO.LoadImageTexture(TestData.Directory + "einstein/ssim0662.jpg");

            var cfg = new GifModel.Config
            {
                Filename = Path.GetFullPath(TestData.Directory + "einstein\\gif_output.mp4"),
                TmpDirectory = Path.GetFullPath(TestData.Directory + "einstein\\giftmp"),
                FramesPerSecond = 30,
                SliderWidth = 3,
                NumSeconds = 2,
                Label1 = "ref",
                Label2 = "ssim0062",
                Left = (TextureArray2D)img1,
                Right = (TextureArray2D)img2
            };

            // delete olf file if it exists (otherwise ffmpeg will hang)
            System.IO.File.Delete(cfg.Filename);
            TestData.CreateOutputDirectory(cfg.TmpDirectory);

            models.Gif.CreateGif(cfg, models.SharedModel);

            models.Progress.WaitForTask();

            Assert.IsTrue(String.IsNullOrEmpty(models.Progress.LastError), models.Progress.LastError);

            Assert.IsTrue(System.IO.File.Exists(cfg.Filename));
        }
    }
}