# ShikiPad

ShikiPad is a Windows keyboard and mouse mapper for DualSense, Xbox 360, and Xbox Series X|S controllers. It reads DualSense through Direct HID, reads Xbox controllers through XInput, and sends real keyboard/mouse input through `SendInput`.

This release is tuned for fast text entry. Letter, number, and punctuation layers use one-shot virtual taps, while the base navigation layer keeps real held keys and progressive repeat.

Chinese documentation: [README.zh-CN.md](README.zh-CN.md)

## Controller Selection

On startup, the terminal UI asks for the controller profile before the mapper begins reading input:

| Choice | Controller | Backend |
|---:|---|---|
| 1 | DualSense / DS5 | Direct HID |
| 2 | Xbox 360 controller | XInput |
| 3 | Xbox Series X|S controller | XInput |

The selected profile is fixed for that run. ShikiPad does not inspect incoming button signals every frame to guess which controller is connected, so the running input path stays lean.

For shortcuts or launchers, you can skip the prompt:

```powershell
.\ShikiPad.exe --controller=ds5
.\ShikiPad.exe --controller=xbox360
.\ShikiPad.exe --controller=xboxseries
```

Xbox controllers do not have a DualSense touchpad. Their View/Back and Menu/Start buttons both act as ShikiPad's touchpad clutch for the left-stick accumulator. The clutch stays active while either small button is held, so you can hand the hold from the left-side button to the right-side button without dropping the accumulated keys. Menu/Start also remains the Options button, so View/Back + Menu/Start is still the emergency enable/disable hold.

## Stopping ShikiPad

ShikiPad has no terminal exit command. When the program closes, it automatically releases keys and mouse buttons that ShikiPad was holding, including base-layer holds, left-stick modifiers, Fn holds, and mouse buttons.

## Final Input Model

### Right Stick

| Control | Output |
|---|---|
| Right stick movement | Mouse movement |
| R3 | Right mouse button |

### Left Stick

The left stick is an 8-way modifier/function pad. Directions latch until the stick returns to the deadzone.

| Direction | Output |
|---|---|
| Up | Mouse wheel up |
| Up-right | Fn |
| Right | Win |
| Down-right | Left Alt |
| Down | Mouse wheel down |
| Down-left | Ctrl |
| Left | Shift |
| Up-left | Esc |
| L3 | Left mouse button |

### Base Layer

Base-layer keys are real held keys. Holding them repeats with a smooth ramp.

| Controller button | Output |
|---|---|
| D-pad Up | Arrow Up |
| D-pad Right | Arrow Right |
| D-pad Down | Arrow Down |
| D-pad Left | Arrow Left |
| Square | Space |
| Triangle | Backspace |
| Cross | Enter |
| Circle | Tab |

### Character Layers

Character layers are virtual taps. Each press emits exactly one `KeyDown` followed by one `KeyUp`, then the button is consumed until it is released. This avoids accidental fallback to Space/Tab/Enter when shoulder or trigger timing is very fast.

Button order is fixed:

`Up, Right, Square, Triangle, Left, Down, Cross, Circle`

| Layer | Up | Right | Square | Triangle | Left | Down | Cross | Circle |
|---|---|---|---|---|---|---|---|---|
| R1 | i | n | e | a | o | t | h | u |
| L1 | s | r | d | g | l | c | y | z |
| R2 | m | w | j | x | q | f | p | b |
| L2 | k | v | 1 | 2 | 3 | 4 | 5 | 6 |
| R1 + R2 | 7 | 8 | 9 | 0 | - | = | , | . |
| L1 + L2 | `'` | `/` | `;` | `[` | `]` | `\` | `` ` `` | None |

### Shift Behavior

ShikiPad sends physical key input. It does not output Unicode punctuation directly. Shifted symbols are handled by Windows and the active keyboard layout:

`1 + Shift = !`, `2 + Shift = @`, `- + Shift = _`, `= + Shift = +`, `/ + Shift = ?`, and so on.

## Fast Typing Logic

The current release uses these rules to keep typing stable:

1. When an action button is pressed, ShikiPad waits `actionLayerGraceMs` before deciding the output layer.
2. During that short window, the button records the latest non-base layer that appears while the button is still physically down.
3. If a character layer is selected, ShikiPad sends one virtual tap and suppresses further layer changes until the action button is released.
4. If the base layer is selected, ShikiPad sends a real key down, keeps it held, and enables progressive repeat.
5. If a combo layer such as `R1+R2` or `L1+L2` is selected, that combo is locked for that action-button press. Releasing one shoulder/trigger while the action button is still down will not fall back to a single layer.
6. Fn-generated `F1-F12` keys are handled by the left-stick Fn state and keep their existing hold/charge behavior.
7. `R1+R2` and `L1+L2` only become combo layers when the second key arrives within `comboLayerWindowMs`. If a trigger or shoulder has already been held longer than that window, a late overlap uses the newest single layer instead of accidentally becoming a combo.

