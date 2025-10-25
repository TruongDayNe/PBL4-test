using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq; // Thêm using này

namespace Core.Networking
{
    public class NetworkService
    {
        // Port cố định cho việc "chào hỏi" ban đầu qua TCP
        public const int HandshakePort = 12345;

        // Sự kiện được kích hoạt ở Host khi Client kết nối và gửi IP
        public event Action<string> ClientConnected;

        /// <summary>
        /// [HOST] Bắt đầu lắng nghe kết nối TCP từ Client.
        /// </summary>
        public async Task StartListeningForClientAsync()
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, HandshakePort);
                listener.Start();
                Debug.WriteLine($"[Host] Đang lắng nghe kết nối TCP trên port {HandshakePort}...");

                // Chỉ chấp nhận một kết nối cho ví dụ đơn giản này
                TcpClient client = await listener.AcceptTcpClientAsync();
                Debug.WriteLine("[Host] Client đã kết nối!");

                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string clientIp = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Debug.WriteLine($"[Host] Nhận được IP của Client: {clientIp}");

                    // Kích hoạt sự kiện để báo cho HostViewModel biết IP của Client
                    ClientConnected?.Invoke(clientIp);
                }
                client.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Host] Lỗi khi lắng nghe TCP: {ex.Message}");
                // Có thể thêm thông báo lỗi cho người dùng ở đây
            }
            finally
            {
                listener?.Stop();
                Debug.WriteLine("[Host] Đã dừng lắng nghe TCP.");
            }
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

