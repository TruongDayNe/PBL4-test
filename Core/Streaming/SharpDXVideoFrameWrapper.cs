using FFMpegCore.Pipes;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Streaming
{
    /// <summary>
    /// Wrapper cho IVideoFrame, cho phép FFMpegCore đọc dữ liệu
    /// từ một Texture2D (staging) của SharpDX.
    /// </summary>
    public class SharpDXVideoFrameWrapper : IVideoFrame, IDisposable
    {
        private readonly Device _device;
        private readonly Texture2D _stagingTexture;
        private bool _isDisposed;

        public SharpDXVideoFrameWrapper(Device device, Texture2D stagingTexture)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _stagingTexture = stagingTexture ?? throw new ArgumentNullException(nameof(stagingTexture));

            Width = _stagingTexture.Description.Width;
            Height = _stagingTexture.Description.Height;
            Format = ConvertStreamFormat(_stagingTexture.Description.Format);
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// Định dạng pixel mà FFmpeg hiểu (ví dụ: "bgra").
        /// </summary>
        public string Format { get; }

        public void Dispose()
        {
            // Wrapper này không sở hữu staging texture,
            // vòng lặp chụp sẽ quản lý nó.
            _isDisposed = true;
        }

        public void Serialize(Stream stream)
        {
            // Ưu tiên phiên bản bất đồng bộ
            SerializeAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task SerializeAsync(Stream stream, CancellationToken token)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SharpDXVideoFrameWrapper));

            var context = _device.ImmediateContext;
            DataBox box;
            try
            {
                // Map texture (khóa nó trên CPU để đọc)
                box = context.MapSubresource(_stagingTexture, 0, MapMode.Read, MapFlags.None);
            }
            catch (SharpDXException ex)
            {
                // Thường xảy ra nếu thiết bị bị mất
                Console.WriteLine($"Failed to map subresource: {ex.Message}");
                return;
            }

            try
            {
                int rowPitch = box.RowPitch;
                int totalSize = rowPitch * Height;
                var buffer = new byte[totalSize];

                // Sao chép toàn bộ dữ liệu từ GPU (thông qua staging) sang bộ đệm byte[]
                //
                Marshal.Copy(box.DataPointer, buffer, 0, totalSize);

                // Ghi bộ đệm vào pipe của FFmpeg
                await stream.WriteAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            }
            finally
            {
                // Rất quan trọng: Luôn unmap texture
                context.UnmapSubresource(_stagingTexture, 0);
            }
        }

        private static string ConvertStreamFormat(Format fmt)
        {
            // Định dạng phổ biến nhất khi chụp màn hình là B8G8R8A8_UNorm
            switch (fmt)
            {
                case SharpDX.DXGI.Format.B8G8R8A8_UNorm:
                    return "bgra";
                case SharpDX.DXGI.Format.R8G8B8A8_UNorm:
                    return "rgba";
                // Thêm các định dạng khác nếu bạn cần
                default:
                    throw new NotSupportedException($"SharpDX format {fmt} not supported.");
            }
        }
    }
}