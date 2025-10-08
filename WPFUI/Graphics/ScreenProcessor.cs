using ScreenDublicator;
using System;
using System.Diagnostics;
using System.Threading;
using System.Drawing;

using MyScreenDublicator = ScreenDublicator.ScreenDublicator;

namespace WPFUI.Graphics
{
    /// <summary>
    /// Lớp nhận và lưu trữ thông tin về trạng thái màn hình desktop.
    /// Làm việc trong luồng riêng, có thể xử lý các yêu cầu
    /// trong môi trường đa luồng
    /// </summary>
    public class ScreenProcessor : IDisposable
    {
        #region CONST

        private int UDPATE_TIMEOUT = 50;

        #endregion

        #region FIELDS

        private static ScreenProcessor _instance = null;                    //Singleton
        private MyScreenDublicator _desktopDublicator = null;               //Bộ nhân bản màn hình desktop (sử dụng DirectX)
        private Image _currentScreenImage = null;                           //Hình ảnh màn hình desktop hiện tại
        private Point _currentCursorPosition = Point.Empty;                 //Vị trí con trỏ hiện tại
        private ReaderWriterLockSlim _rwLocker = new ReaderWriterLockSlim();  //Đồng bộ truy cập tài nguyên
        private bool _isDisposed = false;                                   //Đánh dấu đã bị hủy

        // Thêm một cờ để báo hiệu khi khung hình đầu tiên đã sẵn sàng
        private ManualResetEventSlim _firstFrameReady = new ManualResetEventSlim(false);

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Lấy thể hiện của đối tượng (Singleton).
        /// Not thread-safe
        /// </summary>
        public static ScreenProcessor Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ScreenProcessor();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Hình ảnh màn hình desktop hiện tại
        /// </summary>
        public Image CurrentScreenImage
        {
            get
            {
                try
                {
                    // Chờ đợi cho đến khi khung hình đầu tiên được chụp
                    _firstFrameReady.Wait();

                    this._rwLocker.EnterReadLock();
                    // Tạo một bản sao để tránh vấn đề "tham chiếu vs copy"
                    return new Bitmap(this._currentScreenImage);
                }
                catch (Exception ex)
                {
                    string messge = String.Format("Không thể trả về hình ảnh\n{0}\n{1}", ex.Message, ex.StackTrace);
                    Debug.WriteLine(messge);
                    return null;
                }
                finally
                {
                    this._rwLocker.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Vị trí con trỏ hiện tại
        /// </summary>
        public Point CurrentCursorPosition
        {
            get
            {
                Debug.WriteLine("Yêu cầu lấy tọa độ con trỏ");
                try
                {
                    this._rwLocker.EnterReadLock();
                    return this._currentCursorPosition;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Không thể lấy tọa độ con trỏ");
                    Debug.WriteLine(ex.Message);
                    return Point.Empty;
                }
                finally
                {
                    this._rwLocker.ExitReadLock();
                }
            }
        }

        #endregion

        #region PUBLIC METHODS

        public void Dispose()
        {
            if (this._isDisposed)
            {
                return;
            }

            if (this._desktopDublicator != null)
            {
                this._desktopDublicator.Dispose();
            }

            if (this._rwLocker != null)
            {
                this._rwLocker.Dispose();
                this._rwLocker = null;
            }

            if (this._currentScreenImage != null)
            {
                this._currentScreenImage.Dispose();
            }
            this._isDisposed = true;

            Debug.WriteLine("ScreenProcessor đã được hủy");
        }

        /// <summary>
        /// Khởi động luồng cập nhật dữ liệu trạng thái màn hình desktop
        /// </summary>
        public void Start()
        {
            if (this._isDisposed)
            {
                string message = String.Format("Không thể khởi động luồng cập nhật khung hình. Đối tượng đã bị hủy");
                Debug.WriteLine(message);
                throw new ScreenProcessorException(message);
            }
            try
            {
                Debug.WriteLine("Đang khởi động luồng cập nhật khung hình");
                new Thread(this.UpdateFrame) { IsBackground = true, Name = "Update Frame Thread" }.Start();
                Debug.WriteLine("Luồng cập nhật khung hình đã được khởi động");
            }
            catch (Exception ex)
            {
                string message = String.Format("Lỗi khi khởi động luồng cập nhật khung hình\n{0}\n{1}", ex.Message, ex.StackTrace);
                Debug.WriteLine(message);
                throw new ScreenProcessorException(message);
            }
        }

        #endregion

        #region PRIVATE METHODS

        private ScreenProcessor()
        {
            this._desktopDublicator = new MyScreenDublicator();
        }

        /// <summary>
        /// Cập nhật trạng thái màn hình desktop. Thực hiện trong luồng làm việc.
        /// </summary>
        private void UpdateFrame()
        {
            while (true)
            {
                try
                {
                    ScreenInfo screenInfo = this._desktopDublicator.GetScreenInformation();
                    this._rwLocker.EnterWriteLock();
                    if (this._currentScreenImage != null)
                    {
                        this._currentScreenImage.Dispose();
                    }
                    this._currentScreenImage = screenInfo.ScreenImage;

                    // Báo hiệu rằng khung hình đầu tiên đã sẵn sàng
                    _firstFrameReady.Set();

                }
                catch (ScreenProcessorException sdex)
                {
                    Debug.WriteLine("Không thể cập nhật thông tin khung hình (DesktopDuplicationException)\n{0}\n{1}", sdex.Message, sdex.StackTrace);
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Không thể cập nhật thông tin khung hình (Exception)\n{0}\n{1}", ex.Message, ex.StackTrace);
                    break;
                }
                finally
                {
                    this._rwLocker.ExitWriteLock();
                }
                Thread.Sleep(this.UDPATE_TIMEOUT);  //to-do
            }
            Debug.WriteLine("Luồng cập nhật khung hình đã kết thúc");
        }

        #endregion
    }
}
