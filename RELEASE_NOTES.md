# Release Notes

## V5.2 - 2026-07-22

- Preserved a complete deferred modifier tap when a left-stick or Create/Options modifier is released before its arbitrated KeyDown can run, preventing fast taps from disappearing behind a touchpad output.
- Made the modifier-binding wait genuinely independent from the pure layer grace window while retaining the original layer takeover boundaries.
- Enforced the documented module order at runtime, added native Interception send-result checks with one retry, serialized context cleanup, and kept injector bookkeeping unchanged after failed sends.
- Added a per-session single-instance lock, HID parse diagnostics, validated editable parameters, implemented the documented linear right-stick curve, and corrected all assembly metadata to V5.2.
- Added reproducible release packaging, SHA-256 manifests, third-party notices, .NET 8 Desktop Runtime documentation, and driver-installer exit-code handling; removed the unused Bluetooth marker from the package definition.
- Added a per-poll output arbiter so simultaneous modules cannot interleave independent output sequences. Priority is touchpad click, touch gesture, modifier transitions, L3/R3, eight action positions, left-stick wheel, then right-stick pointer movement.
- Fixed left-stick and Create/Options modifier presses bypassing arbitration: their logical state and 45 ms binding are registered immediately, while physical KeyDown output is serialized after touchpad outputs and before bindable mouse/action output.
- Release events never enter the arbiter: KeyUp/mouse-up/gesture-modifier cleanup always runs. Logical modifier registration plus Home, Mute, and shoulder/trigger state also update before arbitration; only physical modifier KeyDown is serialized.
- Deferred mouse-button and action outputs keep their pending state for the next poll, and blocked repeats keep their due time instead of being silently settled.
- `KeyTap` now temporarily releases and restores an already-held ordinary target key, preventing an atomic shortcut from stealing another module's held-key ownership.
- Touch-gesture shortcuts now use exact modifier isolation: unrelated held modifiers are suspended for the atomic shortcut and restored afterward, while action-layer taps retain their intentional 45 ms bound modifiers.

## 2026-07-22

- Changed two-finger right-side direct swipes to left `Win+Shift+S`, up `Win+Shift+M`, right `Alt+F4`, and down `Win+M`; all four remain one-shot gestures.
- Set the straight-up and straight-down left-stick wheel sectors to ±25° without moving either vertical centerline, for 50° per wheel sector; each of the four modifier sectors now occupies exactly 65°.

## 2026-07-21

- Caps/Fn is now a persistent toggle: only a letter capitalization or an actual `1..0 - =` to `F1..F12` translation automatically exits the mode. Touchpad clicks and every untranslated action leave it active.
- Raised the left-stick scroll-sector deadzone from 0.15 to 0.30 and the four modifier-sector deadzones from 0.45 to 0.50.
- A primed Home clutch is now consumed only by the eight mapped action positions across every layer or by L3/R3. Touchpad `Delete`, `Backspace`, and `Caps Lock` preserve the clutch state, while Base repeat actions continue emitting the saved modifiers with every repeat pulse.
- Restored the pure layer timing state machine: Base is only the eight D-pad/face mappings with no shoulder or trigger held, and modifier capture can no longer override layer ownership. The 45 ms layer grace, 15 ms post-grace, 35 ms combo window, and 20/30 ms occupancy limits are independent from the new 45 ms modifier-binding window.
- Touchpad clicks, Home, Mute, and mouse buttons retain separate module identities. The independent 45 ms modifier-binding window now covers only L3/R3 and all eight action positions across Base and character layers; Home no longer registers or captures bindings from this window.
- Modifier-bound Base repeat keys now emit a complete chord pulse on the initial event and every repeat tick: modifiers and the action key are pressed together, then the action and that pulse's modifier references are released. The binding never leaves a modifier artificially held between repeats.
- Two-finger right-side mappings are now left `Win+V`, up `Win+Shift+S`, right `Win+E`, and down `Alt+F4`.
- Raised `TouchGestureHoldStillDistance` from 50 to 100 for the post-trigger static-finger constraint. On the first two-finger trigger, the finger that has not reached a 150/180 direction threshold becomes static and settles its current position as the baseline, even if it has already moved 100.
- Once the static role is established, reaching 100 movement, releasing that contact, or changing its touch ID immediately invalidates the whole touch sequence until every contact is released. The static role is never transferred and the remaining mover cannot become another gesture.
- Moving-finger continuation remains available while the same static contact stays within tolerance: lifting and replacing only the mover starts a new two-finger segment from the mover's new touch-down position.
- Every physical 150/180 moving-finger step now settles both movement axes and refreshes the static-finger baseline, including forward, reverse, and no-feedback steps. Timer-only repeats do not refresh the baseline.

