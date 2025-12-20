using System;
using System.Collections.Generic;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using RealTimeUdpStream.Core.Input;
using RealTimeUdpStream.Core.Models;

namespace RealTimeUdpStream.Core.ViGEm
{
    /// <summary>
    /// Wrapper cho ViGEm Xbox 360 Controller - Giả lập controller ảo
    /// Dynamic mapping từ config file
    /// </summary>
    public class ViGEmController : IDisposable
    {
        private ViGEmClient _client;
        private IXbox360Controller _controller;
        private bool _disposed = false;

        // Trạng thái các trục analog
        private Dictionary<string, bool> _pressedKeys = new Dictionary<string, bool>();
        private short _leftStickX = 0;
        private short _leftStickY = 0;
        private short _rightStickX = 0;
        private short _rightStickY = 0;
        private byte _leftTrigger = 0;
        private byte _rightTrigger = 0;

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
        /// Xử lý sự kiện phím dựa vào controller mapping từ config
        /// </summary>
        public void ProcessKeyEvent(VirtualKey key, bool pressed, KeyMappingConfig config)
        {
            if (_disposed || config?.ControllerMapping == null) return;

            string keyName = key.ToString();
            
            // Tìm mapping cho phím này
            if (!config.ControllerMapping.TryGetValue(keyName, out var mapping))
            {
                return; // Phím không có mapping
            }

            // Cập nhật state
            _pressedKeys[keyName] = pressed;

            // Xử lý theo loại mapping
            switch (mapping.Type)
            {
                // Left Stick
                case ControllerActionType.LeftStickUp:
                case ControllerActionType.LeftStickDown:
                case ControllerActionType.LeftStickLeft:
                case ControllerActionType.LeftStickRight:
                    UpdateLeftStick(config);
                    break;

                // Right Stick
                case ControllerActionType.RightStickUp:
                case ControllerActionType.RightStickDown:
                case ControllerActionType.RightStickLeft:
                case ControllerActionType.RightStickRight:
                    UpdateRightStick(config);
                    break;

                // Triggers
                case ControllerActionType.LeftTrigger:
                    _leftTrigger = pressed ? (byte)255 : (byte)0;
                    _controller.SetSliderValue(Xbox360Slider.LeftTrigger, _leftTrigger);
                    _controller.SubmitReport();
                    break;

                case ControllerActionType.RightTrigger:
                    _rightTrigger = pressed ? (byte)255 : (byte)0;
                    _controller.SetSliderValue(Xbox360Slider.RightTrigger, _rightTrigger);
                    _controller.SubmitReport();
                    break;

                // Shoulders
                case ControllerActionType.LeftShoulder:
                    _controller.SetButtonState(Xbox360Button.LeftShoulder, pressed);
                    _controller.SubmitReport();
                    break;

                case ControllerActionType.RightShoulder:
                    _controller.SetButtonState(Xbox360Button.RightShoulder, pressed);
                    _controller.SubmitReport();
                    break;

                // Face Buttons
                case ControllerActionType.ButtonA:
                    _controller.SetButtonState(Xbox360Button.A, pressed);
                    _controller.SubmitReport();
                    Console.WriteLine($"[ViGEmController] Button A {(pressed ? "PRESSED" : "RELEASED")}");
                    break;

                case ControllerActionType.ButtonB:
                    _controller.SetButtonState(Xbox360Button.B, pressed);
                    _controller.SubmitReport();
                    Console.WriteLine($"[ViGEmController] Button B {(pressed ? "PRESSED" : "RELEASED")}");
                    break;

                case ControllerActionType.ButtonX:
                    _controller.SetButtonState(Xbox360Button.X, pressed);
                    _controller.SubmitReport();
                    Console.WriteLine($"[ViGEmController] Button X {(pressed ? "PRESSED" : "RELEASED")}");
                    break;

                case ControllerActionType.ButtonY:
                    _controller.SetButtonState(Xbox360Button.Y, pressed);
                    _controller.SubmitReport();
                    Console.WriteLine($"[ViGEmController] Button Y {(pressed ? "PRESSED" : "RELEASED")}");
                    break;

                // D-Pad
                case ControllerActionType.DPadUp:
                    _controller.SetButtonState(Xbox360Button.Up, pressed);
                    _controller.SubmitReport();
                    break;

                case ControllerActionType.DPadDown:
                    _controller.SetButtonState(Xbox360Button.Down, pressed);
                    _controller.SubmitReport();
                    break;

                case ControllerActionType.DPadLeft:
                    _controller.SetButtonState(Xbox360Button.Left, pressed);
                    _controller.SubmitReport();
                    break;

                case ControllerActionType.DPadRight:
                    _controller.SetButtonState(Xbox360Button.Right, pressed);
                    _controller.SubmitReport();
                    break;

                // System Buttons
                case ControllerActionType.Start:
                    _controller.SetButtonState(Xbox360Button.Start, pressed);
                    _controller.SubmitReport();
                    break;

                case ControllerActionType.Back:
                    _controller.SetButtonState(Xbox360Button.Back, pressed);
                    _controller.SubmitReport();
                    break;

                case ControllerActionType.Guide:
                    _controller.SetButtonState(Xbox360Button.Guide, pressed);
                    _controller.SubmitReport();
                    break;

                // Stick Buttons
                case ControllerActionType.LeftStickButton:
                    _controller.SetButtonState(Xbox360Button.LeftThumb, pressed);
                    _controller.SubmitReport();
                    break;

                case ControllerActionType.RightStickButton:
                    _controller.SetButtonState(Xbox360Button.RightThumb, pressed);
                    _controller.SubmitReport();
                    break;
            }
        }

