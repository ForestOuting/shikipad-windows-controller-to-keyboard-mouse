# ShikiPad 中文说明书

ShikiPad 是一个面向 Windows 的 DualSense、Xbox 360、Xbox Series X|S 手柄键盘鼠标映射软件。DualSense 使用 Direct HID 读取，Xbox 手柄使用 XInput 读取，然后用 Windows 的 `SendInput` 模拟真实键盘和鼠标输入。

这个最终版的核心目标是：用手柄快速打字时尽量稳定、灵敏、不串键。

## 启动时选择手柄型号

程序启动后，会先在终端界面让你选择本次运行使用的手柄型号：

| 选项 | 手柄 | 读取方式 |
|---:|---|---|
| 1 | DualSense / DS5 | Direct HID |
| 2 | Xbox 360 手柄 | XInput |
| 3 | Xbox Series X|S 手柄 | XInput |

这个选择只在启动时决定一次。运行过程中不会每帧根据输入信号猜测手柄型号，因此不会给实际打字和鼠标映射路径增加额外识别延迟。

如果你要做快捷方式、自启动或批处理，也可以跳过交互选择：

```powershell
.\ShikiPad.exe --controller=ds5
.\ShikiPad.exe --controller=xbox360
.\ShikiPad.exe --controller=xboxseries
```

Xbox 手柄没有 DualSense 触控板，所以 Xbox 的 View/Back 和 Menu/Start 两个小键都会作为 ShikiPad 的“触控板按压蓄力键”。只要其中任意一个还按着，就持续保持蓄力；你可以先按左边小键蓄力，再按住右边小键并松开左边小键，蓄力池不会断。Menu/Start 同时仍然作为 Options，因此 View/Back + Menu/Start 仍然是紧急启用/停用组合。

## 关闭 ShikiPad

ShikiPad 不提供终端退出命令。程序关闭时会自动释放由 ShikiPad 按住的键盘键和鼠标键，包括基础层按住、左摇杆修饰键、Fn 按住和鼠标按键。

## 最终输入逻辑

### 右摇杆

| 手柄输入 | 输出 |
|---|---|
| 右摇杆移动 | 鼠标移动 |
| R3 | 鼠标右键 |

### 左摇杆

左摇杆是 8 方向功能盘。方向触发后会锁定，直到摇杆回到死区。

| 左摇杆方向 | 输出 |
|---|---|
| 上 | 鼠标滚轮上 |
| 右上 | Fn |
| 右 | Win |
| 右下 | 左 Alt |
| 下 | 鼠标滚轮下 |
| 左下 | Ctrl |
| 左 | Shift |
| 左上 | Esc |
| L3 | 鼠标左键 |

### 基础层

基础层是真实按住：按下就是 `KeyDown`，松开就是 `KeyUp`。按住会渐进式连发。

| 手柄按键 | 输出 |
|---|---|
| D-pad 上 | 键盘上 |
| D-pad 右 | 键盘右 |
| D-pad 下 | 键盘下 |
| D-pad 左 | 键盘左 |
| 方块 | Space |
| 三角 | Backspace |
| 叉 | Enter |
| 圆 | Tab |

### 字母、数字、标点层

字母、数字、标点层是虚拟按键：每次确认后只发送一次 `KeyDown + KeyUp`，然后这个手柄动作键会被消费，直到你真正松开它。

这样做是为了避免快速打字时出现这些问题：

- 想打 `nd`，结果变成 `n Space`。
- 想打 `sh`，结果变成 `ih`。
- `R1+R2` 松慢后，从 `,` 回落成 `h` 或 `p`。
- 按住一个动作键时点另一个肩键，旧字母被改写成新字母。

固定按键顺序：

`上, 右, 方块, 三角, 左, 下, 叉, 圆`

| 层 | 上 | 右 | 方块 | 三角 | 左 | 下 | 叉 | 圆 |
|---|---|---|---|---|---|---|---|---|
| R1 | i | n | e | a | o | t | h | u |
| L1 | s | r | d | g | l | c | y | z |
| R2 | m | w | j | x | q | f | p | b |
| L2 | k | v | 1 | 2 | 3 | 4 | 5 | 6 |
| R1 + R2 | 7 | 8 | 9 | 0 | - | = | , | . |
| L1 + L2 | `'` | `/` | `;` | `[` | `]` | `\` | `` ` `` | 空 |

### Shift 规则

