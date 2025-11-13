# Key Mapping Configuration Guide

## 📋 Giới thiệu

File `keymapping.json` cho phép bạn tùy chỉnh mapping phím từ CLIENT sang HOST mà không cần sửa code.

## 📂 Vị trí file

File config mặc định: `keymapping.json` (cùng thư mục với .exe)

## 🎮 Cấu trúc file

### 1. **KeyboardMapping** - Mapping phím bàn phím

Map phím từ CLIENT sang HOST (keyboard simulation).

```json
"KeyboardMapping": {
  "W": "T",    // Phím W trên CLIENT → Phím T trên HOST
  "A": "F",    // Phím A trên CLIENT → Phím F trên HOST
  "S": "G",
  "D": "H"
}
```

**Các phím hỗ trợ:**
- Chữ cái: `A-Z`
- Số: `0-9`
- Function: `F1-F12`
- Arrow keys: `Up`, `Down`, `Left`, `Right`
- Special: `Space`, `Enter`, `Escape`, `Tab`, `Shift`, `Ctrl`, `Alt`

**Ví dụ thêm mapping:**
```json
"KeyboardMapping": {
  "W": "T",
  "A": "F", 
  "S": "G",
  "D": "H",
  "Space": "Enter",      // Space → Enter
  "Q": "1",              // Q → số 1
  "E": "2"               // E → số 2
}
```

---

### 2. **ControllerMapping** - Mapping sang Xbox Controller

Map phím từ CLIENT sang Xbox 360 Controller ảo trên HOST.

```json
"ControllerMapping": {
  "I": {
    "Type": "LeftStickUp",    // Loại action
    "Value": 1.0               // Giá trị (0.0 - 1.0)
  },
  "O": {
    "Type": "ButtonA",
    "Value": 1.0
  }
}
```

**Các Controller Actions hỗ trợ:**

#### 🕹️ **Left Stick (Analog)**
- `LeftStickUp` - Di chuyển stick lên
- `LeftStickDown` - Di chuyển stick xuống
- `LeftStickLeft` - Di chuyển stick trái
- `LeftStickRight` - Di chuyển stick phải

#### 🕹️ **Right Stick (Analog)**
- `RightStickUp`
- `RightStickDown`
- `RightStickLeft`
- `RightStickRight`

#### 🎮 **Buttons**
- `ButtonA` - Nút A (Xbox: xanh lá)
- `ButtonB` - Nút B (Xbox: đỏ)
- `ButtonX` - Nút X (Xbox: xanh dương)
- `ButtonY` - Nút Y (Xbox: vàng)

#### 🎯 **Shoulders & Triggers**
- `LeftShoulder` - LB
- `RightShoulder` - RB
- `LeftTrigger` - LT (analog)
- `RightTrigger` - RT (analog)

#### ⬆️ **D-Pad**
- `DPadUp`
- `DPadDown`
- `DPadLeft`
- `DPadRight`

#### ⚙️ **Special Buttons**
- `Start` - Nút Start
- `Back` - Nút Back/Select
- `Guide` - Nút Xbox/Home

**Value:**
- `1.0` = Full press (100%)
- `0.5` = Half press (50%) - hữu ích cho trigger/stick
- `0.0` = No press

**Ví dụ controller mapping phức tạp:**
```json
"ControllerMapping": {
  "I": { "Type": "LeftStickUp", "Value": 1.0 },
  "K": { "Type": "LeftStickDown", "Value": 1.0 },
  "J": { "Type": "LeftStickLeft", "Value": 1.0 },
  "L": { "Type": "LeftStickRight", "Value": 1.0 },
  
  "O": { "Type": "ButtonA", "Value": 1.0 },
  "P": { "Type": "ButtonB", "Value": 1.0 },
  "U": { "Type": "ButtonX", "Value": 1.0 },
  "Y": { "Type": "ButtonY", "Value": 1.0 },
  
  "Q": { "Type": "LeftShoulder", "Value": 1.0 },
  "E": { "Type": "RightShoulder", "Value": 1.0 },
  "Z": { "Type": "LeftTrigger", "Value": 0.8 },
  "C": { "Type": "RightTrigger", "Value": 0.8 },
  
  "Up": { "Type": "DPadUp", "Value": 1.0 },
  "Down": { "Type": "DPadDown", "Value": 1.0 },
  "Left": { "Type": "DPadLeft", "Value": 1.0 },
  "Right": { "Type": "DPadRight", "Value": 1.0 }
}
```

---

### 3. **AudioSettings** - Cấu hình Audio