## 2026-07-20

- Replaced the parallel modifier/layer windows with one 45 ms action-decision window. A captured modifier wins and locks the layer that existed when the action began; shoulder/trigger changes after that point cannot take over the same action.
- Captured modifiers are sticky action ownership rather than a one-time restoration. They survive the physical modifier's release until a held keyboard or mouse action ends, and fully wrap virtual taps.
- Inputs are now documented as modifier, non-modifier action, or shoulder/trigger layer controls. Base D-pad repeat, touchpad `Delete`/`Backspace`, Home, and Mute bypass modifier look-ahead; non-repeating `Caps Lock`, face/character actions, and L3/R3 can use it.
- Touchpad swipe gestures no longer produce a buffer-zone gesture. The moving finger's start segment and first recognized direction resolve every gesture to the left or right side.
- One-finger mappings now use left-side Alt-Tab/window sizing and right-side desktop/window switching. Two-finger left-side continuation remains intact, while right-side one-shot mappings are now `Win + V`, `Ctrl + Shift + Esc`, screenshot, and `Alt + F4`.
- After recognition, the gesture side and repeat mode remain locked until the moving finger lifts. Every vertical 150 or horizontal 180 movement settles one step and resets both axis accumulators, even when that step has no shortcut feedback; Alt-Tab remains the special four-direction distance gesture.
- The obsolete 70% trajectory-zone parameter and buffer gesture mappings were removed. The middle touchpad-click area still sends real `Caps Lock` and is independent from swipe zoning.

## 2026-07-13

- Touchpad gestures now use three independent zones: left confirmed, middle buffer, and right confirmed. A confirmed zone needs 70% of the pre-trigger trajectory; otherwise the gesture stays in the buffer.
- Gesture zone and finger count lock after recognition and remain stable until the moving finger lifts. Buffer Alt-Tab navigation keeps holding `Alt` and accepts distance-based arrow gestures even after crossing into another zone.
- One-finger and two-finger mappings were reorganized, including 550 ms desktop repeat and 450 ms window/tab/navigation repeat with reverse-direction switching.
- All touchpad hold-then-swipe recognition, mappings, timers, and state were removed.
- Touchpad clicks and gestures now use first-trigger-wins arbitration. `Backspace`, `Delete`, and `Caps Lock` clicks send real key-down/key-up events, remain available in every layer, and correctly consume one-shot Caps/Fn.
- The Chinese README was rewritten around the actual mapper behavior and current parameters; the redundant English README was removed.

## 2026-07-12

- Two-finger direct touchpad mappings were rearranged: left side now handles tab/back/forward navigation, right side now handles screenshot, Task Manager, and `Alt + F4`; two-finger hold-then-swipe is unmapped.
- Two-finger continuation now uses the returning moving finger's real touch-down point as the next segment origin, so quick re-swipes are recognized without losing the first sampled distance.
- Two-finger continuation now refreshes the still-finger baseline between moving-finger segments, so small thumb drift does not accumulate and break later swipes.
- Unmapped two-finger continuation segments are ignored without cancelling continuation, preventing an accidental unmapped direction from making later swipes appear dead.
- Touchpad stillness checks are unified: movement below `TouchGestureHoldStillDistance` (50 in this release) counts as still, but swipe distance is still counted from the real segment start.
- Two-finger touchpad continuation now survives brief edge unreadable samples: a lightly out-of-bounds edge touch is treated as that one touch temporarily leaving instead of pinning it to the edge, and continuation waits instead of restarting when the still finger briefly disappears.
- Touchpad click now sends `Backspace` when the click report has no active touch point, and also when two touch points are active.
- Create/Options system buttons now use deterministic press-before-actions and release-after-actions ordering, so `Right Alt` / `Right Ctrl` combinations are less likely to miss when pressed in the same frame as action keys or touchpad actions.
- Keyboard and mouse hold bookkeeping is serialized inside the injector, reducing release races between mapper ticks, disable/enable, disconnect, and process shutdown.

## 2026-07-11

- Mute short press now toggles the one-shot Caps/Fn layer. Press Mute again before an action key to cancel it and restore normal output.
- Caps/Fn maps `1..0`, `-`, and `=` to `F1..F12`; unshifted letters output as shifted uppercase letters.
- Touchpad gestures now support direct swipe, hold-then-swipe, and two-finger mappings. Hold-then-swipe is recognized at the 150/180 trigger distance after 450 ms of touch duration.
- Touchpad and left-stick modifier overlap is guarded by modifier reference counting, reducing accidental stuck or prematurely released modifier keys.
- The README no longer duplicates a separate install section; driver and setup order is the installation guide.
