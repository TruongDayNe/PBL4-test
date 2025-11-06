using System;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace RealTimeUdpStream.Core.ViGEm
{
    /// <summary>
    /// Wrapper cho ViGEm Xbox 360 Controller - Giả lập controller ảo
    /// Ánh xạ phím IJKL thành joystick trái
    /// </summary>
    public class ViGEmController : IDisposable
    {
        private ViGEmClient _client;
        private IXbox360Controller _controller;
        private bool _disposed = false;

        // Trạng thái các phím đang nhấn
        private bool _isIPressed = false; // Lên
        private bool _isKPressed = false; // Xuống
        private bool _isJPressed = false; // Trái
        private bool _isLPressed = false; // Phải
        private bool _isOPressed = false; // Nút A

        public ViGEmController()
        {
            try
            {
                _client = new ViGEmClient();
                _controller = _client.CreateXbox360Controller();
                _controller.Connect();
                Console.WriteLine("[ViGEmController] Xbox 360 controller ao da duoc tao va ket noi");
            }
            catch (Exception ex)
            {
                throw new Exception($"Loi khi khoi tao ViGEm controller: {ex.Message}. Dam bao da cai ViGEmBus driver!");
            }
        }

        /// <summary>
        /// Xử lý phím I - Joystick lên
        /// </summary>
        public void SetIPressedState(bool pressed)
        {
            _isIPressed = pressed;
            UpdateJoystick();
        }

        /// <summary>
        /// Xử lý phím K - Joystick xuống
        /// </summary>
        public void SetKPressedState(bool pressed)
        {
            _isKPressed = pressed;
            UpdateJoystick();
        }

        /// <summary>
        /// Xử lý phím J - Joystick trái
        /// </summary>
        public void SetJPressedState(bool pressed)
        {
            _isJPressed = pressed;
            UpdateJoystick();
        }

        /// <summary>
        /// Xử lý phím L - Joystick phải
        /// </summary>
        public void SetLPressedState(bool pressed)
        {
            _isLPressed = pressed;
            UpdateJoystick();
        }

        /// <summary>
        /// Xử lý phím O - Nút A trên Xbox controller
        /// </summary>
        public void SetOPressedState(bool pressed)
        {
            _isOPressed = pressed;
            UpdateButtons();
        }

        /// <summary>
        /// Cập nhật trạng thái joystick dựa trên các phím đang nhấn
        /// </summary>
        private void UpdateJoystick()
        {
            if (_disposed) return;

            // Tính toán giá trị trục X (trái/phải)
            short thumbX = 0;
            if (_isJPressed) thumbX = -32767; // Trái
            if (_isLPressed) thumbX = 32767;  // Phải

            // Tính toán giá trị trục Y (lên/xuống)
            short thumbY = 0;
            if (_isIPressed) thumbY = 32767;  // Lên
            if (_isKPressed) thumbY = -32767; // Xuống

            // Cập nhật joystick trái
            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, thumbX);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, thumbY);
            
            // Submit report để áp dụng thay đổi
            _controller.SubmitReport();

            Console.WriteLine($"[ViGEmController] Joystick cap nhat: X={thumbX}, Y={thumbY} - Report submitted!");
        }

        /// <summary>
        /// Cập nhật trạng thái các nút bấm trên controller
        /// </summary>
        private void UpdateButtons()
        {
            if (_disposed) return;

            // Set nút A (O key)
            if (_isOPressed)
            {
                _controller.SetButtonState(Xbox360Button.A, true);
                Console.WriteLine("[ViGEmController] Nut A PRESSED");
            }
            else
            {
                _controller.SetButtonState(Xbox360Button.A, false);
                Console.WriteLine("[ViGEmController] Nut A RELEASED");
            }

            // Submit report để áp dụng thay đổi
            _controller.SubmitReport();
        }

        /// <summary>
        /// Reset tất cả phím về trạng thái không nhấn
        /// </summary>
        public void ResetAll()
        {
            _isIPressed = false;
            _isKPressed = false;
            _isJPressed = false;
            _isLPressed = false;
            _isOPressed = false;
            UpdateJoystick();
            UpdateButtons();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                ResetAll();
                _controller?.Disconnect();
                _client?.Dispose();
                Console.WriteLine("[ViGEmController] Da dong ket noi controller ao");
            }
            catch { }

            _disposed = true;
        }
    }
}
