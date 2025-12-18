# 🔥 Hot Reload Config - Hướng dẫn sử dụng

## ✅ Đã implement thành công!

App của bạn giờ **tự động reload config** khi file `keymapping.json` thay đổi - **KHÔNG CẦN BUILD LẠI!**

---

## 📋 Cách test Hot Reload

### 1. Chạy app
```powershell
# Chạy app bằng cách build và run
dotnet run --project WPFUI_NEW/WPFUI_NEW.csproj
```

Hoặc chạy trực tiếp file `.exe`:
```powershell
.\WPFUI_NEW\bin\Debug\net8.0-windows\WPFUI_NEW.exe
```

### 2. Kết nối CLIENT và HOST
- **Máy HOST**: Chọn "Host" → Start streaming
- **Máy CLIENT**: Chọn "Client" → Nhập IP của HOST → Connect

### 3. Sửa config **TRONG KHI APP ĐANG CHẠY**
Mở file `keymapping.json`, thay đổi mapping:

**VÍ DỤ - Đổi từ:**
```json
{
  "KeyboardMapping": {
    "W": "Z",
    "A": "B",
    "S": "G",
    "D": "H"
  }
}
```

**Thành:**
```json
{
  "KeyboardMapping": {
    "W": "Up",
    "A": "Left", 
    "S": "Down",
    "D": "Right"
  }
}
```

### 4. Save file (Ctrl+S)
App sẽ **TỰ ĐỘNG RELOAD** config trong vòng 100ms!

### 5. Test ngay lập tức
- Bấm phím W → Controller nhận phím `Up` (không phải `Z` nữa!)
- Bấm phím A → Controller nhận phím `Left`
- **KHÔNG CẦN RESTART APP!**

---

## 🔍 Cách biết config đã reload

Xem **Console Output** hoặc **Debug Output** (trong Visual Studio Code):

```
🔄 Config file changed, reloading: d:\PBL4\PBL4-test\keymapping.json
✓ Config loaded successfully!
🔄 [KeyboardManager] Config changed, reloading key mappings...
✓ [KeyboardManager] Reloaded 4 key mappings
  W → Up
  A → Left
  S → Down
  D → Right
```

---

## ⚙️ Cách hoạt động (Technical)

### FileSystemWatcher
- `ConfigHelper` tự động theo dõi file `keymapping.json`
- Khi file thay đổi → trigger event `OnConfigChanged`
- Event này được subscribe bởi:
  - `KeyboardManager` → Reload key mappings
  - `ViGEmManager` → Reload controller mappings

### Hot Reload Components
1. **ConfigHelper.cs** - FileSystemWatcher monitoring
2. **KeyboardSimulator.cs** - `UpdateKeyMapping()` method
3. **KeyboardManager.cs** - Subscribe to config changes
4. **ViGEmManager.cs** - Subscribe to config changes

---

## 📝 Lưu ý quan trọng

### ✅ Hot Reload hoạt động với:
- **Keyboard mapping** (W→Z, A→B, etc.)
- **Controller mapping** (I→LeftStickUp, J→LeftStickLeft, etc.)
- **Audio settings** (Bitrate, SampleRate, Channels)

### ⚠️ Hot Reload KHÔNG hoạt động với:
- Thay đổi code C# (.cs files) - cần build lại
- Thay đổi XAML UI - cần build lại (hoặc dùng `dotnet watch`)

### 🚫 Nếu reload KHÔNG hoạt động:
1. Kiểm tra file path đúng: `d:\PBL4\PBL4-test\keymapping.json`
2. Kiểm tra JSON syntax hợp lệ (không có dấu phẩy thừa)
3. Xem Console Output để tìm lỗi

---

## 🎯 Use Cases thực tế

### Testing nhiều key mappings
```json
// Test 1: WASD → Arrow keys
"W": "Up", "A": "Left", "S": "Down", "D": "Right"

// Save → Test ngay → Không thích?

// Test 2: WASD → TFGH  
"W": "T", "A": "F", "S": "G", "D": "H"

// Save → Test lại → Vẫn chưa ổn?

// Test 3: Custom mapping
"W": "Space", "A": "Shift", "S": "Ctrl", "D": "Enter"
```

**Không cần build/restart app giữa các lần test!**

### Team collaboration
- Member A test với mapping 1
- Member B test với mapping 2
- Mỗi người chỉ cần sửa `keymapping.json` và Save
- App tự động reload → tiết kiệm thời gian cực kỳ nhiều!

---

## 🐛 Troubleshooting

### "Config không reload sau khi Save"
- **Nguyên nhân**: File editor lock file (VS Code, Notepad++)
- **Giải pháp**: Đợi 1-2 giây, hoặc đóng file editor

### "JSON parse error"
- **Nguyên nhân**: Syntax lỗi (thiếu dấu ngoặc, dấu phẩy thừa)
- **Giải pháp**: Dùng JSON validator online, hoặc xem error log

### "Mapping không đổi"
- **Nguyên nhân**: Bạn đang ở mode sai (CLIENT/HOST)
- **Giải pháp**: 
  - Keyboard mapping: Chỉ hoạt động ở **HOST mode**
  - Controller mapping: Load khi parse packet (tự động)

---

## 🎉 Kết luận

**Hot Reload Config = Dev Experience tuyệt vời!**

- ✅ Không cần build lại app
- ✅ Không cần restart app  
- ✅ Test nhanh, iterate nhanh
- ✅ Team collaboration dễ dàng
- ✅ 100% không ảnh hưởng code (chỉ là extension riêng)

**Enjoy coding! 🚀**