ShikiPad 不直接输出 Unicode 字符，而是模拟真实物理键。Shift 后的字符由 Windows 当前键盘布局和输入法自然处理。

例如：

- `1 + Shift = !`
- `2 + Shift = @`
- `9 + Shift = (`
- `0 + Shift = )`
- `- + Shift = _`
- `= + Shift = +`
- `/ + Shift = ?`
- `` ` + Shift = ~ ``

## 快速打字状态机

这是最终版最重要的部分。

1. 动作键按下后，会先进入 `actionLayerGraceMs` 的短确认窗口，当前默认是 80ms。
2. 如果动作键是在基础层按下的，且第一个肩键/扳机层在这 80ms 窗口结束前仍然有效，这个基础动作键会按这个第一个肩键/扳机层输出。
3. 如果动作键按下时已经处在非基础层，之后又按下另一个肩键/扳机层，后按下的层只会向前接管 `layerTakeoverWindowMs` 内刚按下且仍在 pending 的动作键，当前默认是 35ms；超过 35ms 时，动作键保持原来的非基础层。
4. 如果最终确认到字母、数字、标点层，就发送一次虚拟 tap：`KeyDown + KeyUp`。
5. 虚拟 tap 发送后，这个动作键在本次按住期间被消费，不会再因为肩键/扳机变化变成另一个字符。
6. 如果最终确认到基础层，就进入真实按住模式，并启用渐进式连发。
7. `R1+R2` 和 `L1+L2` 组合层会锁定本次动作键。组合键先松开一个时，不会回落到 `R1/R2/L1/L2` 单层。
8. `R1+R2` 和 `L1+L2` 只有在第二个键进入 `comboLayerWindowMs` 时间窗口内才算组合层。如果某个肩键或扳机已经按住超过这个窗口，之后再和对应键短暂重叠，会按最后触发的单层处理，不会误变成组合层。
9. 动作键一旦松开，pending 状态不会再继续捕获下一组肩键/扳机，避免上一枚键污染下一枚键。

换成一句话：基础动作键第一次被肩键/扳机“拉起”的窗口是 80ms；已有非基础层之后再被后按肩键/扳机“抢走”的窗口是 35ms。

实际效果应该是：

- 快速 `R1 + 右`，再快速 `L1 + 方块`，输出 `nd`。
- 先按基础层 `叉`，再在 80ms 确认窗口内按住 `R1` 到窗口结束，会输出 `h`；如果 `R1` 等到 80ms 之后才按下，就不会把这次 `叉` 改成 `h`。
- 快速 `L1 + 上`，100ms 后按 `叉`，再在 35ms 内按 `R1`，即使 `L1` 还没松，也会输出 `sh`。如果间隔到 70ms，`叉` 会保持前面的 `L1` 层，不再被 `R1` 抢走。Xbox 手柄对应为 `LB + D-pad 上`，再按 `A`，再按 `RB`。
- `R1+R2 + 叉` 输出 `,`，即使 R1 或 R2 先松，也不会补出 `h` 或 `p`。

## 触控板蓄力系统

触控板是多修饰键蓄力开关。

普通左摇杆：

- 左摇杆到 Shift/Ctrl/Alt/Win/Esc 方向：立刻按下该键。
- 左摇杆回到死区：释放该键。

按住触控板时：

1. 按住触控板。
2. 左摇杆移动到不同方向，方向对应的键会被加入蓄力池，但不会立刻输出。
3. 松开触控板时，如果左摇杆还在某个非死区方向，蓄力池里的键会一起按下并保持。
4. 松开触控板时，如果左摇杆在死区，蓄力池会清空，不输出任何键。

鼠标滚轮方向不会进入蓄力池。

## Fn 层

左摇杆右上是 Fn。触发 Fn 后，按数字键或 `-`、`=` 会变成 F1-F12。

| 物理键 | 输出 |
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

Fn 也支持触控板蓄力。例如你可以先蓄力 F4，再蓄力 Alt，最后一起释放成 Alt+F4。

## 配置文件

配置文件是根目录下的 `shikipad.json`。修改后保存并重启 `ShikiPad.exe` 即可生效。

当前默认值：

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
  "layerTakeoverWindowMs": 35,
  "actionLayerSwitchGuardMs": 120,
  "comboLayerWindowMs": 100,
  "useScanCode": true,
  "scrollSlowIntervalMs": 100,
  "scrollFastIntervalMs": 20,
  "r3FreezeMs": 60
}
```

### 延迟和连发参数

