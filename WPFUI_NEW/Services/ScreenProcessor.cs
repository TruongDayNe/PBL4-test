using ScreenDublicator;
using System;
using System.Diagnostics;
using System.Threading;
using System.Drawing;
using MyScreenDublicator = ScreenDublicator.ScreenDublicator;

namespace WPFUI_NEW.Services
{
    public class ScreenProcessor : IDisposable
    {
        private int UDPATE_TIMEOUT = 50;
        private static ScreenProcessor _instance = null;
        private MyScreenDublicator _desktopDublicator = null;
        private Image _currentScreenImage = null;
        // Lưu trữ vùng thay đổi của frame hiện tại
        private Rectangle _currentDirtyRect = Rectangle.Empty;
        private Point _currentCursorPosition = Point.Empty;
        private ReaderWriterLockSlim _rwLocker = new ReaderWriterLockSlim();
        private bool _isDisposed = false;
        private ManualResetEventSlim _firstFrameReady = new ManualResetEventSlim(false);

        public static ScreenProcessor Instance
        {
            get
            {
                if (_instance == null) _instance = new ScreenProcessor();
                return _instance;
            }
        }

        // Cập nhật Action để trả về thêm Rectangle
        public void ProcessScreenImage(Action<Image, Rectangle> processingAction)
        {
            if (_isDisposed || this._rwLocker == null) return;

            try
            {
                _firstFrameReady.Wait();
                this._rwLocker.EnterReadLock();
                if (this._currentScreenImage != null)
                {
                    // Truyền ảnh gốc và dirty rect cho logic xử lý
                    processingAction(this._currentScreenImage, this._currentDirtyRect);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"ScreenProcessor Read Error: {ex.Message}"); }
            finally
            {
                if (this._rwLocker != null && this._rwLocker.IsReadLockHeld)
                    this._rwLocker.ExitReadLock();
            }
        }

        public Point CurrentCursorPosition { /* Giữ nguyên */ get; }

        public void Dispose() { /* Giữ nguyên logic dispose */ _isDisposed = true; _desktopDublicator?.Dispose(); _rwLocker?.Dispose(); _currentScreenImage?.Dispose(); }

        public void Start()
        {
            if (_isDisposed) throw new ScreenProcessorException("Object disposed");
            new Thread(this.UpdateFrame) { IsBackground = true, Name = "Update Frame Thread" }.Start();
        }

        private ScreenProcessor() { this._desktopDublicator = new MyScreenDublicator(); }

        private void UpdateFrame()
        {
            var stopwatch = new Stopwatch();
            while (true)
            {
                stopwatch.Restart();
                ScreenInfo screenInfo = null;
                try
                {
                    screenInfo = this._desktopDublicator.GetScreenInformation();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Capture Error: {ex.Message}");
                    continue;
                }

                try
                {
                    this._rwLocker.EnterWriteLock();

                    if (this._currentScreenImage != null) this._currentScreenImage.Dispose();
                    this._currentScreenImage = screenInfo.ScreenImage;

                    // --- TÍNH TOÁN DIRTY RECT ---
                    if (screenInfo.UpdatedRegions != null && screenInfo.UpdatedRegions.Length > 0)
                    {
                        // Hợp nhất tất cả các vùng thay đổi thành 1 hình chữ nhật bao quanh
                        Rectangle unionRect = screenInfo.UpdatedRegions[0];
                        for (int i = 1; i < screenInfo.UpdatedRegions.Length; i++)
                        {
                            unionRect = Rectangle.Union(unionRect, screenInfo.UpdatedRegions[i]);
                        }
                        this._currentDirtyRect = unionRect;
                    }
                    else
                    {
                        this._currentDirtyRect = Rectangle.Empty;
                    }

                    if (screenInfo.PointerInfo != null)
                    {
                        this._currentCursorPosition = new Point(screenInfo.PointerInfo.Position.X, screenInfo.PointerInfo.Position.Y);
                    }
                    _firstFrameReady.Set();
                }
                finally { this._rwLocker.ExitWriteLock(); }

                stopwatch.Stop();
                int timeToWait = this.UDPATE_TIMEOUT - (int)stopwatch.ElapsedMilliseconds;
                if (timeToWait > 0) Thread.Sleep(timeToWait);
            }
        }
    }
}