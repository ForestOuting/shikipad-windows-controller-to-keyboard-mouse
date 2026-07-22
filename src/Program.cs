using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Program {

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private delegate bool ConsoleCtrlHandler(int ctrlType);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, bool add);

    private static ConsoleCtrlHandler _consoleCtrlHandler;
    private const string StartupTaskName = "ShikiPad";
    private const string RunMutexName = @"Local\ForestOuting.ShikiPad";
    public static void PrintGradientBanner() {
        EnableAnsi();
        PrintStartupSpinner();
    }

    public static void PrintRunHint() {
        EnableAnsi();
        Console.Write("\x1b[0m");
    }

    private static string[] BuildShikiPadBlockLogo() {
        return new string[] {
            BlockLogo(" ########  ##    ## #### ##   ##  #### #######  ####### ####### "),
            BlockLogo("##         ##    ##  ##  ##  ##    ##  ##    ## ##   ## ##    ##"),
            BlockLogo("##         ##    ##  ##  ## ##     ##  ##    ## ##   ## ##    ##"),
            BlockLogo(" #######   ########  ##  ####      ##  #######  ####### ##    ##"),
            BlockLogo("      ##   ##    ##  ##  ####      ##  ##       ##   ## ##    ##"),
            BlockLogo("       ##  ##    ##  ##  ## ##     ##  ##       ##   ## ##    ##"),
            BlockLogo("########   ##    ##  ##  ##  ##    ##  ##       ##   ## ##    ##"),
            BlockLogo(" ########  ##    ## #### ##   ##  #### ##       ##   ## ####### ")
        };
    }

    private static string BlockLogo(string pattern) {
        return pattern.Replace('#', '\u2588');
    }

    public static void PrintDetailedManual(Config config) {
        try { Console.Clear(); } catch { }
        EnableAnsi();
        int width = GetConsoleWidth();
        int panelWidth = Math.Min(116, Math.Max(72, width - 6));

        Console.WriteLine();
        WriteEmbossedCenteredText(width, panelWidth, "映 射 说 明", SeasonGlowStops(), true);
        Console.WriteLine();
        
        int blockWidth = 76;
        int indent = Math.Max(0, (panelWidth - blockWidth) / 2);
        string pad = new string(' ', indent);

        WriteManualSingleLayer(width, panelWidth, pad, blockWidth, "基础层", "↑", "→", "Tab", "Esc", "←", "↓", "Space", "Enter");
        WriteManualLayerPair(width, panelWidth, pad, blockWidth, "R1/RB", "o", "p", "j", "i", "n", "m", "k", "l",
            "L1/LB", "w", "d", "q", "e", "a", "s", "z", "x");
        WriteManualLayerPair(width, panelWidth, pad, blockWidth, "R2/RT", "0", "g", "y", "u", "-", "=", "b", "h",
            "L2/LT", "r", "f", "t", "1", "c", "v", "3", "2");
        WriteManualLayerPair(width, panelWidth, pad, blockWidth, "R1+L1", "4", ",", ".", "7", "5", "6", "9", "8",
            "R2+L2", "+", "/", "&", "*", "_", "^", "$", "%");
        WriteManualLayerPair(width, panelWidth, pad, blockWidth, "R1+L2", "(", ")", ";", "'", "<", ">", "`", "\\",
            "L1+R2", "[", "]", "!", "?", "{", "}", "@", "#");
        WriteManualGradientLine(width, panelWidth, pad + "Shift       ;→:  '→\"  `→~  \\→|", blockWidth);
        Console.WriteLine();
        WriteManualGradientLine(width, panelWidth, pad + "右摇杆      鼠标移动; L3 左键; R3 右键", blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + "左摇杆      ↖ Shift; ↑/↓ 滚轮; ↗ Win; ↙ Ctrl; ↘ Alt", blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + "蓄力        Home 短按锁定 / 长按保持; 锁定瞬间有修饰才被动作键消费", blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + "触控板中区  按压 -> 真实 CapsLock", blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + "触控板手势  起点四段判区，最终左区 / 右区; 单指 / 二指", blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + "静音键      短按 Caps/Fn 开/关; 长按禁用 / 启用", blockWidth);
        Console.WriteLine();
        WriteManualGradientLine(width, panelWidth, pad + "Caps/Fn     1..0/-/= -> F1..F12; 字母 -> 大写", blockWidth);
        
        Console.WriteLine();
        WriteEmbossedCenteredText(width, panelWidth, "Enter 主界面   |   Esc 关闭软件", SeasonGlowStops(), false);
        Console.WriteLine("\x1b[0m");
    }

    private static void WriteManualSingleLayer(int width, int panelWidth, string pad, int blockWidth,
        string title, string up, string right, string square, string triangle, string left, string down, string cross, string circle) {
        string spacing = new string(' ', 20);
        WriteManualGradientLine(width, panelWidth, pad + spacing + LayerDiagramLine(0, title, up, right, square, triangle, left, down, cross, circle) + spacing, blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + spacing + LayerDiagramLine(1, title, up, right, square, triangle, left, down, cross, circle) + spacing, blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + spacing + LayerDiagramLine(2, title, up, right, square, triangle, left, down, cross, circle) + spacing, blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + spacing + LayerDiagramLine(3, title, up, right, square, triangle, left, down, cross, circle) + spacing, blockWidth);
    }

    private static void WriteManualLayerPair(int width, int panelWidth, string pad, int blockWidth,
        string leftTitle, string leftUp, string leftRight, string leftSquare, string leftTriangle, string leftLeft, string leftDown, string leftCross, string leftCircle,
        string rightTitle, string rightUp, string rightRight, string rightSquare, string rightTriangle, string rightLeft, string rightDown, string rightCross, string rightCircle) {
        WriteManualGradientLine(width, panelWidth, pad + LayerDiagramLine(0, leftTitle, leftUp, leftRight, leftSquare, leftTriangle, leftLeft, leftDown, leftCross, leftCircle) + "    " +
            LayerDiagramLine(0, rightTitle, rightUp, rightRight, rightSquare, rightTriangle, rightLeft, rightDown, rightCross, rightCircle), blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + LayerDiagramLine(1, leftTitle, leftUp, leftRight, leftSquare, leftTriangle, leftLeft, leftDown, leftCross, leftCircle) + "    " +
            LayerDiagramLine(1, rightTitle, rightUp, rightRight, rightSquare, rightTriangle, rightLeft, rightDown, rightCross, rightCircle), blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + LayerDiagramLine(2, leftTitle, leftUp, leftRight, leftSquare, leftTriangle, leftLeft, leftDown, leftCross, leftCircle) + "    " +
            LayerDiagramLine(2, rightTitle, rightUp, rightRight, rightSquare, rightTriangle, rightLeft, rightDown, rightCross, rightCircle), blockWidth);
        WriteManualGradientLine(width, panelWidth, pad + LayerDiagramLine(3, leftTitle, leftUp, leftRight, leftSquare, leftTriangle, leftLeft, leftDown, leftCross, leftCircle) + "    " +
            LayerDiagramLine(3, rightTitle, rightUp, rightRight, rightSquare, rightTriangle, rightLeft, rightDown, rightCross, rightCircle), blockWidth);
    }

    private static string LayerDiagramLine(int row, string title, string up, string right, string square, string triangle, string left, string down, string cross, string circle) {
        const int diagramWidth = 36;
        if (String.IsNullOrEmpty(title)) return new string(' ', diagramWidth);
        string[] line = new string[diagramWidth];
        for (int i = 0; i < diagramWidth; i++) line[i] = " ";
        switch (row) {
            case 0:
                PutCentered(line, 0, 36, title);
                break;
            case 1:
                PutCentered(line, 5, 5, up);
                PutCentered(line, 26, 5, triangle);
                break;
            case 2:
                PutCentered(line, 0, 5, left);
                PutCentered(line, 10, 5, right);
                PutCentered(line, 21, 5, square);
                PutCentered(line, 31, 5, circle);
                break;
            case 3:
                PutCentered(line, 5, 5, down);
                PutCentered(line, 26, 5, cross);
                break;
        }
        return string.Join("", line);
    }

    private static void PutCentered(string[] line, int start, int width, string text) {
        if (String.IsNullOrEmpty(text) || start >= line.Length || width <= 0) return;
        text = TrimToWidth(text, width);
        int textWidth = DisplayWidth(text);
        int offset = Math.Max(0, (width - textWidth) / 2);
        int col = Math.Max(0, start + offset);
        for (int i = 0; i < text.Length && col < line.Length; i++) {
            char c = text[i];
            int cw = CharDisplayWidth(c);
            line[col++] = c.ToString();
            for (int w = 1; w < cw && col < line.Length; w++) {
                line[col++] = "";
            }
        }
    }

    public static void PrintConnectedWelcome(Config config, string deviceName) {
        PrintHomeSurface(config, deviceName, true, false);
    }

    public static void PrintRunningHome(Config config, string deviceName, bool connected) {
        PrintHomeSurface(config, deviceName, connected, false);
    }

    private static void PrintHomeSurface(Config config, string deviceName, bool connected, bool pageBreak) {
        EnableAnsi();
        if (pageBreak) {
            WritePageBreak();
        } else {
            try { Console.Clear(); } catch { }
        }

        int width = GetConsoleWidth();
        int panelWidth = Math.Min(118, Math.Max(72, width - 6));
        Console.WriteLine();
        WriteNeonRule(width, panelWidth, "ShikiPad");
        WriteExtrudedLogo(width, BuildShikiPadBlockLogo(), SeasonFlowStops());
        WriteEmbossedCenteredText(width, panelWidth, "手 柄 键 鼠 映 射", SeasonGlowStops(), true);
        Console.WriteLine();
        WriteEmbossedCenteredText(width, panelWidth, "Enter 映射说明   |   Esc 关闭软件   |   关闭窗口释放按键", SeasonGlowStops(), false);
        Console.WriteLine("\x1b[0m");
    }

    private static void WriteManualGradientLine(int width, int panelWidth, string text, int blockWidth) {
        int left = Math.Max(0, (width - panelWidth) / 2);
        Console.Write(new string(' ', left));
        
        Rgb[] stops = SeasonFlowStops();
        int currentDisplayCol = left;
        int logoWidth = 64;
        int logoLeft = Math.Max(0, (width - 67) / 2);
        
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            int col = currentDisplayCol - logoLeft;
            double t = Math.Max(0.0, Math.Min(col, logoWidth - 1)) / (double)(logoWidth - 1);
            WriteRgb(GradientAt(stops, t), c.ToString());
            currentDisplayCol += CharDisplayWidth(c);
        }
        Console.WriteLine();
    }

    private static void WritePageBreak() {
        int lines = 28;
        try {
            lines = Math.Max(28, Console.WindowHeight + 6);
        } catch { }
        for (int i = 0; i < lines; i++) Console.WriteLine();
    }

    private static void PrintStartupSpinner() {
        try { Console.Clear(); } catch { }
        EnableAnsi();
        int width = GetConsoleWidth();
        int panelWidth = Math.Min(98, Math.Max(66, width - 8));
        Console.WriteLine();
        WriteNeonRule(width, panelWidth, "ShikiPad 正在启动");
        WriteExtrudedLogo(width, BuildShikiPadBlockLogo(), SeasonFlowStops());
        WriteEmbossedCenteredText(width, panelWidth, "正在连接手柄", SeasonGlowStops(), false);
        Console.WriteLine();
    }

    private static void PrintFatalStartupError(string message) {
        try { Console.Clear(); } catch { }
        EnableAnsi();
        int width = GetConsoleWidth();
        int panelWidth = Math.Min(104, Math.Max(66, width - 6));
        Console.WriteLine();
        WriteNeonRule(width, panelWidth, "ShikiPad 无法启动");
        WritePanelBorder(width, panelWidth, true, new Rgb(255, 148, 82));
        WritePanelTitle(width, panelWidth, "Interception 驱动不可用", new Rgb(255, 215, 92));
        WritePanelSeparator(width, panelWidth, new Rgb(74, 94, 106));
        WritePanelWrappedLine(width, panelWidth, "  原因", message, SeasonAutumn(), new Rgb(245, 250, 255));
        WritePanelWrappedLine(width, panelWidth, "  处理", "请安装 Interception 驱动, 重启 Windows, 并以管理员权限运行 ShikiPad. 当前版本不会回退到 SendInput.", SeasonGold(), new Rgb(245, 250, 255));
        WritePanelBorder(width, panelWidth, false, new Rgb(255, 148, 82));
        Console.WriteLine("\x1b[0m");
    }

    private static void WritePanelWrappedLine(int width, int panelWidth, string label, string value, Rgb labelColor, Rgb valueColor) {
        int inner = panelWidth - 2;
        int labelWidth = Math.Min(28, Math.Max(18, inner / 3));
        int valueWidth = inner - labelWidth - 3;
        string[] lines = WrapToWidth(value, valueWidth);
        if (lines.Length == 0) lines = new string[] { "" };
        for (int i = 0; i < lines.Length; i++) {
            WritePanelLine(width, panelWidth, i == 0 ? label : "  ", lines[i], labelColor, valueColor);
        }
    }

    private static string[] WrapToWidth(string text, int width) {
        List<string> lines = new List<string>();
        if (String.IsNullOrEmpty(text)) return lines.ToArray();
        StringBuilder current = new StringBuilder();
        int used = 0;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == '\r') continue;
            if (c == '\n') {
                lines.Add(current.ToString());
                current.Length = 0;
                used = 0;
                continue;
            }
            int cw = CharDisplayWidth(c);
            if (used > 0 && used + cw > width) {
                lines.Add(current.ToString().TrimEnd());
                current.Length = 0;
                used = 0;
                if (c == ' ') continue;
            }
            current.Append(c);
            used += cw;
        }
        if (current.Length > 0) lines.Add(current.ToString().TrimEnd());
        return lines.ToArray();
    }

    private static bool IsChineseUi() {
        return true;
    }

    private static void EnableAnsi() {
        try {
            IntPtr handle = GetStdHandle(-11);
            uint mode;
            if (GetConsoleMode(handle, out mode)) {
                SetConsoleMode(handle, mode | 0x0004 | 0x0008);
            }
        } catch { }

        try {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        } catch { }
    }

    private static int GetConsoleWidth() {
        int width = 88;
        try { width = Console.WindowWidth; } catch { }
        if (width < 64) width = 64;
        if (width > 160) width = 160;
        return width;
    }

    private static string CenterLine(int width, string text) {
        int textWidth = DisplayWidth(text);
        if (textWidth >= width) return TrimToWidth(text, width);
        int left = (width - textWidth) / 2;
        return new string(' ', left) + text + new string(' ', width - left - textWidth);
    }

    private struct Rgb {
        public int R;
        public int G;
        public int B;

        public Rgb(int r, int g, int b) {
            R = r;
            G = g;
            B = b;
        }
    }

    private static void WriteRgb(Rgb color, string text) {
        Console.Write(string.Format("\x1b[38;2;{0};{1};{2}m{3}", color.R, color.G, color.B, text));
    }

    private static Rgb Mix(Rgb a, Rgb b, double t) {
        return new Rgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    private static Rgb Scale(Rgb color, double amount) {
        return new Rgb(
            ClampColor(color.R * amount),
            ClampColor(color.G * amount),
            ClampColor(color.B * amount));
    }

    private static int ClampColor(double value) {
        if (value < 0.0) return 0;
        if (value > 255.0) return 255;
        return (int)value;
    }

    private static Rgb GradientAt(Rgb[] stops, double t) {
        if (t <= 0.0) return stops[0];
        if (t >= 1.0) return stops[stops.Length - 1];
        double scaled = t * (stops.Length - 1);
        int segment = (int)scaled;
        if (segment >= stops.Length - 1) segment = stops.Length - 2;
        return Mix(stops[segment], stops[segment + 1], scaled - segment);
    }

    private static Rgb[] SeasonFlowStops() {
        return new Rgb[] {
            SeasonSpring(),
            SeasonSummer(),
            SeasonGold(),
            SeasonAutumn(),
            SeasonWinter()
        };
    }

    private static Rgb[] SeasonGlowStops() {
        return new Rgb[] {
            new Rgb(72, 255, 202),
            new Rgb(91, 226, 255),
            SeasonGold(),
            new Rgb(255, 163, 102),
            new Rgb(244, 252, 255)
        };
    }

    private static Rgb SeasonSpring() { return new Rgb(94, 255, 197); }
    private static Rgb SeasonSummer() { return new Rgb(91, 226, 255); }
    private static Rgb SeasonGold() { return new Rgb(255, 215, 92); }
    private static Rgb SeasonAutumn() { return new Rgb(255, 148, 82); }
    private static Rgb SeasonWinter() { return new Rgb(255, 255, 255); }
    private static void WriteExtrudedLogo(int width, string[] logo, Rgb[] stops) {
        int logoWidth = 0;
        for (int row = 0; row < logo.Length; row++) if (logo[row].Length > logoWidth) logoWidth = logo[row].Length;
        int shadowX = 3;
        int shadowY = 2;
        int outputWidth = logoWidth + shadowX;
        int left = Math.Max(0, (width - outputWidth) / 2);

        for (int row = 0; row < logo.Length + shadowY; row++) {
            Console.Write(new string(' ', left));
            Console.Write("\x1b[1m");
            for (int col = 0; col < outputWidth; col++) {
                bool main = IsLogoPixel(logo, row, col);
                bool nearShadow = IsLogoPixel(logo, row - 1, col - 2);
                bool farShadow = IsLogoPixel(logo, row - shadowY, col - shadowX);
                double t = logoWidth <= 1 ? 1.0 : (double)Math.Max(0, Math.Min(col, logoWidth - 1)) / (double)(logoWidth - 1);
                Rgb baseColor = GradientAt(stops, t);

                if (main) {
                    double rowT = logo.Length <= 1 ? 0.0 : (double)row / (double)(logo.Length - 1);
                    Rgb face = LogoFaceColor(baseColor, rowT);
                    WriteRgb(face, "\u2588");
                } else if (nearShadow) {
                    WriteRgb(Scale(baseColor, 0.40), "\u2593");
                } else if (farShadow) {
                    WriteRgb(Scale(baseColor, 0.24), "\u2592");
                } else {
                    Console.Write(' ');
                }
            }
            Console.Write("\x1b[22m");
            Console.WriteLine();
        }
    }

    private static bool IsLogoPixel(string[] logo, int row, int col) {
        if (row < 0 || row >= logo.Length) return false;
        if (col < 0 || col >= logo[row].Length) return false;
        return logo[row][col] != ' ';
    }

    private static Rgb LogoFaceColor(Rgb baseColor, double rowT) {
        if (rowT < 0.22) return Mix(baseColor, new Rgb(255, 255, 255), 0.24);
        if (rowT > 0.72) return Scale(baseColor, 0.82);
        return baseColor;
    }

    private static void WriteNeonRule(int width, int panelWidth, string title) {
        string line = "\u2726\u2500\u2500 " + title + " " + new string('\u2500', Math.Max(0, panelWidth - title.Length - 7)) + "\u2726";
        WriteEmbossedCenteredText(width, panelWidth, line, SeasonGlowStops(), true);
    }

    private static void WriteGradientText(int absoluteX, int consoleWidth, string text, Rgb[] stops) {
        int logoWidth = 64;
        int logoLeft = Math.Max(0, (consoleWidth - 67) / 2);
        for (int i = 0; i < text.Length; i++) {
            int col = (absoluteX + i) - logoLeft;
            double t = Math.Max(0.0, Math.Min(col, logoWidth - 1)) / (double)(logoWidth - 1);
            WriteRgb(GradientAt(stops, t), text[i].ToString());
        }
    }

    private static void WriteEmbossedCenteredText(int width, int panelWidth, string text, Rgb[] stops, bool bold) {
        int left = (width - panelWidth) / 2;
        string line = CenterLine(panelWidth, text);
        Console.Write(new string(' ', left + 1));
        WriteGradientShadowGlyphs(left + 1, width, line, stops);
        Console.Write("\r");
        Console.Write(new string(' ', left));
        if (bold) Console.Write("\x1b[1m");
        WriteGradientText(left, width, line, stops);
        if (bold) Console.Write("\x1b[22m");
        Console.WriteLine();
    }

    private static void WriteGradientShadowGlyphs(int absoluteX, int consoleWidth, string text, Rgb[] stops) {
        int logoWidth = 64;
        int logoLeft = Math.Max(0, (consoleWidth - 67) / 2);
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == ' ') {
                Console.Write(' ');
            } else {
                int col = (absoluteX + i) - logoLeft;
                double t = Math.Max(0.0, Math.Min(col, logoWidth - 1)) / (double)(logoWidth - 1);
                WriteRgb(Scale(GradientAt(stops, t), 0.28), "\u2592");
            }
        }
    }

    private static void WritePanelBorder(int width, int panelWidth, bool top, Rgb color) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        string line = (top ? "\u256d" : "\u2570") + new string('\u2500', panelWidth - 2) + (top ? "\u256e" : "\u256f");
        WriteRgb(color, line);
        Console.WriteLine();
    }

    private static void WritePanelSeparator(int width, int panelWidth, Rgb color) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        WriteRgb(color, "\u2502" + new string('\u2504', panelWidth - 2) + "\u2502");
        Console.WriteLine();
    }

    private static void WritePanelTitle(int width, int panelWidth, string title, Rgb color) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        WriteRgb(color, CenterLine(panelWidth - 2, title));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        Console.WriteLine();
    }

    private static void WritePanelLine(int width, int panelWidth, string label, string value, Rgb labelColor, Rgb valueColor) {
        int left = (width - panelWidth) / 2;
        int inner = panelWidth - 2;
        int labelWidth = Math.Min(28, Math.Max(18, inner / 3));
        int valueWidth = inner - labelWidth - 3;
        if (DisplayWidth(value) > valueWidth) value = TrimToWidth(value, valueWidth);

        Console.Write(new string(' ', left));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        Console.Write("\x1b[1m");
        WriteRgb(labelColor, PadRight(label, labelWidth));
        Console.Write("\x1b[22m");
        WriteRgb(new Rgb(72, 91, 101), " \u2506 ");
        WriteRgb(valueColor, PadRight(value, valueWidth));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        Console.WriteLine();
    }

    private static string PadRight(string text, int width) {
        if (width <= 0) return "";
        int textWidth = DisplayWidth(text);
        if (textWidth >= width) return TrimToWidth(text, width);
        return text + new string(' ', width - textWidth);
    }

    private static string TrimToWidth(string text, int width) {
        if (width <= 0) return "";
        if (DisplayWidth(text) <= width) return text;
        if (width <= 1) return "\u2026";

        StringBuilder sb = new StringBuilder();
        int used = 0;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            int cw = CharDisplayWidth(c);
            if (used + cw > width - 1) break;
            sb.Append(c);
            used += cw;
        }
        sb.Append("\u2026");
        return sb.ToString();
    }

    private static int DisplayWidth(string text) {
        if (String.IsNullOrEmpty(text)) return 0;
        int width = 0;
        for (int i = 0; i < text.Length; i++) width += CharDisplayWidth(text[i]);
        return width;
    }

    private static int CharDisplayWidth(char c) {
        if (c >= 0x1100 &&
            (c <= 0x115F ||
             c == 0x2329 || c == 0x232A ||
             (c >= 0x2190 && c <= 0x2199) || // Arrows
             (c >= 0x2E80 && c <= 0xA4CF) ||
             (c >= 0xAC00 && c <= 0xD7A3) ||
             (c >= 0xF900 && c <= 0xFAFF) ||
             (c >= 0xFE10 && c <= 0xFE19) ||
             (c >= 0xFE30 && c <= 0xFE6F) ||
             (c >= 0xFF00 && c <= 0xFF60) ||
             (c >= 0xFFE0 && c <= 0xFFE6))) {
            return 2;
        }
        return 1;
    }

    private static bool _shutdownReleaseRegistered;
    private static int _shutdownReleaseStarted;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [STAThread]
    private static int Main(string[] args) {
        AllocConsole();
        StreamWriter writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(writer);
        try { Console.Title = "ShikiPad"; } catch { }
        EnableAnsi();

        string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Directory.SetCurrentDirectory(root);
        Config config = new Config();
        RegisterShutdownRelease();
        if (HasArg(args, "--list-devices") || HasArg(args, "--enum-hid")) {
            RunHidEnumTest();
            return 0;
        }

        if (HasArg(args, "--install-startup")) {
            return InstallStartupTask();
        }

        if (HasArg(args, "--uninstall-startup")) {
            return UninstallStartupTask();
        }

        if (HasArg(args, "--identity")) {
            Console.WriteLine("\n--- SHIKIPAD PROCESS IDENTITY ---");
            Console.WriteLine("Current process exe path: " + Process.GetCurrentProcess().MainModule.FileName);
            Console.WriteLine("Working directory: " + Environment.CurrentDirectory);
            Console.WriteLine("Command line: " + Environment.CommandLine);
            Console.WriteLine("Process ID: " + Process.GetCurrentProcess().Id);

            Console.WriteLine("Does this exact process read DualSense through Direct HID? YES");
            Console.WriteLine("Is any helper process used? NO");
            Console.WriteLine("\nAdd THIS EXACT path to HidHide Applications:");
            Console.WriteLine(Process.GetCurrentProcess().MainModule.FileName);
            return 0;
        }

        Mutex runMutex;
        bool ownsRunMutex;
        try {
            runMutex = new Mutex(true, RunMutexName, out ownsRunMutex);
        } catch (Exception ex) {
            PrintFatalStartupError("无法创建单实例锁：" + ex.Message);
            return 1;
        }
        if (!ownsRunMutex) {
            runMutex.Dispose();
            PrintFatalStartupError("ShikiPad 已经在当前 Windows 会话中运行。请先关闭已有实例。");
            return 2;
        }

        try {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            PrintStartupSpinner();

            PrintRunHint();
            MapperForm form;
            try {
                form = new MapperForm(config);
            } catch (Exception ex) {
                PrintFatalStartupError(ex.Message);
                return 1;
            }
            Application.Run(form);
            return 0;
        } finally {
            try { runMutex.ReleaseMutex(); } catch { }
            runMutex.Dispose();
        }
    }

    private static void RegisterShutdownRelease() {
        if (_shutdownReleaseRegistered) return;
        _shutdownReleaseRegistered = true;
        Application.ApplicationExit += delegate(object sender, EventArgs e) {
            ReleaseAllRuntimeInput();
        };
        AppDomain.CurrentDomain.ProcessExit += delegate(object sender, EventArgs e) {
            ReleaseAllRuntimeInput();
        };
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) {
            try {
                Application.Exit();
            } catch {
                ReleaseAllRuntimeInput();
            }
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) {
            ReleaseAllRuntimeInput();
        };

        _consoleCtrlHandler = delegate(int ctrlType) {
            ReleaseAllRuntimeInput();
            return false;
        };
        try {
            SetConsoleCtrlHandler(_consoleCtrlHandler, true);
        } catch {
        }
    }

    private static void ReleaseAllRuntimeInput() {
        if (Interlocked.Exchange(ref _shutdownReleaseStarted, 1) != 0) return;
        InputInjector.ReleaseAllRegistered();
        InterceptionDriver.Cleanup();
    }

    private static bool HasArg(string[] args, string value) {
        for (int i = 0; i < args.Length; i++) if (String.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static int InstallStartupTask() {
        string exePath = Process.GetCurrentProcess().MainModule.FileName;
        string taskRun = "\"" + exePath + "\"";
        string arguments = "/Create /F /TN " + QuoteArgument(StartupTaskName) +
            " /SC ONLOGON /RL HIGHEST /TR " + QuoteArgument(taskRun);

        int exitCode = RunSchtasks(arguments);
        if (exitCode == 0) {
            Console.WriteLine("Startup task installed: " + StartupTaskName);
            Console.WriteLine("Target: " + exePath);
        } else {
            Console.WriteLine("Failed to install startup task. Run this command from an elevated terminal.");
        }
        return exitCode;
    }

    private static int UninstallStartupTask() {
        string arguments = "/Delete /F /TN " + QuoteArgument(StartupTaskName);
        int exitCode = RunSchtasks(arguments);
        if (exitCode == 0) {
            Console.WriteLine("Startup task removed: " + StartupTaskName);
        } else {
            Console.WriteLine("Failed to remove startup task. Run this command from an elevated terminal.");
        }
        return exitCode;
    }

    private static int RunSchtasks(string arguments) {
        ProcessStartInfo info = new ProcessStartInfo();
        info.FileName = "schtasks.exe";
        info.Arguments = arguments;
        info.UseShellExecute = false;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using (Process process = Process.Start(info)) {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!String.IsNullOrWhiteSpace(output)) Console.WriteLine(output.TrimEnd());
            if (!String.IsNullOrWhiteSpace(error)) Console.Error.WriteLine(error.TrimEnd());
            return process.ExitCode;
        }
    }

    private static string QuoteArgument(string value) {
        if (String.IsNullOrEmpty(value)) return "\"\"";
        StringBuilder builder = new StringBuilder();
        builder.Append('"');
        int backslashes = 0;
        for (int i = 0; i < value.Length; i++) {
            char c = value[i];
            if (c == '\\') {
                backslashes++;
            } else if (c == '"') {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
            } else {
                builder.Append('\\', backslashes);
                backslashes = 0;
                builder.Append(c);
            }
        }
        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }

    private static void RunHidEnumTest() {
        Console.WriteLine("\n--- HID DEVICE ENUMERATION TEST ---");
        Console.WriteLine("This test bypasses RawInput and directly queries the OS for connected HID devices.");
        Console.WriteLine("It lists DualSense USB HID paths without opening device handles.\n");

        Guid hidGuid;
        NativeMethods.HidD_GetHidGuid(out hidGuid);

        IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (deviceInfoSet == new IntPtr(-1)) {
            Console.WriteLine("Failed to get device info set.");
            return;
        }

        NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
        interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

        uint index = 0;
        int dualSenseUsbPathCount = 0;

        while (NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData)) {
            index++;
            uint requiredSize = 0;
            NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

            if (requiredSize == 0) continue;

            IntPtr detailData = Marshal.AllocHGlobal((int)requiredSize);
            Marshal.WriteInt32(detailData, (IntPtr.Size == 8) ? 8 : (Marshal.SystemDefaultCharSize == 1 ? 5 : 6));

            if (NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailData, requiredSize, out requiredSize, IntPtr.Zero)) {
                string devicePath = Marshal.PtrToStringAuto(new IntPtr(detailData.ToInt64() + 4));
                string lowerPath = (devicePath ?? "").ToLowerInvariant();
                if (!IsDualSenseUsbPath(lowerPath)) {
                    Marshal.FreeHGlobal(detailData);
                    continue;
                }

                Console.WriteLine("FOUND DUALSENSE USB HID PATH:");
                Console.WriteLine("  Path: " + devicePath);
                Console.WriteLine();
                dualSenseUsbPathCount++;
            }
            Marshal.FreeHGlobal(detailData);
        }

        NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);

        Console.WriteLine("Total DualSense USB HID paths found: " + dualSenseUsbPathCount);
        if (dualSenseUsbPathCount > 0) {
            Console.WriteLine("\nSUCCESS: A wired DualSense USB HID path is visible to this process.");
        } else {
            Console.WriteLine("\nFAILED: No wired DualSense USB HID path visible. Either it is unplugged, or HidHide whitelist failed.");
        }
    }

    private static bool IsDualSenseUsbPath(string lowerPath) {
        if (String.IsNullOrEmpty(lowerPath)) return false;
        return lowerPath.Contains("vid_054c") &&
               (lowerPath.Contains("pid_0ce6") || lowerPath.Contains("pid_0df2"));
    }

}