        /// <summary>
        /// Cập nhật Left Stick dựa trên tất cả phím đang nhấn
        /// </summary>
        private void UpdateLeftStick(KeyMappingConfig config)
        {
            short x = 0, y = 0;

            foreach (var kvp in config.ControllerMapping)
            {
                if (!_pressedKeys.ContainsKey(kvp.Key) || !_pressedKeys[kvp.Key])
                    continue;

                switch (kvp.Value.Type)
                {
                    case ControllerActionType.LeftStickUp:
                        y = 32767;
                        break;
                    case ControllerActionType.LeftStickDown:
                        y = -32767;
                        break;
                    case ControllerActionType.LeftStickLeft:
                        x = -32767;
                        break;
                    case ControllerActionType.LeftStickRight:
                        x = 32767;
                        break;
                }
            }

            _leftStickX = x;
            _leftStickY = y;
            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, x);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, y);
            _controller.SubmitReport();
            
            Console.WriteLine($"[ViGEmController] Left Stick: X={x}, Y={y}");
        }

        /// <summary>
        /// Cập nhật Right Stick dựa trên tất cả phím đang nhấn
        /// </summary>
        private void UpdateRightStick(KeyMappingConfig config)
        {
            short x = 0, y = 0;

            foreach (var kvp in config.ControllerMapping)
            {
                if (!_pressedKeys.ContainsKey(kvp.Key) || !_pressedKeys[kvp.Key])
                    continue;

                switch (kvp.Value.Type)
                {
                    case ControllerActionType.RightStickUp:
                        y = 32767;
                        break;
                    case ControllerActionType.RightStickDown:
                        y = -32767;
                        break;
                    case ControllerActionType.RightStickLeft:
                        x = -32767;
                        break;
                    case ControllerActionType.RightStickRight:
                        x = 32767;
                        break;
                }
            }

            _rightStickX = x;
            _rightStickY = y;
            _controller.SetAxisValue(Xbox360Axis.RightThumbX, x);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY, y);
            _controller.SubmitReport();
            
            Console.WriteLine($"[ViGEmController] Right Stick: X={x}, Y={y}");
        }

        /// <summary>
        /// Reset tất cả về trạng thái ban đầu
        /// </summary>
        public void ResetAll()
        {
            if (_disposed) return;

            _pressedKeys.Clear();
            _leftStickX = _leftStickY = 0;
            _rightStickX = _rightStickY = 0;
            _leftTrigger = _rightTrigger = 0;

            // Reset all buttons
            foreach (Xbox360Button button in Enum.GetValues(typeof(Xbox360Button)))
            {
                _controller.SetButtonState(button, false);
            }

            // Reset sticks
            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);

            // Reset triggers
            _controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
            _controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);

            _controller.SubmitReport();
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