```json
"AudioSettings": {
  "Codec": "OPUS",      // PCM16 hoặc OPUS
  "Bitrate": 96000,     // Bitrate cho OPUS (bps)
  "SampleRate": 48000,  // 48000 Hz (khuyến nghị)
  "Channels": 2         // 2 = Stereo, 1 = Mono
}
```

**Codec options:**
- `"PCM16"` - Không nén, chất lượng cao, băng thông cao (~1536 Kbps)
- `"OPUS"` - Nén cao, chất lượng tốt, băng thông thấp (~64-128 Kbps)

**OPUS Bitrate recommendations:**
- `64000` (64 Kbps) - Chất lượng tốt, băng thông thấp
- `96000` (96 Kbps) - **Khuyến nghị** - Cân bằng tốt
- `128000` (128 Kbps) - Chất lượng cao, băng thông cao hơn

---

## 💡 Ví dụ Config hoàn chỉnh

### Config cho game FPS (WASD + IJKL joystick + OUPY buttons):
```json
{
  "KeyboardMapping": {
    "W": "T",
    "A": "F",
    "S": "G",
    "D": "H",
    "Shift": "Ctrl",
    "Space": "V"
  },
  "ControllerMapping": {
    "I": { "Type": "LeftStickUp", "Value": 1.0 },
    "K": { "Type": "LeftStickDown", "Value": 1.0 },
    "J": { "Type": "LeftStickLeft", "Value": 1.0 },
    "L": { "Type": "LeftStickRight", "Value": 1.0 },
    "O": { "Type": "ButtonA", "Value": 1.0 },
    "P": { "Type": "ButtonB", "Value": 1.0 },
    "U": { "Type": "ButtonX", "Value": 1.0 },
    "Y": { "Type": "ButtonY", "Value": 1.0 }
  },
  "AudioSettings": {
    "Codec": "OPUS",
    "Bitrate": 96000,
    "SampleRate": 48000,
    "Channels": 2
  }
}
```

### Config cho racing game (Arrow keys + triggers):
```json
{
  "KeyboardMapping": {},
  "ControllerMapping": {
    "W": { "Type": "RightTrigger", "Value": 1.0 },
    "S": { "Type": "LeftTrigger", "Value": 1.0 },
    "A": { "Type": "LeftStickLeft", "Value": 0.8 },
    "D": { "Type": "LeftStickRight", "Value": 0.8 },
    "Space": { "Type": "ButtonA", "Value": 1.0 },
    "Shift": { "Type": "ButtonX", "Value": 1.0 }
  },
  "AudioSettings": {
    "Codec": "OPUS",
    "Bitrate": 64000,
    "SampleRate": 48000,
    "Channels": 2
  }
}
```

---

## 🚀 Sử dụng trong code

### Load config:
```csharp
// Load từ file mặc định (keymapping.json)
var config = KeyMappingConfig.LoadFromFile(KeyMappingConfig.GetDefaultConfigPath());

// Hoặc load từ file tùy chỉnh
var config = KeyMappingConfig.LoadFromFile(@"D:\myconfig.json");
```

### Tạo và lưu config mới:
```csharp
var config = KeyMappingConfig.CreateDefault();
config.SaveToFile("keymapping.json");
```

### Validate config:
```csharp
if (!config.Validate())
{
    Console.WriteLine("Config không hợp lệ!");
}
```

### In config ra console:
```csharp
Console.WriteLine(config.ToReadableString());
```

---

## ⚠️ Lưu ý

1. **File phải là JSON hợp lệ** - Sử dụng JSON validator nếu cần
2. **Phím phải viết ĐÚNG** - Phân biệt chữ hoa/thường (`"W"` ≠ `"w"`)
3. **Controller Actions phải đúng tên** - Xem danh sách bên trên
4. **Value phải từ 0.0 đến 1.0** - Ngoài range sẽ bị clamp
5. **Audio Codec:** `PCM16` hoặc `OPUS` (khuyến nghị OPUS)
6. **Nếu file không tồn tại** - Sẽ tự động tạo config mặc định

---

## 🐛 Troubleshooting

**Config không load được?**
- Check JSON syntax tại: https://jsonlint.com
- Xem console log để biết lỗi cụ thể

**Phím không hoạt động?**
- Check tên phím có đúng không
- Xem log console để debug

**Controller không nhận mapping?**
- Đảm bảo ViGEmBus driver đã cài đặt
- Check ControllerActionType có đúng không

---

## 📝 Template trống

```json
{
  "KeyboardMapping": {},
  "ControllerMapping": {},
  "AudioSettings": {
    "Codec": "OPUS",
    "Bitrate": 96000,
    "SampleRate": 48000,
    "Channels": 2
  }
}
```

Copy template này và thêm mapping theo nhu cầu của bạn! 🎮
