using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WPFUI_NEW.ViewModels;

namespace WPFUI_NEW.Views
{
    public partial class HostOverlayWindow : Window
    {
        // ID cho các Hotkey
        private const int HOTKEY_TOGGLE_ID = 9000;
        private const int HOTKEY_ACCEPT_ID = 9001;
        private const int HOTKEY_REJECT_ID = 9002;

        // Modifiers & Keys
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NONE = 0x0000;

        private const uint VK_H = 0x48;
        private const uint VK_F1 = 0x70; // Mã phím F1
        private const uint VK_F2 = 0x71; // Mã phím F2

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HostOverlayWindow()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
            this.Closed += OnClosed;

            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = 0;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            var handle = helper.Handle;

            // Đăng ký Ctrl + H (Ẩn/Hiện)
            RegisterHotKey(handle, HOTKEY_TOGGLE_ID, MOD_CONTROL, VK_H);

            // Đăng ký F1 (Chấp nhận) - Không cần modifier (như yêu cầu)
            RegisterHotKey(handle, HOTKEY_ACCEPT_ID, MOD_NONE, VK_F1);

            // Đăng ký F2 (Từ chối)
            RegisterHotKey(handle, HOTKEY_REJECT_ID, MOD_NONE, VK_F2);

            ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_TOGGLE_ID);
            UnregisterHotKey(helper.Handle, HOTKEY_ACCEPT_ID);
            UnregisterHotKey(helper.Handle, HOTKEY_REJECT_ID);
            ComponentDispatcher.ThreadPreprocessMessage -= ComponentDispatcher_ThreadPreprocessMessage;
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg.message == WM_HOTKEY)
            {
                int id = msg.wParam.ToInt32();

                if (DataContext is HostViewModel vm)
                {
                    if (id == HOTKEY_TOGGLE_ID)
                    {
                        vm.ToggleOverlayCommand.Execute(null);
                        handled = true;
                    }
                    else if (id == HOTKEY_ACCEPT_ID)
                    {
                        // Gọi lệnh Accept Latest nếu có thể
                        if (vm.AcceptLatestRequestCommand.CanExecute(null))
                        {
                            vm.AcceptLatestRequestCommand.Execute(null);
                            handled = true;
                        }
                    }
                    else if (id == HOTKEY_REJECT_ID)
                    {
                        // Gọi lệnh Reject Latest nếu có thể
                        if (vm.RejectLatestRequestCommand.CanExecute(null))
                        {
                            vm.RejectLatestRequestCommand.Execute(null);
                            handled = true;
                        }
                    }
                }
            }
        }

        private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }
    }
}