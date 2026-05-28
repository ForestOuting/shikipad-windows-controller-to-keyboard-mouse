# ShikiPad

(✿◠‿◠) Welcome to ShikiPad! (✿◠‿◠)

ShikiPad 是一款专为 PlayStation 5 (DualSense) 手柄设计的 Windows 键盘/鼠标映射工具，基于纯正的 Direct HID (底层设备读写) 技术开发，能够穿透各种系统层的双重输入封锁（如 HidHide），提供极致纯净、精准的键鼠控制体验。

## 🌟 核心特性
1. **纯净的 USB 协议直连**：硬编码解析 DualSense 极速 USB 0x01 报文，零延迟。
2. **工业级边沿触发状态机**：彻底根除 SendInput 容易导致的按键卡死（Ghosting）和高频重复发送堵死队列的问题，同时包含科学的打字机连发（Typematic Repeat）引擎。
3. **8 向分层修饰盘 (Left Stick)**：左摇杆化身为 Shift / Ctrl / Alt / Win 等 8 向组合修饰键，满足所有快捷键组合。
4. **动态手柄切层与后发制人逻辑 (L1/R1/L2/R2)**：
   - 独创 **"Modifier Stack (修饰键栈)"** 判定：当按住多个肩键时，最新的肩键会覆盖旧的肩键输出；松开新肩键会自动回退到旧肩键层。
   - **零延迟字母触发**：对于基础层享有 60ms 的防抢按缓冲以及 100ms 的防拖按收尾静默，但当切入字母层时，防抢/防拖按延迟会被瞬间短路为 0，实现闪电般的急速打字体验！

---

## 🚀 最终定稿版默认键位表

- **摇杆与扳机基础操作**
  - **左摇杆**：推向 8 个方向时，按住键盘上的修饰键（左=Ctrl，上=Win，右=Alt，下=Shift）。
  - **右摇杆**：鼠标移动。基于高级非线性曲线算法，微调精准，外延迅速。
  - **L3 / R3 (摇杆下按)**：L3 对应鼠标左键，R3 对应鼠标右键。

- **字母与符号映射组合 (以十字键和图案键为基础)**

| 按键方向/组合 | Base (无肩键) | R1 | L1 | R2 | L2 | R1+R2 | L1+L2 |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **↑** (Up) | ⬆️ | I | S | M | K | 7 | ' (单引号) |
| **→** (Right) | ➡️ | N | R | W | V | 8 | / (斜杠) |
| **□** (Square) | Space | E | D | J | 1 | 9 | ; (分号) |
| **△** (Triangle) | Backspace | A | G | X | 2 | 0 | [ (左方括号) |
| **←** (Left) | ⬅️ | O | L | Q | 3 | , (逗号) | ] (右方括号) |
| **↓** (Down) | ⬇️ | T | C | F | 4 | . (句号) | \ (反斜杠) |
| **×** (Cross) | Enter | H | Y | P | 5 | - (减号) | ` (波浪号) |
| **○** (Circle) | Tab | U | Z | B | 6 | = (等号) | 空格 (Space) |

*(注意：在任意层按住一个图案键不放时，随时按下或松开其他肩键，输出的字母会瞬间无缝切换！)*

---

## 🎛️ 如何修改手柄死区 (Deadzone) 与灵敏度

所有的摇杆死区和扳机阈值都保存在根目录的配置文件 `shikipad.json` 中。程序第一次运行后会自动生成该文件。

直接用记事本打开 `shikipad.json`：
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

- **右摇杆鼠标手感 (`rightStickCurveExponent`)**：默认 `2.2`，数值越大，小幅度推摇杆时光标越慢越精确，推到底时光标越快。
- **左摇杆释放死区 (`leftStickExitDeadzone`)**：回弹到多小才松开修饰键（结合滞回区间防止手抖闪烁，默认 0.25）。
- **修改保存 `shikipad.json` 后，无需重新编译，关闭并重新打开 `ShikiPad.exe` 即刻生效。**