This is the intended typing behavior:

- Quick `R1 + Right`, release, then quick `L1 + Square` should type `nd`, not `n Space`.
- Quick `L1 + Up`, release, then quick `R1 + Cross` should type `sh`, not `ih`.
- `R1+R2 + Cross` should type `,`; releasing R1 or R2 before Cross is released must not type `h` or `p`.

## Touchpad Accumulator

The DualSense touchpad is a clutch for multi-key holds.

Normal left-stick behavior:

- Move to Shift/Ctrl/Alt/Win/Esc: key is held immediately.
- Return to deadzone: key is released.

Touchpad accumulator behavior:

1. Hold the touchpad.
2. Move the left stick through modifier directions. The keys are accumulated silently.
3. Release the touchpad while the left stick is outside the deadzone to press all accumulated keys together.
4. Release the touchpad while the left stick is in the deadzone to cancel and clear the accumulator.

Mouse wheel directions are not accumulated.

## Fn Layer

Move the left stick to Up-right to enter Fn. Then press one of these physical keys:

| Physical key | Output |
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

Fn output also respects the touchpad accumulator. For example, Alt+F4 can be built by accumulating F4 and Alt, then releasing the touchpad while the stick is outside the deadzone.

## Configuration

Runtime settings live in `shikipad.json`. Edit the file, save it, and restart `ShikiPad.exe`.

Current default settings:

```json
{
  "enabled": true,
  "mouseSensitivity": 1,
  "mouseMaxSpeed": 28,
  "rightStickDeadzone": 0,
  "rightStickCurve": "power",
  "rightStickCurveExponent": 2.2,
  "rightStickEpsilon": 0.002,
  "leftStickEnterDeadzone": 0.5,
  "leftStickExitDeadzone": 0.35,
  "triggerPressThreshold": 0.35,
  "triggerReleaseThreshold": 0.25,
  "touchpadSwipeThreshold": 0.22,
  "touchpadMaxSwipeMs": 600,
  "repeatDelayMs": 180,
  "repeatIntervalMs": 20,
  "baseRepeatSlowIntervalMs": 160,
  "baseRepeatRampMs": 1200,
  "actionLayerGraceMs": 80,
  "actionLayerSwitchGuardMs": 120,
  "comboLayerWindowMs": 100,
  "useScanCode": true,
  "scrollSlowIntervalMs": 100,
  "scrollFastIntervalMs": 20,
  "r3FreezeMs": 60
}
```

### Delay and Repeat Settings

| Setting | Default | Meaning |
|---|---:|---|
| `actionLayerGraceMs` | 80 | Layer-confirmation window for action buttons. Increase if fast shoulder/trigger presses still fall back to base keys. Decrease if all character taps feel too delayed. |
| `actionLayerSwitchGuardMs` | 120 | Secondary safety guard for non-base layer-change paths. Normal character input is already protected by virtual tap plus suppress-until-release. |
| `comboLayerWindowMs` | 100 | Maximum time between R1/R2 or L1/L2 presses for a combo layer. Increase if intentional combos are hard to trigger. Decrease if late shoulder/trigger overlaps still become combos while switching layers. |
| `repeatDelayMs` | 180 | Delay before base-layer repeat starts. |
| `baseRepeatSlowIntervalMs` | 160 | First repeat interval for held base-layer keys. |
| `repeatIntervalMs` | 20 | Fastest repeat interval after the ramp completes. |
| `baseRepeatRampMs` | 1200 | Time used to smoothly accelerate repeat from slow to fast. |
| `scrollSlowIntervalMs` | 100 | Mouse wheel interval when the left stick barely crosses the wheel direction threshold. |
| `scrollFastIntervalMs` | 20 | Mouse wheel interval near full stick deflection. |
| `r3FreezeMs` | 60 | Short mouse-movement freeze after R3, avoiding accidental pointer drift while right-clicking. |

To tune shoulder/trigger combo timing, edit `comboLayerWindowMs` in `shikipad.json` and restart ShikiPad. A practical range is 70-140 ms: lower values favor fast layer switching, higher values make intentional combos easier.

### Analog Settings

| Setting | Default | Meaning |
|---|---:|---|
| `rightStickCurveExponent` | 2.2 | Higher values make small mouse movement slower and full deflection still fast. |
| `mouseMaxSpeed` | 28 | Maximum mouse speed scale. |
| `rightStickEpsilon` | 0.002 | Tiny movement cutoff after neutral calibration. |
| `leftStickEnterDeadzone` | 0.5 | Radius required to enter a left-stick direction. |
| `leftStickExitDeadzone` | 0.35 | Radius below which the left-stick direction releases. |
| `triggerPressThreshold` | 0.35 | L2/R2 press threshold. |
| `triggerReleaseThreshold` | 0.25 | L2/R2 release threshold. Keep this lower than press threshold for hysteresis. |

