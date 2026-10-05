// 仅观察触发键、前台与光标位置；不拦截或注入按键。
using System;
using System.Runtime.InteropServices;

namespace EnglishCompanion {
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct GuiInfo {
            public int Size, Flags; public IntPtr Active, Focus, Capture, Menu, Move, Caret; public Rect CaretRect;
        }
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint process);
        internal static bool Down(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        [DllImport("imm32.dll")] static extern IntPtr ImmGetContext(IntPtr hwnd);
        [DllImport("imm32.dll")] static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr context);
        [DllImport("imm32.dll", CharSet=CharSet.Unicode)] static extern int ImmGetCompositionStringW(IntPtr context, uint index, IntPtr buffer, uint length);
        internal static bool Composing(IntPtr hwnd) {
            if (hwnd == IntPtr.Zero) return false;
            var context = ImmGetContext(hwnd); if (context == IntPtr.Zero) return false;
            try { return ImmGetCompositionStringW(context, 8, IntPtr.Zero, 0) > 0; }
            finally { ImmReleaseContext(hwnd, context); }
        }
    }
}
