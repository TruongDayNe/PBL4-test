using System;
using System.Collections.Generic;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using RealTimeUdpStream.Core.Models;

namespace RealTimeUdpStream.Core.ViGEm
{
    /// <summary>
    /// Wrapper cho ViGEm Xbox 360 Controller - Giả lập controller ảo
    /// Hỗ trợ đầy đủ mapping từ config
    /// </summary>
    public class ViGEmController : IDisposable
    {
        private ViGEmClient _client;
        private IXbox360Controller _controller;
        private bool _disposed = false;

        // Trạng thái các input (analog values)
        private readonly Dictionary<ControllerActionType, float> _axisStates = new Dictionary<ControllerActionType, float>();
        private readonly Dictionary<ControllerActionType, bool> _buttonStates = new Dictionary<ControllerActionType, bool>();

        public ViGEmController()
        {
            try
            {
                _client = new ViGEmClient();
                _controller = _client.CreateXbox360Controller();
                _controller.Connect();
                
                // Initialize all states
                InitializeStates();
                
                Console.WriteLine("[ViGEmController] Xbox 360 controller virtual device created and connected");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize ViGEm controller: {ex.Message}. Make sure ViGEmBus driver is installed!");
            }
        }

        private void InitializeStates()
        {
            // Initialize all axis states to 0
            _axisStates[ControllerActionType.LeftStickUp] = 0f;
            _axisStates[ControllerActionType.LeftStickDown] = 0f;
            _axisStates[ControllerActionType.LeftStickLeft] = 0f;
            _axisStates[ControllerActionType.LeftStickRight] = 0f;
            _axisStates[ControllerActionType.RightStickUp] = 0f;
            _axisStates[ControllerActionType.RightStickDown] = 0f;
            _axisStates[ControllerActionType.RightStickLeft] = 0f;
            _axisStates[ControllerActionType.RightStickRight] = 0f;
            _axisStates[ControllerActionType.LeftTrigger] = 0f;
            _axisStates[ControllerActionType.RightTrigger] = 0f;
        }

        /// <summary>
        /// Set controller action state from config mapping
        /// </summary>
        public void SetActionState(ControllerActionType actionType, bool pressed, float value = 1.0f)
        {
            if (_disposed) return;

            // Handle buttons
            if (IsButtonAction(actionType))
            {
                _buttonStates[actionType] = pressed;
                UpdateButton(actionType, pressed);
            }
            // Handle analog inputs (sticks and triggers)
            else if (IsAxisAction(actionType))
            {
                _axisStates[actionType] = pressed ? value : 0f;
                UpdateAxis(actionType);
            }
        }

        private bool IsButtonAction(ControllerActionType actionType)
        {
            return actionType == ControllerActionType.ButtonA ||
                   actionType == ControllerActionType.ButtonB ||
                   actionType == ControllerActionType.ButtonX ||
                   actionType == ControllerActionType.ButtonY ||
                   actionType == ControllerActionType.LeftShoulder ||
                   actionType == ControllerActionType.RightShoulder ||
                   actionType == ControllerActionType.DPadUp ||
                   actionType == ControllerActionType.DPadDown ||
                   actionType == ControllerActionType.DPadLeft ||
                   actionType == ControllerActionType.DPadRight ||
                   actionType == ControllerActionType.Start ||
                   actionType == ControllerActionType.Back ||
                   actionType == ControllerActionType.Guide;
        }

        private bool IsAxisAction(ControllerActionType actionType)
        {
            return actionType == ControllerActionType.LeftStickUp ||
                   actionType == ControllerActionType.LeftStickDown ||
                   actionType == ControllerActionType.LeftStickLeft ||
                   actionType == ControllerActionType.LeftStickRight ||
                   actionType == ControllerActionType.RightStickUp ||
                   actionType == ControllerActionType.RightStickDown ||
                   actionType == ControllerActionType.RightStickLeft ||
                   actionType == ControllerActionType.RightStickRight ||
                   actionType == ControllerActionType.LeftTrigger ||
                   actionType == ControllerActionType.RightTrigger;
        }

        private void UpdateButton(ControllerActionType actionType, bool pressed)
        {
            bool hasButton = false;
            Xbox360Button button = Xbox360Button.A; // Default value
            
            switch (actionType)
            {
                case ControllerActionType.ButtonA:
                    button = Xbox360Button.A;
                    hasButton = true;
                    break;
                case ControllerActionType.ButtonB:
                    button = Xbox360Button.B;
                    hasButton = true;
                    break;
                case ControllerActionType.ButtonX:
                    button = Xbox360Button.X;
                    hasButton = true;
                    break;
                case ControllerActionType.ButtonY:
                    button = Xbox360Button.Y;
                    hasButton = true;
                    break;
                case ControllerActionType.LeftShoulder:
                    button = Xbox360Button.LeftShoulder;
                    hasButton = true;
                    break;
                case ControllerActionType.RightShoulder:
                    button = Xbox360Button.RightShoulder;
                    hasButton = true;
                    break;
                case ControllerActionType.DPadUp:
                    button = Xbox360Button.Up;
                    hasButton = true;
                    break;
                case ControllerActionType.DPadDown:
                    button = Xbox360Button.Down;
                    hasButton = true;
                    break;
                case ControllerActionType.DPadLeft:
                    button = Xbox360Button.Left;
                    hasButton = true;
                    break;
                case ControllerActionType.DPadRight:
                    button = Xbox360Button.Right;
                    hasButton = true;
                    break;
                case ControllerActionType.Start:
                    button = Xbox360Button.Start;
                    hasButton = true;
                    break;
                case ControllerActionType.Back:
                    button = Xbox360Button.Back;
                    hasButton = true;
                    break;
                case ControllerActionType.Guide:
                    button = Xbox360Button.Guide;
                    hasButton = true;
                    break;
            }

            if (hasButton)
            {
                _controller.SetButtonState(button, pressed);
                _controller.SubmitReport();
                Console.WriteLine($"[ViGEmController] Button {actionType} {(pressed ? "PRESSED" : "RELEASED")}");
            }
        }