| 参数 | 默认值 | 说明 |
|---|---:|---|
| `actionLayerGraceMs` | 80 | 动作键按下后的层确认窗口。第一个肩键/扳机层最多能认领 80ms 前按下、仍未落定且仍按着的基础动作键；快速打字掉回 Space/Tab 时可以略微增大，觉得字符输出太慢时可以略微减小。 |
| `layerTakeoverWindowMs` | 35 | 后按下的肩键/扳机层向前接管动作键的回看窗口。已有非基础 pending 层时，只认领它前 35ms 内刚按下的动作键；保持它不大于 `actionLayerGraceMs`，可以避免上一枚动作键被新 R1/RB 抢走。 |
| `actionLayerSwitchGuardMs` | 120 | 非基础层切换路径的第二层保险。正常字符输入主要由“虚拟 tap + 松开前抑制”保护。 |
| `comboLayerWindowMs` | 100 | R1/R2 或 L1/L2 被视为组合层的最大间隔。想更容易按出组合层就调大；切换层时旧扳机/肩键松慢仍误进组合层就调小。 |
| `repeatDelayMs` | 180 | 基础层按住多久后开始连发。 |
| `baseRepeatSlowIntervalMs` | 160 | 基础层刚开始连发时的间隔。 |
| `repeatIntervalMs` | 20 | 基础层连发加速后的最快间隔。 |
| `baseRepeatRampMs` | 1200 | 从慢速连发平滑过渡到快速连发所需时间。 |
| `scrollSlowIntervalMs` | 100 | 左摇杆滚轮方向刚触发时的滚动间隔。 |
| `scrollFastIntervalMs` | 20 | 左摇杆滚轮方向大幅推动时的最快滚动间隔。 |
| `r3FreezeMs` | 60 | R3 右键后短暂冻结鼠标移动，防止右键时指针漂移。 |

如果要调肩键/扳机组合层的判定时间，修改 `shikipad.json` 里的 `comboLayerWindowMs`，保存后重启 ShikiPad。比较实用的范围是 70-140 ms：调小更利于快速切换单层，调大更容易按出组合层。

如果要调“第一个肩键/扳机认领基础动作键”的时间，改 `actionLayerGraceMs`；如果要调“后按下的肩键/扳机向前抢已有非基础动作键”的时间，改 `layerTakeoverWindowMs`。这两个值都可以直接在 `shikipad.json` 里改，保存后重启生效；`layerTakeoverWindowMs` 必须大于等于 0 且不能大于 `actionLayerGraceMs`，否则启动读取配置时会回退到安全值。

### 摇杆和扳机参数

| 参数 | 默认值 | 说明 |
|---|---:|---|
| `rightStickCurveExponent` | 2.2 | 右摇杆鼠标曲线指数。越大，小幅移动越细，满幅仍然很快。 |
| `mouseMaxSpeed` | 28 | 鼠标最大速度比例。 |
| `rightStickEpsilon` | 0.002 | 右摇杆极小抖动过滤。 |
| `leftStickEnterDeadzone` | 0.5 | 左摇杆进入方向所需半径。 |
| `leftStickExitDeadzone` | 0.35 | 左摇杆释放方向所需半径。 |
| `triggerPressThreshold` | 0.35 | L2/R2 被视为按下的阈值。 |
| `triggerReleaseThreshold` | 0.25 | L2/R2 被视为松开的阈值。必须小于按下阈值，用来防抖。 |

## 如果要改代码，去哪里改

普通用户优先改 `shikipad.json`，不需要重新编译。

如果要改键位或状态机，主要看 [src/ShikiPad.cs](src/ShikiPad.cs)：

如果要在代码里改默认时间值，改 `Config` 类里的这两行，然后重新编译：

```csharp
public int ActionLayerGraceMs = 80;
public int LayerTakeoverWindowMs = 35;
```

