# ShikiPad

(✿◠‿◠) Welcome to ShikiPad! (✿◠‿◠)

ShikiPad is a Windows keyboard/mouse mapping tool specifically designed for the PlayStation 5 (DualSense) controller. Built on pure Direct HID (low-level device read/write) technology, it effortlessly bypasses dual-input blockers at the system level (such as HidHide), delivering an incredibly pure and precise keyboard & mouse control experience.

## 🌟 Core Features
1. **Pure USB Protocol Direct Connection**: Hardcoded to parse the DualSense high-speed USB 0x01 reports for zero-latency execution.
2. **Industrial-Grade Edge-Trigger State Machine**: Completely eradicates key-stuck (Ghosting) issues and high-frequency queue-clogging typically caused by `SendInput`, while incorporating a scientifically tuned Typematic Repeat engine for natural typing.
3. **8-Way Layered Modifier Pad (Left Stick)**: The left stick transforms into an 8-way modifier pad combining Shift / Ctrl / Alt / Win, satisfying all complex shortcut requirements.
4. **Dynamic Layer Switching & "Latest Wins" Logic (L1/R1/L2/R2)**:
   - Original **"Modifier Stack"**: When holding multiple shoulder/trigger buttons, the most recently pressed one dictates the active layer. Releasing the new modifier seamlessly drops you back to the previously held modifier's layer.
   - **Zero-Latency Letter Triggering**: Base layer buttons (D-Pad/Face buttons) enjoy a 60ms anti-preemption buffer and a 100ms trailing-release silence to prevent misclicks. However, the moment you enter a letter layer via a modifier, these delays are instantly bypassed to 0ms, enabling lightning-fast typing!

---

## 🚀 Final Release Default Layout

- **Sticks & Triggers Basics**
  - **Left Stick**: Pushing into 8 directions while pressing a key holds keyboard modifiers (Left=Ctrl, Up=Win, Right=Alt, Down=Shift).
  - **Right Stick**: Mouse movement. Employs an advanced non-linear power curve algorithm for precise micro-adjustments and fast large flicks.
  - **L3 / R3 (Stick Clicks)**: L3 = Left Mouse Click, R3 = Right Mouse Click.

- **Letters & Symbols Mapping (Based on D-Pad and Face Buttons)**

| Button / Modifier | Base (No Mod) | R1 | L1 | R2 | L2 | R1+R2 | L1+L2 |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **↑** (Up) | ⬆️ | I | S | M | K | 7 | ' (Quote) |
| **→** (Right) | ➡️ | N | R | W | V | 8 | / (Slash) |
| **□** (Square) | Space | E | D | J | 1 | 9 | ; (Semicolon) |
| **△** (Triangle) | Backspace | A | G | X | 2 | 0 | [ (Left Bracket) |
| **←** (Left) | ⬅️ | O | L | Q | 3 | , (Comma) | ] (Right Bracket) |
| **↓** (Down) | ⬇️ | T | C | F | 4 | . (Period) | \ (Backslash) |
| **×** (Cross) | Enter | H | Y | P | 5 | - (Minus) | ` (Grave) |
| **○** (Circle) | Tab | U | Z | B | 6 | = (Equals) | Space |

*(Note: Holding a base face button and dynamically pressing/releasing shoulder modifiers will seamlessly transition the output letter instantly!)*

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