        private void UpdateAxis(ControllerActionType actionType)
        {
            // Update sticks
            if (actionType == ControllerActionType.LeftStickUp || 
                actionType == ControllerActionType.LeftStickDown ||
                actionType == ControllerActionType.LeftStickLeft || 
                actionType == ControllerActionType.LeftStickRight)
            {
                UpdateLeftStick();
            }
            else if (actionType == ControllerActionType.RightStickUp || 
                     actionType == ControllerActionType.RightStickDown ||
                     actionType == ControllerActionType.RightStickLeft || 
                     actionType == ControllerActionType.RightStickRight)
            {
                UpdateRightStick();
            }
            else if (actionType == ControllerActionType.LeftTrigger)
            {
                byte triggerValue = (byte)(_axisStates[ControllerActionType.LeftTrigger] * 255);
                _controller.SetSliderValue(Xbox360Slider.LeftTrigger, triggerValue);
                _controller.SubmitReport();
                Console.WriteLine($"[ViGEmController] Left Trigger: {triggerValue}");
            }
            else if (actionType == ControllerActionType.RightTrigger)
            {
                byte triggerValue = (byte)(_axisStates[ControllerActionType.RightTrigger] * 255);
                _controller.SetSliderValue(Xbox360Slider.RightTrigger, triggerValue);
                _controller.SubmitReport();
                Console.WriteLine($"[ViGEmController] Right Trigger: {triggerValue}");
            }
        }

        private void UpdateLeftStick()
        {
            // Calculate X axis (left/right)
            short thumbX = 0;
            float leftValue = _axisStates.ContainsKey(ControllerActionType.LeftStickLeft) ? _axisStates[ControllerActionType.LeftStickLeft] : 0f;
            float rightValue = _axisStates.ContainsKey(ControllerActionType.LeftStickRight) ? _axisStates[ControllerActionType.LeftStickRight] : 0f;
            
            if (leftValue > 0) thumbX = (short)(-32767 * leftValue);
            if (rightValue > 0) thumbX = (short)(32767 * rightValue);

            // Calculate Y axis (up/down)
            short thumbY = 0;
            float upValue = _axisStates.ContainsKey(ControllerActionType.LeftStickUp) ? _axisStates[ControllerActionType.LeftStickUp] : 0f;
            float downValue = _axisStates.ContainsKey(ControllerActionType.LeftStickDown) ? _axisStates[ControllerActionType.LeftStickDown] : 0f;
            
            if (upValue > 0) thumbY = (short)(32767 * upValue);
            if (downValue > 0) thumbY = (short)(-32767 * downValue);

            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, thumbX);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, thumbY);
            _controller.SubmitReport();

            Console.WriteLine($"[ViGEmController] Left Stick: X={thumbX}, Y={thumbY}");
        }

        private void UpdateRightStick()
        {
            // Calculate X axis (left/right)
            short thumbX = 0;
            float leftValue = _axisStates.ContainsKey(ControllerActionType.RightStickLeft) ? _axisStates[ControllerActionType.RightStickLeft] : 0f;
            float rightValue = _axisStates.ContainsKey(ControllerActionType.RightStickRight) ? _axisStates[ControllerActionType.RightStickRight] : 0f;
            
            if (leftValue > 0) thumbX = (short)(-32767 * leftValue);
            if (rightValue > 0) thumbX = (short)(32767 * rightValue);

            // Calculate Y axis (up/down)
            short thumbY = 0;
            float upValue = _axisStates.ContainsKey(ControllerActionType.RightStickUp) ? _axisStates[ControllerActionType.RightStickUp] : 0f;
            float downValue = _axisStates.ContainsKey(ControllerActionType.RightStickDown) ? _axisStates[ControllerActionType.RightStickDown] : 0f;
            
            if (upValue > 0) thumbY = (short)(32767 * upValue);
            if (downValue > 0) thumbY = (short)(-32767 * downValue);

            _controller.SetAxisValue(Xbox360Axis.RightThumbX, thumbX);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY, thumbY);
            _controller.SubmitReport();

            Console.WriteLine($"[ViGEmController] Right Stick: X={thumbX}, Y={thumbY}");
        }

        /// <summary>
        /// Reset tất cả trạng thái controller
        /// </summary>
        public void ResetAll()
        {
            _axisStates.Clear();
            _buttonStates.Clear();

            // Reset all axes to center
            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);
            _controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
            _controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);

            // Reset all buttons
            foreach (Xbox360Button button in Enum.GetValues(typeof(Xbox360Button)))
            {
                _controller.SetButtonState(button, false);
            }

            _controller.SubmitReport();
            Console.WriteLine("[ViGEmController] Reset all controller states");
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
