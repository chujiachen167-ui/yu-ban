using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace EnglishCompanion {
    internal sealed class TrayIcon : NativeWindow, IDisposable {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Data {
            public int Size; public IntPtr Window; public uint Id, Flags, Callback; public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Tip;
            public uint State,StateMask;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string Title;
            public uint InfoFlags; public Guid Guid; public IntPtr Balloon;
        }
        [StructLayout(LayoutKind.Sequential)] struct Identifier { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint command,ref Data data);
        [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref Identifier id,out Native.Rect rect);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string text);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        readonly Guid guid=new Guid("dbf62e64-8559-4bcd-a0c6-e07b2c5c7382");
        readonly ContextMenuStrip menu; readonly Action open;
        readonly Timer retry=new Timer {Interval=3000}; readonly uint taskbar; Data data; bool disposed;
        internal bool Registered {get;private set;}
        internal TrayIcon(ContextMenuStrip menu,Action open,bool demo) {
            this.menu=menu; this.open=open;
            // Each preview/test instance has a separate registration. The real app
            // keeps its stable identity so Explorer remembers the user's preference.
            if(demo) guid=Guid.NewGuid();
            CreateHandle(new CreateParams {Caption="EnglishCompanion.TrayHost",ExStyle=0x80}); taskbar=RegisterWindowMessage("TaskbarCreated");
            data=new Data {Size=Marshal.SizeOf(typeof(Data)),Window=Handle,Id=1,Flags=1|2|4|32|128,Callback=0x8001,Icon=AppIcon.Value.Handle,Tip=demo?"语伴 · 演示":"语伴 · 点击打开设置",Guid=guid,Info="",Title=""};
            Register(); retry.Tick+=delegate { if(!disposed && !HasRectangle()) Register(); }; retry.Start();
        }
        void Register() {
            if(disposed)return;data.Version=0;Registered=Shell_NotifyIcon(0,ref data);if(!Registered)Registered=Shell_NotifyIcon(1,ref data);
            // Explorer binds unsigned GUID registrations to their first executable
            // path. A portable build moved by its owner must still get an icon.
            if(!Registered&&(data.Flags&32)!=0){data.Flags&=~32u;data.Guid=Guid.Empty;Registered=Shell_NotifyIcon(0,ref data);}
            data.Version=4;Shell_NotifyIcon(4,ref data);
        }
        internal bool HasRectangle() { var id=new Identifier {Size=(uint)Marshal.SizeOf(typeof(Identifier)),Window=Handle,Id=1,Guid=data.Guid}; Native.Rect rect; return Shell_NotifyIconGetRect(ref id,out rect)==0; }
        protected override void WndProc(ref Message m) {
            if((uint)m.Msg==taskbar) { Register(); }
            if(m.Msg==0x8001) {
                int action=(int)((long)m.LParam&0xffff);
                if(action==0x400 || action==0x401 || action==0x203) open();
                if(action==0x7b || action==0x205) { SetForegroundWindow(Handle); menu.Show(Cursor.Position); }
            }
            base.WndProc(ref m);
        }
        public void Dispose() { if(disposed)return; disposed=true; retry.Stop();retry.Dispose();Shell_NotifyIcon(2,ref data);menu.Dispose();DestroyHandle(); }
    }
}
