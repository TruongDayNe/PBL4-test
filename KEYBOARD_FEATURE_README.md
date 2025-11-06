# HƯỚNG DẪN TÍNH NĂNG TRUYỀN PHÍM QUA MẠNG

## 📋 TỔNG QUAN

Tính năng cho phép truyền keyboard input từ **CLIENT** → **HOST**:
- **CLIENT**: Nhấn phím WASD (và các phím khác)
- **HOST**: Nhận và giả lập thành YGHJ (theo key mapping)

## 🎯 HOẠT ĐỘNG

### Kiến trúc:
```
CLIENT (Capture)          →  NETWORK (UDP)  →  HOST (Simulate)
  Nhấn: W, A, S, D                               Giả lập: Y, G, H, J
  Capture từ toàn máy                            Simulate toàn máy
```

### Key Mapping mặc định:
```
CLIENT → HOST
  W    →   Y
  A    →   G  
  S    →   H
  D    →   J
  Space → Space
  Shift → Shift
  Ctrl  → Ctrl
```

## ⚙️ CÀI ĐẶT TỰ ĐỘNG

Tính năng **tự động bật** khi:

### 1️⃣ BÊN HOST:
```csharp
// Khi Host bắt đầu stream:
- Tạo KeyboardManager (capture mode)
- Chờ Client kết nối...

// Khi Client kết nối:
- SetTargetEndPoint(clientEndPoint)
- StartCapture() → Bắt đầu capture phím từ CLIENT
```

### 2️⃣ BÊN CLIENT:
```csharp
// Khi Client kết nối:
- Tạo KeyboardManager (simulate mode)
- StartSimulation() → Nhận phím và giả lập
```

## 🔧 CÁC THÀNH PHẦN

### 1. KeyboardCapture.cs
- Capture phím từ toàn hệ thống (CLIENT)
- Sử dụng `GetAsyncKeyState` Windows API
- Poll mỗi 10ms để responsive
- Phát hiện KeyDown và KeyUp

### 2. KeyboardSimulator.cs
- Giả lập phím vào toàn hệ thống (HOST)
- Sử dụng `SendInput` Windows API
- Hỗ trợ key mapping tùy chỉnh
- Giả lập chính xác KeyDown và KeyUp

### 3. KeyboardManager.cs
- Quản lý capture và simulation
- Xử lý network packet (type 0x16)
- Serialize/Deserialize KeyEvent
- Tự động cleanup

## 📦 PACKET FORMAT

```
UdpPacket Type: 0x16 (KEYBOARD_PACKET_TYPE)
Payload: [Key: 1 byte][Action: 1 byte][Reserved: 1 byte]
```

## 🚀 SỬ DỤNG

### Chạy Host:
1. Mở app
2. Click "Bắt đầu Host"
3. Đợi Client kết nối
4. ✅ Keyboard capture tự động bật

### Chạy Client:
1. Mở app
2. Nhập IP Host
3. Click "Kết nối"
4. ✅ Keyboard simulation tự động bật
5. Nhấn WASD → Host nhận YGHJ

## ⚡ LƯU Ý

### Quyền Admin:
- **KHÔNG CẦN** admin cho capture (GetAsyncKeyState)
- **CẦN** admin nếu giả lập vào app có UAC
- Game có anti-cheat có thể chặn

### Performance:
- Poll interval: 10ms (responsive)
- Packet size: 3 bytes (rất nhỏ)
- Độ trễ: < 20ms (LAN)

### Key Mapping:
Để thay đổi key mapping, sửa trong `KeyboardSimulator.cs`:
```csharp
_keyMapping = new Dictionary<VirtualKey, VirtualKey>
{
    { VirtualKey.W, VirtualKey.Y },  // Đổi Y thành phím khác
    { VirtualKey.A, VirtualKey.G },  // Đổi G thành phím khác
    // ... thêm mapping mới
};
```

## 🐛 DEBUG

### Kiểm tra logs:
```
[KeyboardCapture] W DOWN
[KeyboardManager] Sending W DOWN
[KeyboardManager] Received W DOWN  
[KeyboardSimulator] Simulated Y DOWN
```

### Test:
1. Mở Notepad trên HOST
2. Nhấn W trên CLIENT
3. Kiểm tra HOST có xuất hiện "Y" không

## 🔐 BẢO MẬT

⚠️ **Cảnh báo**: 
- Tính năng này có thể bị lạm dụng
- Chỉ dùng trong mạng LAN tin cậy
- Không mã hóa packet (có thể thêm sau)

## 📝 ROADMAP

- [ ] Thêm mã hóa packet
- [ ] Hỗ trợ chuột
- [ ] UI để thay đổi key mapping
- [ ] Lưu key mapping vào config file
- [ ] Thêm whitelist/blacklist phím
- [ ] Hỗ trợ macro/combo phím

---

**Tác giả**: PBL4 Team  
**Ngày tạo**: 2025-11-03  
**Version**: 1.0.0
