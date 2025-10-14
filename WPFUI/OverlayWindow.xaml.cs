using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace WPFUI
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
        }

        #region Win32 API

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_Q = 0x51; // Mã phím 'Q'

        private HwndSource _source;
        private IntPtr _hwnd;

        #endregion

        protected override async void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _hwnd = helper.Handle;
            _source = HwndSource.FromHwnd(_hwnd);
            _source.AddHook(HwndHook);

            // Đăng ký phím nóng toàn cục (Ctrl + Alt + Q)
            if (!RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_Q))
            {
                MessageBox.Show("Không thể đăng ký phím nóng Ctrl+Alt+Q.");
            }

            // Bắt đầu quy trình hiển thị
            await ShowInitialPopupAsync();
        }

        /// <summary>
        /// Hiển thị pop-up ban đầu trong 5 giây, sau đó chuyển sang overlay đơn giản.
        /// </summary>
        private async Task ShowInitialPopupAsync()
        {
            // Bật click-through để không làm phiền người dùng
            EnableClickThrough();

            // Hiển thị pop-up
            InitialPopup.Visibility = Visibility.Visible;

            // Chờ 5 giây
            await Task.Delay(5000);

            // Ẩn pop-up và hiện overlay đơn giản
            InitialPopup.Visibility = Visibility.Collapsed;
            SimpleOverlay.Visibility = Visibility.Visible;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleFocusOverlay();
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Bật/tắt chế độ xem chi tiết.
        /// </summary>
        private void ToggleFocusOverlay()
        {
            if (FocusOverlay.IsVisible)
            {
                // TẮT chế độ chi tiết
                FocusOverlay.Visibility = Visibility.Collapsed;
                SimpleOverlay.Visibility = Visibility.Visible; // Hiện lại overlay đơn giản
                EnableClickThrough();
            }
            else
            {
                // BẬT chế độ chi tiết
                SimpleOverlay.Visibility = Visibility.Collapsed; // Ẩn overlay đơn giản đi
                FocusOverlay.Visibility = Visibility.Visible;
                DisableClickThrough(); // Tắt click-through để người dùng có thể focus vào cửa sổ (nếu cần)
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _source?.RemoveHook(HwndHook);
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            base.OnClosing(e);
        }

        private void EnableClickThrough()
        {
            int extendedStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        private void DisableClickThrough()
        {
            int extendedStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
    }
}