## Where to Change Code

Most users should only edit `shikipad.json`. Recompile only if you want to change mappings or state-machine behavior.

Important locations in `src/ShikiPad.cs`:

| Area | Code location |
|---|---|
| Runtime config fields and defaults | `Config` class |
| Config file loading/saving | `Config.Load` and `Config.Save` |
| Layer mapping tables | `MappingEngine` constructor |
| Latest-layer resolution | `MappingEngine.Resolve` |
| Combo timing window | `comboLayerWindowMs` in `shikipad.json`, used by `MappingEngine.Resolve` |
| Startup controller selection | `Program.SelectControllerProfile`, `DirectHidController` constructor |
| DualSense Direct HID parsing | `DirectHidController.ParseReport` |
| Xbox XInput parsing | `DirectHidController.XInputLoop`, `DirectHidController.ParseXInput` |
| Action-button state machine | `MapperForm.UpdateActionButtons` |
| Character virtual taps | `TapActionKey` |
| Base-layer held/repeat logic | `PressActionKey`, `UpdateBaseRepeat`, `BaseRepeatIntervalMs` |
| Left-stick 8-way mapping | `GetLeftStickKey`, `UpdateLeftStick`, `Sector` |
| Fn to F1-F12 translation | `TranslateToFKey`, `ApplyFnLayer`, `ActivateFnKey` |
| Right-stick mouse movement | `UpdateRightStick` |
| Shutdown input release | `InputInjector.ReleaseAll`, `Program.RegisterShutdownRelease`, `MapperForm.OnFormClosing` |
| Startup terminal UI | `Program.PrintGradientBanner` and related `Write...` helpers |

Mapping table edits:

- Base layer: `_tables[(int)Layer.Base]`
- R1 layer: `_tables[(int)Layer.R1]`
- L1 layer: `_tables[(int)Layer.L1]`
- R2 layer: `_tables[(int)Layer.R2]`
- L2 layer: `_tables[(int)Layer.L2]`
- R1+R2 layer: `_tables[(int)Layer.R1R2]`
- L1+L2 layer: `_tables[(int)Layer.L1L2]`

After changing code, rebuild with:

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /utf8output /platform:x64 /target:exe /out:ShikiPad.exe /win32icon:shiki.ico /win32manifest:ShikiPad.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\ShikiPad.cs
```

## UAC and Elevated Windows

`ShikiPad.exe` is built with `requireAdministrator` in `ShikiPad.manifest`, so it asks for administrator permission when launched.

Important Windows limitation:

- If ShikiPad is not already running when a UAC prompt appears, it cannot control that prompt.
- If Windows isolates the UAC prompt on a protected desktop, synthetic input can be blocked by the operating system.
- Setting UAC to "notify me only when apps try to make changes to my computer (do not dim my desktop)" removes the dimmed secure desktop, but it does not make it possible for an app that is not running/elevated yet to control its own startup UAC prompt.

Recommended use:

1. Start `ShikiPad.exe`.
2. Confirm its UAC prompt with a real keyboard/mouse when needed.
3. Keep ShikiPad running as administrator.
4. It can then interact with normal and elevated windows that accept `SendInput`.

## HidHide

If a game sees both the physical controller and ShikiPad's virtual keyboard/mouse input, use HidHide:

1. Install HidHide.
2. Hide the DualSense controller.
3. Add `ShikiPad.exe` to HidHide's application whitelist.
4. Restart ShikiPad.

## Test Commands

Useful built-in checks:

```powershell
.\ShikiPad.exe --layer-test
.\ShikiPad.exe --left-stick-test
.\ShikiPad.exe --mouse-test
```

Expected high-level checks:

- Layer order is `Up, Right, Square, Triangle, Left, Down, Cross, Circle`.
- `R1+R2` and `L1+L2` resolve correctly.
- Left stick Up-right is Fn.
- Left stick Up/Down are mouse wheel directions.

## Release Notes

This final release is optimized for practical fast typing:

- Character layers are virtual taps.
- Base layer holds and repeats.
- Combo layers do not fall back while the action button is still held.
- Pending layer capture stops when the action button is released, preventing cross-character contamination.
- Shutdown paths automatically release ShikiPad-held inputs without a terminal exit command.
- Startup terminal UI uses a filled, extruded ANSI logo, with the ShikiPad face flowing from Spring to Summer to Autumn to Winter.
