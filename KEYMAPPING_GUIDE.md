# Hướng Dẫn Cấu Hình Phím (Key Mapping Guide)

## Cách Sử Dụng File `keymapping.json`

### 1. Keyboard Mapping (Ánh xạ phím bàn phím)

**Cấu trúc:**
```json
"KeyboardMapping": {
  "PhímNguồn": "PhímĐích",
  "W": "Z",           // Ấn W trên CLIENT → Giả lập Z trên HOST
  "A": "B",           // Ấn A trên CLIENT → Giả lập B trên HOST
  "Space": ""         // Để trống = không ánh xạ (phím gốc)
}
```

**Danh sách phím có thể dùng:**

- **Chữ cái:** A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z
- **Số:** D0, D1, D2, D3, D4, D5, D6, D7, D8, D9 (0-9)
- **Function:** F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
- **Mũi tên:** Up, Down, Left, Right
- **Control:** Enter, Space, Tab, Back (Backspace), Escape, Shift, Ctrl, Alt
- **Navigation:** Home, End, PageUp, PageDown, Insert, Delete
- **Special:** CapsLock, NumLock, ScrollLock, PrintScreen, Apps
- **Numpad:** NumPad0-NumPad9, Add, Subtract, Multiply, Divide, Decimal
- **Symbols:** OemSemicolon (;:), OemPlus (=+), OemComma (,<), OemMinus (-_), OemPeriod (.>), OemQuestion (/?), OemTilde (`~), OemOpenBrackets ([{), OemPipe (\\|), OemCloseBrackets (]}), OemQuotes ('")

### 2. Controller Mapping (Ánh xạ phím → tay cầm Xbox)

**Cấu trúc:**
```json
"ControllerMapping": {
  "I": {
    "Type": "LeftStickUp",
    "Value": 1.0
  },
  "Q": {
    "Type": "",           // Để trống = không ánh xạ
    "Value": 1.0
  }
}
```

**Danh sách Controller Actions:**

- **Left Stick:** LeftStickUp, LeftStickDown, LeftStickLeft, LeftStickRight
- **Right Stick:** RightStickUp, RightStickDown, RightStickLeft, RightStickRight
- **Buttons:** ButtonA, ButtonB, ButtonX, ButtonY
- **Shoulders:** LeftShoulder (LB), RightShoulder (RB)
- **Triggers:** LeftTrigger (LT), RightTrigger (RT)
- **D-Pad:** DPadUp, DPadDown, DPadLeft, DPadRight
- **Special:** Start, Back, Guide

**Value:** Giá trị từ 0.0 đến 1.0 (analog input strength). Mặc định = 1.0 (full press)

### 3. Audio Settings

```json
"AudioSettings": {
  "Codec": "OPUS",      // OPUS hoặc PCM16
  "Bitrate": 96000,     // 64000, 96000, 128000 (chỉ dành cho OPUS)
  "SampleRate": 48000,  // 48000 Hz (khuyến nghị)
  "Channels": 2         // 1 (Mono) hoặc 2 (Stereo)
}
```

## Ví Dụ Thực Tế

### Game WASD → TFGH
```json
"KeyboardMapping": {
  "W": "T",
  "A": "F",
  "S": "G",
  "D": "H"
}
```

### Game WASD → Mũi tên
```json
"KeyboardMapping": {
  "W": "Up",
  "A": "Left",
  "S": "Down",
  "D": "Right"
}
```

### Full Controller Setup
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
  "R": { "Type": "LeftTrigger", "Value": 1.0 },
  "F": { "Type": "RightTrigger", "Value": 1.0 },
  "Enter": { "Type": "Start", "Value": 1.0 },
  "Escape": { "Type": "Back", "Value": 1.0 }
}
```

## Lưu Ý Quan Trọng

1. **Không cần build lại:** Sau khi sửa file `keymapping.json`, chỉ cần **restart ứng dụng** (không cần build lại code)
2. **Phím để trống:** Nếu để value = `""` (empty string), phím sẽ không được ánh xạ
3. **Case-sensitive:** Tên phím phải viết đúng chữ hoa/thường (VD: `W` không phải `w`)
4. **File location:** File config nằm ở `WPFUI_NEW\bin\Debug\net8.0-windows\keymapping.json`
5. **Kiểm tra log:** Khi chạy app, xem console log để verify config đã load đúng

## Troubleshooting

### Config không áp dụng?
- Kiểm tra file có đúng trong thư mục output không
- Xem console log có lỗi parse JSON không
- Đảm bảo tên phím viết đúng (case-sensitive)

### Phím không hoạt động?
- Kiểm tra phím có trong danh sách VirtualKey không
- Xem log `[KeyboardManager] Converted X mappings to VirtualKey`
- Phím để trống (`""`) sẽ bị bỏ qua

### Controller không phản ứng?
- Kiểm tra ViGEm Bus Driver đã cài chưa
- Xem log `[ViGEmController] Xbox 360 controller ao da duoc tao`
- Type để trống sẽ gây lỗi parse
