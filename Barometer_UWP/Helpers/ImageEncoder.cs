using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace Barometer_UWP.Helpers
{
    public static class ImageEncoder
    {
        /// <summary>Encodes a WriteableBitmap to PNG or JPEG bytes.</summary>
        public static async Task<byte[]> EncodeAsync(WriteableBitmap bmp, bool asPng)
        {
            if (bmp == null) return null;
            using (var stream = new InMemoryRandomAccessStream())
            {
                await bmp.ToStream().SaveAsync(stream);

                Guid encoderId = asPng
                    ? BitmapEncoder.PngEncoderId
                    : BitmapEncoder.JpegEncoderId;

                var decoder = await BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync();

                using (var outStream = new InMemoryRandomAccessStream())
                {
                    var encoder = await BitmapEncoder.CreateAsync(encoderId, outStream);
                    encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode,
                        decoder.OrientedPixelWidth, decoder.OrientedPixelHeight,
                        decoder.DpiX, decoder.DpiY, pixelData.DetachPixelData());
                    await encoder.FlushAsync();

                    outStream.Seek(0);
                    using (var reader = new DataReader(outStream))
                    {
                        await reader.LoadAsync((uint)outStream.Size);
                        var bytes = new byte[outStream.Size];
                        reader.ReadBytes(bytes);
                        return bytes;
                    }
                }
            }
        }

        public static async Task SaveToStreamAsync(WriteableBitmap bmp, Stream stream, bool asPng)
        {
            var bytes = await EncodeAsync(bmp, asPng);
            if (bytes != null)
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }
        }
    }
}
