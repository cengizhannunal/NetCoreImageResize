using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace NetCoreImageResizeWithRedis.Controllers
{
    public class ImageController : Controller
    {
        private IDistributedCache _distributedCache;

        public ImageController(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        public async Task<IActionResult> Index(string url, int w, int h)
        {
            try
            {
                string cacheKey = $"{url}?w={w}&h={h}";
                var cachaData = await _distributedCache.GetAsync(cacheKey);
                if (cachaData != null)
                {
                    byte[] imageBytes = (byte[])cachaData;
                    var image = ByteArrayConvertToImage(imageBytes);
                    var outputStream = ImageConvertToStream(image, ImageFormat.Jpeg);
                    return this.File(outputStream, "image/png");
                }

                using (Image sourceImage = await this.LoadImageFromUrl(url))
                {
                    if (sourceImage != null)
                    {
                        using (Image destinationImage = this.CropImage(sourceImage, w, h))
                        {
                            Stream outputStream = new MemoryStream();
                            destinationImage.Save(outputStream, ImageFormat.Jpeg);
                            outputStream.Seek(0, SeekOrigin.Begin);
                            var imageData = ImageToByteArray(destinationImage);
                            await _distributedCache.SetAsync(cacheKey, imageData);
                            var imageResult = this.File(outputStream, "image/png");
                            return imageResult;
                        }
                    }
                }
            }
            catch
            {
            }

            return this.NotFound();

        }
        private async Task<Image> LoadImageFromUrl(string url)
        {
            Image image = null;
            try
            {
                using (HttpClient httpClient = new HttpClient())

                using (HttpResponseMessage response = await httpClient.GetAsync(url))

                using (Stream inputStream = await response.Content.ReadAsStreamAsync())

                using (Bitmap temp = new Bitmap(inputStream))

                    image = new Bitmap(temp);
            }
            catch
            {
                // Add error logging here
            }

            return image;
        }
        private Image CropImage(Image sourceImage, int w, int h)
        {

            var resized = new Bitmap(w, h);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImage(sourceImage, 0, 0, w, h);
            }
            return resized;
        }

        public byte[] ImageToByteArray(System.Drawing.Image image)
        {
            using (var memorStream = new MemoryStream())
            {
                image.Save(memorStream, image.RawFormat);
                return memorStream.ToArray();
            }
        }
        public Image ByteArrayConvertToImage(byte[] byteArray)
        {
            MemoryStream memoryStream = new MemoryStream(byteArray);
            Image image = Image.FromStream(memoryStream);
            return image;
        }

        public Stream ImageConvertToStream(Image image, ImageFormat imageFormat)
        {
            var stream = new System.IO.MemoryStream();
            image.Save(stream, imageFormat);
            stream.Position = 0;
            return stream;
        }

    }
}