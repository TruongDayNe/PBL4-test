using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Pipes;
using Newtonsoft.Json; // Thêm gói Nuget Newtonsoft.Json vào project Core
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
// using Core.Networking; // Giả sử đây là namespace của UdpPeer

// Placeholder cho class UDP của bạn
// Bạn nên thay thế bằng UdpPeer
namespace Core.Networking
{
    public class MyCustomUdpSender : IDisposable
    {
        private UdpClient _client;
        private string _host;
        private int _port;
        public MyCustomUdpSender(string host, int port)
        {
            _host = host;
            _port = port;
            _client = new UdpClient();
        }
        public async Task SendAsync(byte[] data, int length)
        {
            await _client.SendAsync(data, length, _host, _port);
        }
        public void Dispose()
        {
            _client.Close();
            _client.Dispose();
        }
    }
}


namespace Core.Streaming
{
    public class StreamingService
    {
        private CancellationTokenSource _cts;
        private Task _streamingTask;
        private Core.Networking.MyCustomUdpSender _udpSender;
        private StreamConfig _config;

        /// <summary>
        /// Bắt đầu stream, được gọi từ WPF UI.
        /// </summary>
        /// <param name="configJson">Chuỗi JSON của đối tượng StreamConfig.</param>
        /// <param name="udpHost">Địa chỉ IP để gửi UDP.</param>
        /// <param name="udpPort">Cổng để gửi UDP.</param>
        public void StartStream(string configJson, string udpHost, int udpPort)
        {
            if (_streamingTask != null && !_streamingTask.IsCompleted)
            {
                return; // Đã đang stream
            }

            _config = JsonConvert.DeserializeObject<StreamConfig>(configJson);
            _udpSender = new Core.Networking.MyCustomUdpSender(udpHost, udpPort);
            _cts = new CancellationTokenSource();

            // Tạo pipe source đầu vào (Input Pipe)
            //
            var rawVideoPipeSource = new RawVideoPipeSource(CaptureFrames(_cts.Token))
            {
                FrameRate = _config.FrameRate
            };

            // Tạo pipe sink đầu ra (Output Pipe)
            //
            var pipeSink = new StreamPipeSink(HandleCompressedStream, _cts.Token)
            {
                Format = _config.OutputFormat
            };

            //
            var outputPipeArgument = new OutputPipeArgument(pipeSink);

            // Xây dựng các đối số FFmpeg
            var arguments = FFMpegArguments
                .FromPipe(rawVideoPipeSource) // -i pipe:0
                .OutputToPipe(outputPipeArgument, options => options
                    .WithVideoCodec(_config.VideoCodec)
                    .WithSpeedPreset(_config.Preset)
                    .WithConstantRateFactor(_config.Crf)
                    .WithVideoPixelFormat(_config.PixelFormat)
                    .WithFrameRate(_config.FrameRate)
                    .ForceFormat(_config.OutputFormat) // Quan trọng: -f mpegts
                    .WithCustomArgument("-tune zerolatency") // Tối ưu độ trễ thấp
                    .WithCustomArgument("-muxdelay 0 -muxpreload 0")
                );

            // Chạy FFmpeg
            _streamingTask = arguments.ProcessAsynchronously(true, _cts.Token);
        }

        public void StopStream()
        {
            try
            {
                _cts?.Cancel();
                _streamingTask?.Wait(2000); // Chờ process kết thúc
            }
            catch { /* Bỏ qua lỗi */ }
            finally
            {
                _udpSender?.Dispose();
                _cts?.Dispose();
                _streamingTask = null;
            }
        }

        /// <summary>
        /// Vòng lặp chụp màn hình bằng SharpDX.
        /// Đây là nơi cung cấp frame cho RawVideoPipeSource.
        /// </summary>
        private IEnumerable<IVideoFrame> CaptureFrames(CancellationToken token)
        {
            // --- Khởi tạo SharpDX ---
            // (Bạn có thể cần chọn adapter/output cụ thể)
            using (var factory = new Factory1())
            using (var adapter = factory.GetAdapter1(0))
            using (var device = new SharpDX.Direct3D11.Device(adapter))
            using (var output = adapter.GetOutput(0))
            using (var output1 = output.QueryInterface<Output1>())
            using (var outputDuplication = output1.DuplicateOutput(device))
            {
                var textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = output.Description.DesktopBounds.Width,
                    Height = output.Description.DesktopBounds.Height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };

                // Tạo một staging texture để tái sử dụng
                using (var stagingTexture = new Texture2D(device, textureDesc))
                {
                    while (!token.IsCancellationRequested)
                    {
                        SharpDX.DXGI.Resource frameResource = null;
                        try
                        {
                            // Lấy frame tiếp theo từ GPU
                            var result = outputDuplication.AcquireNextFrame(100, out _, out frameResource);
                            if (result.Success && frameResource != null)
                            {
                                // Sao chép từ texture GPU sang staging texture (CPU có thể đọc)
                                using (var gpuTexture = frameResource.QueryInterface<Texture2D>())
                                {
                                    device.ImmediateContext.CopyResource(gpuTexture, stagingTexture);
                                }

                                // Cung cấp frame cho FFmpeg
                                yield return new SharpDXVideoFrameWrapper(device, stagingTexture);

                                frameResource.Dispose();
                                outputDuplication.ReleaseFrame();
                            }
                            else if (result.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Code)
                            {
                                // Không có frame mới, tiếp tục
                                continue;
                            }
                            else
                            {
                                // Lỗi, thử khởi động lại
                                throw new Exception($"Lỗi chụp frame: {result.Code}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Lỗi vòng lặp chụp: {ex.Message}");
                            frameResource?.Dispose();
                            // Tạm dừng một chút trước khi thử lại
                            Task.Delay(500, token).Wait(token);
                            break; // Thoát vòng lặp để thử khởi động lại
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Hàm này được StreamPipeSink gọi với dữ liệu đã nén từ FFmpeg.
        /// </summary>
        private async Task HandleCompressedStream(Stream compressedStream, CancellationToken token)
        {
            // Kích thước buffer tốt cho MPEG-TS qua UDP (7 * 188 bytes = 1316)
            var buffer = new byte[1316];
            int bytesRead;

            try
            {
                while ((bytesRead = await compressedStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    if (_udpSender != null)
                    {
                        // Gửi dữ liệu đã nén qua UDP
                        await _udpSender.SendAsync(buffer, bytesRead);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stream bị hủy, đây là điều bình thường khi dừng
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi gửi UDP: {ex.Message}");
            }
        }
    }
}