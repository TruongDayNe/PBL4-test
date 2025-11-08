using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading; // Thêm CancellationToken
using System.Threading.Tasks;
using System.Linq;

namespace Core.Networking
{
    public class NetworkService
    {
        public const int HandshakePort = 12345;
        public event Action<string, string> ClientRequestReceived;
        public event Action<string> ClientAccepted;

        private readonly ConcurrentDictionary<string, TcpClient> _pendingClients = new ConcurrentDictionary<string, TcpClient>();

        private TcpListener _tcpListener; // Biến thành viên để có thể Stop() từ bên ngoài

        /// <summary>
        /// [HOST] Bắt đầu lắng nghe KẾT NỐI TCP TỪ NHIỀU CLIENT liên tục.
        /// </summary>
        public async Task StartTcpListenerLoopAsync(CancellationToken token)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, HandshakePort);
                _tcpListener.Start();
                Debug.WriteLine($"[Host] Đang lắng nghe kết nối TCP trên port {HandshakePort}...");

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Chấp nhận client mới
                        TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                        Debug.WriteLine("[Host] Client mới đang kết nối...");

                        // Xử lý client này trên một Task riêng để quay lại vòng lặp Accept ngay
                        _ = HandleNewClientAsync(client, token);
                    }
                    catch (SocketException ex) when (token.IsCancellationRequested)
                    {
                        Debug.WriteLine("[Host] TCP Listener_tcpListener stopped (via token).");
                        break; // Thoát vòng lặp khi bị hủy
                    }
                    catch (ObjectDisposedException)
                    {
                        Debug.WriteLine("[Host] TCP Listener_tcpListener stopped (disposed).");
                        break; // Thoát vòng lặp
                    }
                    catch (Exception ex)
                    {
                        if (!token.IsCancellationRequested)
                            Debug.WriteLine($"[Host] Lỗi khi chấp nhận client: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) // Lỗi khi Start() listener (ví dụ: port đã dùng)
            {
                Debug.WriteLine($"[Host] Lỗi nghiêm trọng khi khởi động TCP listener: {ex.Message}");
                throw; // Ném lỗi ra ngoài để HostViewModel bắt được
            }
            finally
            {
                _tcpListener?.Stop();
                foreach (var pair in _pendingClients)
                {
                    pair.Value.Close();
                }
                _pendingClients.Clear();
                Debug.WriteLine("[Host] Đã dừng lắng nghe TCP.");
            }
        }

        /// <summary>
        /// Xử lý logic đọc IP từ một client TCP
        /// </summary>
        private async Task HandleNewClientAsync(TcpClient client, CancellationToken token)
        {
            // LẤY IP AN TOÀN: Lấy IP từ chính kết nối TCP, không tin Client
            string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length, token);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), token); // 10 giây để gửi request

                    if (await Task.WhenAny(readTask, timeoutTask) == timeoutTask)
                    {
                        // Timeout
                        throw new TimeoutException("Client did not send request in time.");
                    }

                    // Nếu đến đây, readTask đã hoàn thành
                    int bytesRead = await readTask;

                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string displayName = "Unknown Client";

                        // Phân tích request (ví dụ: "CONNECT:MyPCName")
                        if (request.StartsWith("CONNECT:"))
                        {
                            displayName = request.Substring(8);
                        }

                        Debug.WriteLine($"[Host] Nhận được yêu cầu từ: {clientIp} (Tên: {displayName})");

                        // Thêm vào danh sách chờ và giữ kết nối TCP
                        if (_pendingClients.TryAdd(clientIp, client))
                        {
                            // Kích hoạt sự kiện để báo cho UI
                            ClientRequestReceived?.Invoke(clientIp, displayName);

                            // Đợi cho đến khi Host chấp nhận/từ chối hoặc token bị hủy
                            await Task.Delay(Timeout.Infinite, token);
                        }
                        else
                        {
                            // Đã có client từ IP này đang chờ
                            await SendResponseAndClose(client, "REJECT:BUSY");
                        }
                    
                }
            }
            catch (Exception ex) // Bắt lỗi (timeout, client ngắt kết nối...)
            {
                Debug.WriteLine($"[Host] Lỗi khi xử lý client {clientIp}: {ex.Message}");
             
            }
            finally
            {
                // Đảm bảo client được đóng nếu nó vẫn còn trong danh sách
                if (_pendingClients.TryRemove(clientIp, out var finalClient))
                {
                    finalClient.Close();
                }
            }
        }

        private async Task SendResponseAndClose(TcpClient client, string response)
        {
            try
            {
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await client.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Lỗi khi gửi phản hồi: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        // HÀM MỚI: Host gọi khi nhấn "Chấp nhận"
        public async Task AcceptClientAsync(string clientIp)
        {
            if (_pendingClients.TryRemove(clientIp, out TcpClient client))
            {
                Debug.WriteLine($"[Host] Chấp nhận client: {clientIp}");
                await SendResponseAndClose(client, "ACCEPT");
                ClientAccepted?.Invoke(clientIp); // Báo cho HostViewModel bắt đầu stream
            }
        }

        // HÀM MỚI: Host gọi khi nhấn "Từ chối"
        public async Task RejectClientAsync(string clientIp)
        {
            if (_pendingClients.TryRemove(clientIp, out TcpClient client))
            {
                Debug.WriteLine($"[Host] Từ chối client: {clientIp}");
                await SendResponseAndClose(client, "REJECT:DENIED");
            }
        }

        /// <summary>
        /// [HOST] Hàm mới để dừng TcpListener từ bên ngoài
        /// </summary>
        public void StopListening()
        {
            _tcpListener?.Stop();
        }

        /// <summary>
        /// [CLIENT] Kết nối đến Host qua TCP và gửi địa chỉ IP cục bộ của Client.
        /// </summary>
        public async Task<bool> ConnectToHostAsync(string hostIpAddress)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    Debug.WriteLine($"[Client] Đang kết nối TCP tới Host {hostIpAddress}:{HandshakePort}...");
                    // Thêm timeout cho kết nối
                    var connectTask = client.ConnectAsync(hostIpAddress, HandshakePort);
                    if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                    {
                        throw new TimeoutException("Connection timed out.");
                    }

                    Debug.WriteLine("[Client] Kết nối TCP thành công!");

                    using (NetworkStream stream = client.GetStream())
                    {
                        // SỬA ĐỔI: Gửi yêu cầu kết nối thay vì gửi IP
                        string displayName = Environment.MachineName; // Gửi tên máy
                        string request = $"CONNECT:{displayName}";
                        byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                        await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                        Debug.WriteLine($"[Client] Gửi yêu cầu: {request}");

                        // SỬA ĐỔI: Chờ phản hồi từ Host
                        byte[] buffer = new byte[1024];

                        // Thêm timeout 30 giây chờ Host chấp nhận
                        var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                        if (await Task.WhenAny(readTask, Task.Delay(30000)) != readTask)
                        {
                            throw new TimeoutException("Host did not respond in time.");
                        }

                        int bytesRead = await readTask;
                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Debug.WriteLine($"[Client] Nhận phản hồi từ Host: {response}");

                        return response == "ACCEPT";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Client] Lỗi khi kết nối TCP tới Host: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lấy địa chỉ IPv4 cục bộ của máy.
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork) // Chỉ lấy IPv4
                    {
                        return ip.ToString();
                    }
                }
                // Nếu không tìm thấy IPv4, thử cách khác (ít tin cậy hơn)
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530); // Kết nối ảo đến Google DNS
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi lấy IP cục bộ: {ex.Message}");
                return null;
            }
        }
    }
}
