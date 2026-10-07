// 托盘诊断：区分“注册失败”与“注册成功但取不到矩形”，并说明是哪一步。
// 只读，不改系统设置。
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EnglishCompanion {
    internal static class TrayDiagnostics {
        [STAThread] internal static int Main(string[] args) {
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            Console.WriteLine("UserInteractive = " + Environment.UserInteractive);
            Console.WriteLine("SessionId      = " + System.Diagnostics.Process.GetCurrentProcess().SessionId);
            var explorer = System.Diagnostics.Process.GetProcessesByName("explorer");
            Console.WriteLine("explorer procs = " + explorer.Length);
            foreach (var p in explorer) Console.WriteLine("   explorer pid=" + p.Id + " session=" + p.SessionId);
            var handle = AppIcon.Value.Handle;
            Console.WriteLine("AppIcon handle = " + handle + "  zero=" + (handle == IntPtr.Zero));

            // 直接用产品同一份结构与参数复现注册。
            var host = new Host();
            var data = MakeData(host.Handle, "语伴 · 诊断");
            bool added = Notify(0, ref data);
            Console.WriteLine("NIM_ADD (v0, guid)   = " + added);
            if (!added) {
                var legacy = MakeData(host.Handle, "语伴 · 诊断");
                legacy.Flags &= ~32u; legacy.Guid = Guid.Empty;
                added = Notify(1, ref legacy);
                Console.WriteLine("NIM_ADD (v1, no guid) = " + added);
            }
            data.Version = 4;
            Console.WriteLine("NIM_SETVERSION       = " + Notify(4, ref data));
            var id = MakeIdent(host.Handle, data.Guid);
            Console.WriteLine("GetRect              = " + GetRect(ref id));
            Console.WriteLine("Explorer running     = " + (explorer.Length > 0));
            Console.WriteLine("Session matches      = " + (System.Diagnostics.Process.GetCurrentProcess().SessionId == (explorer.Length > 0 ? explorer[0].SessionId : -1)));
            host.DestroyHandle();
            return 0;
        }
        class Host : NativeWindow {
            public Host() { CreateHandle(new CreateParams { Caption = "EnglishCompanion.TrayDiag", ExStyle = 0x80 }); }
            protected override void WndProc(ref Message m) { base.WndProc(ref m); }
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct Data {
            public int Size; public IntPtr Window; public uint Id, Flags, Callback; public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
            public uint InfoFlags; public Guid Guid; public IntPtr Balloon;
        }
        [StructLayout(LayoutKind.Sequential)] struct Ident { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint c, ref Data d);
        [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref Ident i, out RECT r);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
        static Data MakeData(IntPtr h, string tip) {
            return new Data { Size = Marshal.SizeOf(typeof(Data)), Window = h, Id = 1, Flags = 1 | 2 | 4 | 32 | 128, Callback = 0x8001, Icon = AppIcon.Value.Handle, Tip = tip, Guid = Guid.NewGuid(), Info = "", Title = "" };
        }
        static Ident MakeIdent(IntPtr h, Guid g) { return new Ident { Size = (uint)Marshal.SizeOf(typeof(Ident)), Window = h, Id = 1, Guid = g }; }
        static bool Notify(uint c, ref Data d) { return Shell_NotifyIcon(c, ref d); }
        static string GetRect(ref Ident i) { RECT r; return Shell_NotifyIconGetRect(ref i, out r).ToString(); }
    }
}
