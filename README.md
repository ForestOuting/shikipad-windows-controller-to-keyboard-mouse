# ShikiPad (Windows Controller-to-Keyboard Mapper)

ShikiPad is a Windows application that maps DualSense (PS5), Xbox 360, and Xbox Series X|S controllers to keyboard and mouse inputs.

This final release is heavily optimized for **fast controller typing**. You can use it to fluently type characters and operate the mouse while lying in bed or using a handheld/streaming device.

Chinese documentation: [README.zh-CN.md](README.zh-CN.md)

## 📥 Installation & First Run

### 1. Download ShikiPad
Download the latest zip archive from the **Releases** section on the right side of the GitHub page (or clone this repository). Extract it to any folder on your computer.

### 2. (Required) Install the Kernel Driver
To allow ShikiPad to work inside **Virtual Machines (like VMware)** or in **games with low-level anti-cheat**, we use the Interception driver for hardware-level injection.
1. Inside the extracted folder, find the **`install_driver.bat`** file.
2. **Right-click on it and select "Run as administrator"**.
3. A black command prompt will appear. Once it says `Installation complete!`, you can close the window.
4. **You MUST restart your computer** for the driver to take effect.

### 3. Run the Program
After restarting your computer:
1. Double-click `ShikiPad.exe`.
2. A black terminal window will appear asking you to choose your controller profile:
   - Type `1` for DualSense (PS5)
   - Type `2` for Xbox 360
   - Type `3` for Xbox Series X|S
3. The program will minimize, and your controller is now mapped to your computer!

To close ShikiPad, simply close the black terminal window.

---

## 🎮 Controls Overview

### 🖱️ Mouse Control (Right Stick)

| Controller Input | Output |
|---|---|
| **Right Stick** movement | Mouse movement |
| **R3** (Right Stick Click) | Right Mouse Button |
| **L3** (Left Stick Click) | Left Mouse Button |

### 🛠️ Modifiers & Functions (Left Stick)
The left stick acts as an 8-way directional pad for modifier keys. Push the stick in a direction to hold the key, and release the stick back to the center to release the key.

| Left Stick Direction | Keyboard Output |
|---|---|
| **Left** | `Shift` |
| **Down-Left** | `Ctrl` |
| **Down-Right** | `Left Alt` |
| **Right** | `Win` |
| **Up-Left** | `Esc` |
| **Up** | Mouse Wheel Up |
| **Down** | Mouse Wheel Down |
| **Up-Right** | Activate **`Fn`** Layer (see below) |

#### Fn Layer (F1 - F12)
When you hold the Left Stick in the **Up-Right (Fn)** direction, number keys on the controller will output F-keys instead.
- Pressing numbers or `-`/`=` while holding Fn will output `F1` ~ `F12`.

### ⌨️ Typing & Action Buttons

#### Base Layer
When you press the D-Pad or Face Buttons *without* holding any triggers or bumpers:

| Controller Button | Keyboard Output |
|---|---|
| **D-Pad (Up/Down/Left/Right)** | Arrow Keys |
| **Square** (Xbox: X) | `Space` |
| **Triangle** (Xbox: Y) | `Backspace` |
| **Cross** (Xbox: A) | `Enter` |
| **Circle** (Xbox: B) | `Tab` |

*Holding keys on the Base Layer will repeatedly output them (just like holding a real key).*

#### Character Layer (Fast Typing)
When you hold down the top bumpers or triggers (**L1, R1, L2, R2**), the D-Pad and Face buttons turn into English letters, numbers, and punctuation marks.

To prevent typos during fast typing, **Character Layer inputs act as single virtual taps**. Holding them down will *not* repeat the character.

Here is the Character Mapping Table:

*Button order (left to right): Up, Right, Square, Triangle, Left, Down, Cross, Circle*

| Shoulder/Trigger Held | Up | Right | Square(X) | Triangle(Y) | Left | Down | Cross(A) | Circle(B) |
|---|---|---|---|---|---|---|---|---|
| **R1 (RB)** | i | n | e | a | o | t | h | u |
| **L1 (LB)** | s | r | d | g | l | c | y | z |
| **R2 (RT)** | m | w | j | x | q | f | p | b |
| **L2 (LT)** | k | v | 1 | 2 | 3 | 4 | 5 | 6 |
| **R1 + R2** | 7 | 8 | 9 | 0 | - | = | , | . |
| **L1 + L2** | `'` | `/` | `;` | `[` | `]` | `\` | `` ` `` | None |

*Note: For uppercase letters or symbols (like `!`, `@`, `_`, `?`), push the Left Stick to the Left (`Shift`) while typing the character.*

---

## ⚙️ Advanced Configuration
If you want to tweak mouse sensitivity or typing delays, you can edit the `shikipad.json` file in the program folder (open it with Notepad). Save the file and restart ShikiPad for the changes to take effect.

Common settings you might want to change:
- `mouseMaxSpeed`: Maximum mouse cursor speed.
- `rightStickCurveExponent`: Stick acceleration curve (higher values make fine movements more precise).
- `rightStickDeadzone`: Right stick deadzone (Default `0.03`. Increase this if the mouse drifts when idle).
- `useInterception`: Whether to use the kernel driver (Defaults to `true`. If the driver isn't installed, it safely falls back to normal Windows input).
- `actionLayerGraceMs`: Typing typo-prevention delay (Default `80` ms). If you keep accidentally typing a Space or Enter when you meant to type a letter, increase this value to `100` or `120`.
- `layerTakeoverWindowMs`: Grace window for a later-pressed shoulder/trigger to override an already-pressed action button (Default `50` ms).

## 💡 Tip: Double Input Issue
If you play a game that natively supports controllers, the game might receive both the raw gamepad input *and* ShikiPad's keyboard input at the same time.
To fix this, install [HidHide](https://github.com/nefarius/HidHide/releases), hide your physical controller from the system, and add `ShikiPad.exe` to HidHide's whitelist.
