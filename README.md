# ShikiPad

(✿◠‿◠) Welcome to ShikiPad! (✿◠‿◠)

ShikiPad is a Windows keyboard/mouse mapping tool specifically designed for the PlayStation 5 (DualSense) controller. Built on pure Direct HID (low-level device read/write) technology, it effortlessly bypasses dual-input blockers at the system level (such as HidHide), delivering an incredibly pure and precise keyboard & mouse control experience.

## 🌟 Core Features
1. **Pure USB Protocol Direct Connection**: Hardcoded to parse the DualSense high-speed USB 0x01 reports for zero-latency execution.
2. **Industrial-Grade Edge-Trigger State Machine**: Completely eradicates key-stuck (Ghosting) issues and high-frequency queue-clogging typically caused by `SendInput`, while incorporating a scientifically tuned Typematic Repeat engine for natural typing.
3. **8-Way Layered Modifier Pad (Left Stick)**: The left stick transforms into an 8-way modifier pad combining Shift / Ctrl / Alt / Win / Fn / Esc, satisfying all complex shortcut requirements.
4. **Touchpad Accumulator System (NEW)**: Hold the touchpad to enter "Accumulator Mode". Move the left stick to add multiple modifiers/keys into an accumulator pool without triggering them immediately. Release the touchpad to trigger all accumulated keys simultaneously! Releasing the touchpad in the deadzone cancels the accumulation.
5. **Dynamic Layer Switching & "Latest Wins" Logic (L1/R1/L2/R2)**:
   - Original **"Modifier Stack"**: When holding multiple shoulder/trigger buttons, the most recently pressed one dictates the active layer. Releasing the new modifier seamlessly drops you back to the previously held modifier's layer.
   - **Zero-Latency Letter Triggering**: Base layer buttons (D-Pad/Face buttons) enjoy a 60ms anti-preemption buffer and a 100ms trailing-release silence to prevent misclicks. However, the moment you enter a letter layer via a modifier, these delays are instantly bypassed to 0ms, enabling lightning-fast typing!

---

## 🚀 Final Release Default Layout

- **Sticks & Triggers Basics**
  - **Left Stick**: Pushing into 8 directions while pressing a key holds keyboard modifiers/keys: 
    - Left = Shift
    - UpLeft = Esc
    - Up = WheelUp
    - UpRight = Fn (Wait for a number key input to intercept it and output F1-F12!)
    - Right = Win
    - DownRight = Alt
    - Down = WheelDown
    - DownLeft = Ctrl
  - **Right Stick**: Mouse movement. Employs an advanced non-linear power curve algorithm for precise micro-adjustments and fast large flicks.
  - **L3 / R3 (Stick Clicks)**: L3 = Left Mouse Click, R3 = Right Mouse Click.

- **Letters & Symbols Mapping (Based on D-Pad and Face Buttons)**

