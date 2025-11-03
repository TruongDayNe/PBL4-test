using System;
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
        public event Action<string> ClientConnected;

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
                        Task.Run(() => HandleNewClient(client), token);
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
                Debug.WriteLine("[Host] Đã dừng lắng nghe TCP.");
            }
        }

        /// <summary>
        /// Xử lý logic đọc IP từ một client TCP
        /// </summary>
        private void HandleNewClient(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length); // Dùng Read đồng bộ vì đang ở Task riêng
                    string clientIp = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Debug.WriteLine($"[Host] Nhận được IP của Client: {clientIp}");

                    // Kích hoạt sự kiện để báo cho HostViewModel biết IP của Client
                    ClientConnected?.Invoke(clientIp);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Lỗi khi xử lý client TCP: {ex.Message}");
            }
            finally
            {
                client.Close();
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
                    await client.ConnectAsync(hostIpAddress, HandshakePort);
                    Debug.WriteLine("[Client] Kết nối TCP thành công!");

                    using (NetworkStream stream = client.GetStream())
                    {
                        string localIp = GetLocalIPAddress();
                        if (string.IsNullOrEmpty(localIp))
                        {
                            Debug.WriteLine("[Client] Lỗi: Không thể lấy địa chỉ IP cục bộ.");
                            return false;
                        }

                        Debug.WriteLine($"[Client] Gửi địa chỉ IP cục bộ ({localIp}) đến Host...");
                        byte[] ipBytes = Encoding.UTF8.GetBytes(localIp);
                        await stream.WriteAsync(ipBytes, 0, ipBytes.Length);
                        Debug.WriteLine("[Client] Gửi IP thành công.");
                    }
                }
                return true;
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