| 想改的内容 | 代码位置 |
|---|---|
| 配置项默认值 | `Config` 类 |
| 配置文件读取和保存 | `Config.Load` / `Config.Save` |
| 各层键位表 | `MappingEngine` 构造函数 |
| 肩键/扳机层优先级 | `MappingEngine.Resolve` |
| 基础动作键被第一个肩键/扳机认领的窗口 | `Config.ActionLayerGraceMs`；运行时由 `MapperForm.UpdateActionButtons` 的 pending 落定逻辑使用 |
| 后按肩键/扳机向前接管已有非基础动作键的窗口 | `Config.LayerTakeoverWindowMs`；由 `MapperForm.UpdatePendingLayer` / `MapperForm.ResolvePendingLayer` 使用，并在 `Config.Load` 里校验不能大于 `actionLayerGraceMs` |
| 组合层判定时间 | `shikipad.json` 里的 `comboLayerWindowMs`，由 `MappingEngine.Resolve` 使用 |
| 启动时手柄型号选择 | `Program.SelectControllerProfile` / `DirectHidController` 构造函数 |
| DualSense Direct HID 解析 | `DirectHidController.ParseReport` |
| Xbox XInput 解析 | `DirectHidController.XInputLoop` / `DirectHidController.ParseXInput` |
| 方向键和图案键状态机 | `MapperForm.UpdateActionButtons` |
| 字符层虚拟 tap | `TapActionKey` |
| 基础层按住和连发 | `PressActionKey` / `UpdateBaseRepeat` / `BaseRepeatIntervalMs` |
| 左摇杆 8 方向 | `GetLeftStickKey` / `UpdateLeftStick` / `Sector` |
| Fn 转 F1-F12 | `TranslateToFKey` / `ApplyFnLayer` / `ActivateFnKey` |
| 右摇杆鼠标 | `UpdateRightStick` |
| 关闭时输入释放 | `InputInjector.ReleaseAll` / `Program.RegisterShutdownRelease` / `MapperForm.OnFormClosing` |
| 启动终端界面 | `Program.PrintGradientBanner` 和相关 `Write...` 函数 |

键位表在 `MappingEngine` 构造函数里：

- 基础层：`_tables[(int)Layer.Base]`
- R1：`_tables[(int)Layer.R1]`
- L1：`_tables[(int)Layer.L1]`
- R2：`_tables[(int)Layer.R2]`
- L2：`_tables[(int)Layer.L2]`
- R1+R2：`_tables[(int)Layer.R1R2]`
- L1+L2：`_tables[(int)Layer.L1L2]`

重新编译命令：

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /utf8output /platform:x64 /target:exe /out:ShikiPad.exe /win32icon:shiki.ico /win32manifest:ShikiPad.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\ShikiPad.cs
```

## UAC 和管理员权限

`ShikiPad.manifest` 里已经设置了：

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

也就是说 ShikiPad 启动时会请求管理员权限。

需要注意：

- 如果 UAC 弹窗出现时 ShikiPad 还没有启动，它无法控制这个弹窗。
- 如果 Windows 把 UAC 放到受保护桌面，系统可能会阻止模拟输入。
- 把 UAC 调到“不变暗桌面”可以减少安全桌面隔离，但不能让一个还没启动或还没提权的程序控制自己的 UAC 弹窗。

推荐方式：

1. 先启动 `ShikiPad.exe`。
2. 必要时用真实键鼠确认 ShikiPad 自己的 UAC。
3. 让 ShikiPad 保持管理员运行。
4. 之后它可以控制大多数普通窗口和管理员窗口。

## HidHide

如果游戏同时识别到实体手柄和 ShikiPad 的键鼠输入，会出现双输入。推荐使用 HidHide：

1. 安装 HidHide。
2. 隐藏 DualSense 手柄。
3. 在 HidHide 的应用白名单里加入 `ShikiPad.exe`。
4. 重启 ShikiPad。

## 测试命令

```powershell
.\ShikiPad.exe --layer-test
.\ShikiPad.exe --left-stick-test
.\ShikiPad.exe --mouse-test
```

测试重点：

- 按键顺序必须是 `Up, Right, Square, Triangle, Left, Down, Cross, Circle`。
- `R1+R2` 和 `L1+L2` 必须正确解析。
- 左摇杆右上必须是 Fn。
- 左摇杆上/下必须是滚轮。

## 最终版特点

- 字符层是虚拟 tap。
- 基础层是真实按住和渐进连发。
- 组合层不会在动作键未松开时回落到单层。
- 动作键松开后不会继续捕获下一组肩键/扳机。
- 程序关闭路径会自动释放 ShikiPad 按住的输入，不需要终端退出命令。
- 启动界面仍然是纯终端字符艺术，会按系统语言显示中文或英文，ShikiPad 软件名保持英文；实心填充、立体挤出的 ANSI 字标从 Spring mint 到 Summer aqua，再经过不单独标注的中间偏右 Solar gold 光桥，进入 Autumn ember 和纯白 Winter frost。
