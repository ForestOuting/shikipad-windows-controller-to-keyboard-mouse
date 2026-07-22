using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

internal sealed class DirectHidController {
    private const ushort SonyVendorId = 0x054C;
    private const ushort DualSenseProductId = 0x0CE6;
    private const ushort DualSenseEdgeProductId = 0x0DF2;
    private const int HidpStatusSuccess = 0x110000;
    private const int TouchpadMaxX = 1919;
    private const int TouchpadMaxY = 1079;
    private const int TouchpadEdgeTolerance = 128;

    public volatile ControllerState State = new ControllerState();
    public event Action<ControllerState> StateUpdated;

    private Thread _thread;
    private volatile bool _running;
    private IntPtr _handle = IntPtr.Zero;
    private string _deviceName = "DualSense";
    private long _lastHidErrorLogMs = -10000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    public DirectHidController() {
        _deviceName = "DualSense / Direct HID (USB)";
    }

    public string DisplayName {
        get { return _deviceName; }
    }

    public void Start() {
        _running = true;
        _thread = new Thread(Loop);
        _thread.IsBackground = true;
        _thread.Priority = ThreadPriority.AboveNormal;
        _thread.Start();
    }

    public void Stop() {
        _running = false;
        if (_handle != IntPtr.Zero && _handle != new IntPtr(-1)) {
            NativeMethods.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
        if (_thread != null) {
            _thread.Join(500);
        }
    }

    private void Loop() {
        byte[] buffer = new byte[1024];
        while (_running) {
            if (_handle == IntPtr.Zero || _handle == new IntPtr(-1)) {
                State = new ControllerState();
                _handle = FindAndOpenDevice();
                if (_handle != IntPtr.Zero && _handle != new IntPtr(-1)) {
                    State = new ControllerState { Connected = true };
                } else {
                    Thread.Sleep(1000);
                    continue;
                }
            }

            uint bytesRead;
            if (ReadFile(_handle, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero)) {
                if (bytesRead > 0) {
                    byte[] report = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, report, 0, (int)bytesRead);
                    try {
                        if (ParseReport(report)) StateUpdated?.Invoke(State);
                    } catch (Exception ex) {
                        LogHidError("HID 报告处理失败：" + ex.GetType().Name + "：" + ex.Message);
                    }
                }
            } else {
                NativeMethods.CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    private IntPtr FindAndOpenDevice() {
        Guid hidGuid;
        NativeMethods.HidD_GetHidGuid(out hidGuid);

        IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (deviceInfoSet == new IntPtr(-1)) return IntPtr.Zero;

        NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
        interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

        IntPtr foundHandle = IntPtr.Zero;
        uint index = 0;

        while (NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData)) {
            index++;
            uint requiredSize = 0;
            NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
            if (requiredSize == 0) continue;

            IntPtr detailData = Marshal.AllocHGlobal((int)requiredSize);
            try {
                Marshal.WriteInt32(detailData, (IntPtr.Size == 8) ? 8 : (Marshal.SystemDefaultCharSize == 1 ? 5 : 6));
                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailData, requiredSize, out requiredSize, IntPtr.Zero)) {
                    continue;
                }

                string devicePath = Marshal.PtrToStringAuto(new IntPtr(detailData.ToInt64() + 4));
                if (!IsDualSenseUsbPath(devicePath)) continue;

                IntPtr handle = NativeMethods.CreateFile(devicePath, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (handle == new IntPtr(-1)) continue;

                bool keepHandle = false;
                try {
                    NativeMethods.HIDD_ATTRIBUTES attrs = new NativeMethods.HIDD_ATTRIBUTES();
                    attrs.Size = (uint)Marshal.SizeOf(attrs);
                    if (!NativeMethods.HidD_GetAttributes(handle, ref attrs)) continue;
                    if (!IsDualSenseProduct(attrs.VendorID, attrs.ProductID)) continue;

                    if (!IsGamepadCollection(handle)) continue;

                    _deviceName = ProductName(attrs.ProductID);
                    foundHandle = handle;
                    keepHandle = true;
                    break;
                } finally {
                    if (!keepHandle) NativeMethods.CloseHandle(handle);
                }
            } finally {
                Marshal.FreeHGlobal(detailData);
            }
        }

        NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        return foundHandle;
    }

    private static bool IsDualSenseProduct(ushort vendorId, ushort productId) {
        return vendorId == SonyVendorId &&
               (productId == DualSenseProductId || productId == DualSenseEdgeProductId);
    }

    private static bool IsDualSenseUsbPath(string devicePath) {
        if (String.IsNullOrEmpty(devicePath)) return false;
        string path = devicePath.ToLowerInvariant();
        return path.Contains("vid_054c") &&
               (path.Contains("pid_0ce6") || path.Contains("pid_0df2"));
    }

    private static bool IsGamepadCollection(IntPtr handle) {
        IntPtr preparsedData;
        if (!NativeMethods.HidD_GetPreparsedData(handle, out preparsedData)) return false;
        try {
            NativeMethods.HIDP_CAPS caps;
            if (NativeMethods.HidP_GetCaps(preparsedData, out caps) != HidpStatusSuccess) return false;
            if (caps.UsagePage != 1 || (caps.Usage != 4 && caps.Usage != 5)) return false;
            return true;
        } finally {
            NativeMethods.HidD_FreePreparsedData(preparsedData);
        }
    }

    private static string ProductName(ushort productId) {
        string name = productId == DualSenseEdgeProductId ? "DualSense Edge" : "DualSense";
        return name + " (PID 0x" + productId.ToString("X4", CultureInfo.InvariantCulture) + ")";
    }

    private bool ParseReport(byte[] r) {
        ControllerState s;
        if (!TryParseDualSenseReport(r, out s)) {
            LogHidError("忽略了无法解析的 DualSense HID 报告（长度 " + (r == null ? 0 : r.Length) + "）。");
            return false;
        }
        State = s;
        return true;
    }

    private void LogHidError(string message) {
        long now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastHidErrorLogMs) < 1000) return;
        Interlocked.Exchange(ref _lastHidErrorLogMs, now);
        try { Console.Error.WriteLine("[warn] " + message); } catch { }
    }

    internal static bool TryParseDualSenseReport(byte[] r, out ControllerState s) {
        s = null;
        if (r == null || r.Length < 8) return false;

        if (r[0] == 0x01) {
            if (r.Length >= 64) return TryParseDualSenseFullReport(r, 1, out s);
            return TryParseDualSenseSimpleReport(r, out s);
        }

        return false;
    }

    private static bool TryParseDualSenseSimpleReport(byte[] r, out ControllerState s) {
        s = null;
        if (r.Length < 8) return false;

        s = new ControllerState { Connected = true };
        s.LX = Axis(r[1]);
        s.LY = Axis(r[2]);
        s.RX = Axis(r[3]);
        s.RY = Axis(r[4]);

        FillDpadAndFace(s, r[5]);
        s.L2 = r.Length > 8 ? Trigger(r[8]) : 0.0;
        s.R2 = r.Length > 9 ? Trigger(r[9]) : 0.0;
        FillShoulderAndSystemButtons(s, r[6], r[7]);
        return true;
    }

    private static bool TryParseDualSenseFullReport(byte[] r, int offset, out ControllerState s) {
        s = null;
        if (offset < 0 || r.Length <= offset + 9) return false;

        s = new ControllerState { Connected = true };
        s.LX = Axis(r[offset + 0]);
        s.LY = Axis(r[offset + 1]);
        s.RX = Axis(r[offset + 2]);
        s.RY = Axis(r[offset + 3]);
        s.L2 = Trigger(r[offset + 4]);
        s.R2 = Trigger(r[offset + 5]);

        FillDpadAndFace(s, r[offset + 7]);
        FillShoulderAndSystemButtons(s, r[offset + 8], r[offset + 9]);

        if (!TryParseTouchPoints(s, r, offset + 32)) {
            TryParseTouchPoints(s, r, offset + 31);
        }
        return true;
    }

    private static void FillShoulderAndSystemButtons(ControllerState s, byte buttons1, byte buttons2) {
        s.L1 = (buttons1 & 0x01) != 0;
        s.R1 = (buttons1 & 0x02) != 0;
        s.L2 = Math.Max(s.L2, (buttons1 & 0x04) != 0 ? 1.0 : 0.0);
        s.R2 = Math.Max(s.R2, (buttons1 & 0x08) != 0 ? 1.0 : 0.0);
        s.Create = (buttons1 & 0x10) != 0;
        s.Options = (buttons1 & 0x20) != 0;
        s.L3 = (buttons1 & 0x40) != 0;
        s.R3 = (buttons1 & 0x80) != 0;

        s.Home = (buttons2 & 0x01) != 0;
        s.TouchClick = (buttons2 & 0x02) != 0;
        s.Mute = (buttons2 & 0x04) != 0;
    }

    private static bool TryParseTouchPoints(ControllerState s, byte[] r, int offset) {
        TouchPoint p1;
        TouchPoint p2;
        if (!TryReadTouchPair(r, offset, out p1, out p2)) return false;
        AssignTouchPoints(s, p1, p2);
        return true;
    }

    private static bool TryReadTouchPair(byte[] r, int offset, out TouchPoint p1, out TouchPoint p2) {
        p1 = new TouchPoint();
        p2 = new TouchPoint();
        if (!TryReadTouchPoint(r, offset, out p1)) return false;
        if (!TryReadTouchPoint(r, offset + 4, out p2)) return false;
        return true;
    }

    private static int ActiveTouchCount(TouchPoint p1, TouchPoint p2) {
        return (p1.Active ? 1 : 0) + (p2.Active ? 1 : 0);
    }

    private static void AssignTouchPoints(ControllerState s, TouchPoint p1, TouchPoint p2) {
        s.Touch1Active = p1.Active;
        s.Touch1Id = p1.Id;
        s.Touch1X = p1.X;
        s.Touch1Y = p1.Y;
        s.Touch2Active = p2.Active;
        s.Touch2Id = p2.Id;
        s.Touch2X = p2.X;
        s.Touch2Y = p2.Y;
        s.TouchCount = ActiveTouchCount(p1, p2);
    }

    private static bool TryReadTouchPoint(byte[] r, int offset, out TouchPoint point) {
        point = new TouchPoint();
        if (r == null || offset < 0 || r.Length <= offset + 3) return false;
        point.Active = (r[offset] & 0x80) == 0;
        point.Id = r[offset] & 0x7F;
        point.X = r[offset + 1] | ((r[offset + 2] & 0x0F) << 8);
        point.Y = ((r[offset + 2] >> 4) & 0x0F) | (r[offset + 3] << 4);
        if (point.Active && !TryNormalizeTouchPointBounds(ref point)) return false;
        return true;
    }

    private static bool TryNormalizeTouchPointBounds(ref TouchPoint point) {
        if (point.X <= TouchpadMaxX && point.Y <= TouchpadMaxY) return true;

        if (point.X <= TouchpadMaxX + TouchpadEdgeTolerance &&
            point.Y <= TouchpadMaxY + TouchpadEdgeTolerance) {
            point.Active = false;
            return true;
        }

        return false;
    }

    private struct TouchPoint {
        public bool Active;
        public int Id;
        public int X;
        public int Y;
    }

    private static void FillDpadAndFace(ControllerState s, byte b) {
        int d = b & 0x0F;
        s.Up = d == 0 || d == 1 || d == 7;
        s.Right = d == 1 || d == 2 || d == 3;
        s.Down = d == 3 || d == 4 || d == 5;
        s.Left = d == 5 || d == 6 || d == 7;
        s.Square = (b & 0x10) != 0;
        s.Cross = (b & 0x20) != 0;
        s.Circle = (b & 0x40) != 0;
        s.Triangle = (b & 0x80) != 0;
    }

    private static double Axis(byte value) {
        return Clamp(((double)value - 127.5) / 127.5, -1.0, 1.0);
    }

    private static double Trigger(byte value) {
        return Clamp((double)value / 255.0, 0.0, 1.0);
    }

    private static double Clamp(double value, double min, double max) {
        return value < min ? min : (value > max ? max : value);
    }
}