| Button / Modifier | Base (No Mod) | R1 | L1 | R2 | L2 | R1+R2 | L1+L2 |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **↑** (Up) | ⬆️ | I | S | M | K | 7 | ' (Quote) |
| **→** (Right) | ➡️ | N | R | W | V | 8 | / (Slash) |
| **□** (Square) | Space | E | D | J | 1 | 9 | ; (Semicolon) |
| **△** (Triangle) | Backspace | A | G | X | 2 | 0 | [ (Left Bracket) |
| **←** (Left) | ⬅️ | O | L | Q | 3 | - (Minus) | ] (Right Bracket) |
| **↓** (Down) | ⬇️ | T | C | F | 4 | = (Equals) | \ (Backslash) |
| **×** (Cross) | Enter | H | Y | P | 5 | , (Comma) | ` (Grave) |
| **○** (Circle) | Tab | U | Z | B | 6 | . (Period) | None |

*(Note: Holding a base face button and dynamically pressing/releasing shoulder modifiers will seamlessly transition the output letter instantly!)*

- **Shift Behavior**: ShikiPad outputs real physical key presses. Shift+key combinations follow your Windows keyboard layout naturally:
  - `1`+Shift = `!`, `9`+Shift = `(`, `-`+Shift = `_`, `'`+Shift = `"`, etc.

---

## 🎮 Touchpad Accumulator ("Charge & Release" System)

The DualSense touchpad acts as a **clutch** to build up complex multi-modifier key combinations before executing them all at once.

### How It Works

1. **Single modifier (no touchpad)**: Simply push the left stick to Shift/Ctrl/Alt/Win/Esc — it activates immediately and stays held until the stick returns to the deadzone. This is the normal behavior.

2. **Multi-modifier combo (with touchpad)**:
   - **Hold the touchpad** (left, center, or right — all work).
   - While holding the touchpad, move the left stick through different directions. Each direction's key is **silently accumulated** into a pool (not sent to the PC).
   - **Release the touchpad** while the stick is on a modifier direction → all accumulated keys **fire simultaneously** and stay held as long as the stick remains outside the deadzone.
   - **Release the touchpad** while the stick is in the deadzone → the entire accumulation is **cancelled**, nothing is sent.

3. **Example: Alt+F4**
   - Push stick to Fn (↗), hold touchpad, press `4` on L2 layer → F4 is accumulated.
   - Return stick to deadzone (still holding touchpad).
   - Push stick to Alt (↘).
   - Release touchpad → **Alt+F4** fires simultaneously!

4. **Example: Ctrl+Shift+F5**
   - Hold touchpad → push stick to Ctrl (↙) → accumulated.
   - Push stick to Shift (←) → accumulated.
   - Push stick to Fn (↗), press `5` → F5 accumulated.
   - Release touchpad → **Ctrl+Shift+F5** fires!

---

## ⌨️ Fn Key Layer (Left Stick ↗)

Push the left stick to the **upper-right (↗)** direction to activate the Fn layer. While held in this direction, pressing number keys on the L2 or R1+R2 layers will be intercepted and converted to function keys:

| Number Key | Output |
|---|---|
| 1 | F1 |
| 2 | F2 |
| 3 | F3 |
| 4 | F4 |
| 5 | F5 |
| 6 | F6 |
| 7 | F7 |
| 8 | F8 |
| 9 | F9 |
| 0 | F10 |
| - | F11 |
| = | F12 |

The Fn direction fully respects the stick latching system — once triggered, even if your thumb wobbles slightly, the direction remains locked until the stick returns to the deadzone.

---

## 🎛️ How to Adjust Controller Deadzones & Sensitivity

All analog stick deadzones and trigger thresholds are saved in the configuration file `shikipad.json` located in the root directory. The program automatically generates this file upon its first run.

Open `shikipad.json` with Notepad:
```json
{
  "rightStickDeadzone": 0.0,
  "rightStickCurve": "power",
  "rightStickCurveExponent": 2.2,
  "leftStickEnterDeadzone": 0.5,
  "leftStickExitDeadzone": 0.25,
  "triggerPressThreshold": 0.35,
  "triggerReleaseThreshold": 0.25,
  "controllerBackend": "auto"
}
```

- **Right Stick Mouse Feel (`rightStickCurveExponent`)**: Default `2.2`. Higher values make small stick movements slower and more precise, while full stick deflection remains fast.
- **Left Stick Release Deadzone (`leftStickExitDeadzone`)**: The threshold to release the modifier (combined with hysteresis to prevent jitter, default 0.25).
- **After saving `shikipad.json`, no recompilation is needed. Simply restart `ShikiPad.exe` to apply changes instantly.**

---

## 🛡️ Advanced Guide: Making the Controller Work Everywhere

### 1. Fixing Unresponsive Inputs in Elevated Windows and UAC Prompts
Windows enforces strict security isolation. If your controller stops working in certain programs (running as Administrator) or on the UAC prompt, please configure the following:

- **Bypassing Administrator UIPI**: `ShikiPad.exe` is now built with a manifest that automatically requests **Administrator Privileges** on launch. This allows your virtual keyboard and mouse inputs to seamlessly interact with any elevated programs.
- **Bypassing UAC Secure Desktop (Dimmed Screen)**: By default, the Windows UAC prompt isolates input by jumping to a "Secure Desktop" where virtual inputs are blocked. To allow your controller to click "Yes", you must:
  1. Press `Win` and search for **"Change User Account Control settings"** (UAC).
  2. Pull the slider down one notch to **"Notify me only when apps try to make changes to my computer (do not dim my desktop)"**.
  3. Click OK. Your controller now has absolute 24/7 control over your PC!

### 2. Using HidHide to Prevent "Double Input" Issues
Since ShikiPad maps your controller to a keyboard and mouse, games that natively support controllers will receive BOTH the physical controller inputs and the virtual keyboard inputs simultaneously, causing chaotic behavior. We highly recommend using **HidHide**:

1. **Install HidHide**: Download and install [HidHide](https://github.com/nefarius/HidHide).
2. **Hide the Physical Controller**: Open the HidHide Configuration Client, go to the `Devices` tab, check `Sony Interactive Entertainment Wireless Controller`, and check `Enable device hiding` at the bottom.
3. **Whitelist ShikiPad**: Go to the `Applications` tab, click the `+` icon, and add your `ShikiPad.exe`.
4. **Result**: Windows and all games will now be completely blind to your physical controller, only recognizing your virtual keyboard/mouse output. Because of the whitelist, ShikiPad can still exclusively read the raw USB signals!
