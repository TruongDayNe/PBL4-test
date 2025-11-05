# 🚀 Hướng dẫn Setup Project PBL4

## 📋 Yêu cầu hệ thống

### **Cho máy HOST (máy chạy game/stream):**
1. **ViGEmBus Driver** (BẮT BUỘC)
   - Download: https://github.com/nefarius/ViGEmBus/releases/latest
   - Tải file: `ViGEmBus_Setup_x64.exe`
   - Chạy file cài đặt với quyền Administrator
   - Restart máy sau khi cài

2. **.NET Framework 4.8**
   - Thường có sẵn trên Windows 10/11
   - Download: https://dotnet.microsoft.com/download/dotnet-framework/net48

3. **.NET 8.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Chọn: "SDK x64" cho Windows

### **Cho máy CLIENT (máy điều khiển):**
1. **.NET Framework 4.8**
2. **.NET 8.0 SDK**

**❌ KHÔNG CẦN** cài ViGEmBus driver trên máy CLIENT!

## 🔧 Cài đặt Project

### 1. Clone repository:
```bash
git clone <repository-url>
cd PBL4-test
git checkout feature-all
```

### 2. Restore NuGet packages:
```bash
dotnet restore
```

### 3. Build project:
```bash
dotnet build
```

Hoặc build trong Visual Studio 2022:
- Mở file `RealTimeUdpStream.sln`
- Build > Build Solution (Ctrl+Shift+B)

## ▶️ Chạy ứng dụng

### Chạy HOST (máy stream/game):
1. Chạy `WPFUI_NEW.exe`
2. Click **"Bắt đầu Host"**
3. Đợi thông báo: "Đang stream..."

### Chạy CLIENT (máy điều khiển):
1. Chạy `WPFUI_NEW.exe` 
2. Nhập IP của HOST (vd: `192.168.1.100` hoặc `127.0.0.1` nếu cùng máy)
3. Click **"Kết nối"**

## 🎮 Tính năng

### Keyboard Mapping:
- **CLIENT ấn WASD** → **HOST nhận TFGH**
  - W → T (lên)
  - A → F (trái)
  - S → G (xuống)
  - D → H (phải)

### Controller Simulation (ViGEm):
- **CLIENT ấn IJKL** → **HOST controller joystick di chuyển**
  - I → Joystick UP (lên)
  - J → Joystick LEFT (trái)
  - K → Joystick DOWN (xuống)
  - L → Joystick RIGHT (phải)

### Audio & Video:
- HOST stream màn hình + audio system → CLIENT
- CLIENT nhận và hiển thị real-time

## ⚠️ Xử lý lỗi thường gặp

### ❌ "Could not load assembly Nefarius.ViGEm.Client"
**Nguyên nhân:** Chưa cài ViGEmBus driver trên HOST

**Giải pháp:**
1. Download ViGEmBus driver
2. Cài với quyền Administrator
3. Restart máy

### ❌ "Xbox 360 controller ao da duoc tao va ket noi" không xuất hiện
**Nguyên nhân:** ViGEmBus service chưa chạy

**Giải pháp:**
1. Mở Services (Win+R → `services.msc`)
2. Tìm "ViGEmBus"
3. Start service và set Startup type = Automatic

### ❌ Controller không di chuyển trong game
**Kiểm tra:**
1. Mở `joy.cpl` (Game Controllers) → Phải thấy "Xbox 360 Controller"
2. Test controller tại: https://gamepad-tester.com/
3. Kiểm tra console log xem có "Joystick cap nhat" không

### ❌ Port 12000 hoặc 12001 đã được sử dụng
**Giải pháp:**
1. Đóng tất cả ứng dụng đang dùng port đó
2. Hoặc kill process: `netstat -ano | findstr :12000`

## 🔍 Debug

### Xem console output:
- Chạy từ Visual Studio để thấy Debug.WriteLine
- Hoặc chạy từ terminal: `dotnet run --project WPFUI_NEW`

### Kiểm tra network:
```bash
# Kiểm tra port đang mở
netstat -an | findstr "12000 12001"

# Test ping
ping <HOST_IP>

# Disable Firewall tạm thời để test
```

## 📦 NuGet Packages quan trọng

- `Nefarius.ViGEm.Client` v1.21.256 - Controller simulation
- `NAudio` v2.2.1 - Audio capture/playback
- `SharpDX` v4.2.0 - Screen capture
- `CommunityToolkit.Mvvm` - MVVM framework

## 🤝 Contributors

- [Tên thành viên 1]
- [Tên thành viên 2]
- ...

## 📝 License

[Thêm license nếu có]
