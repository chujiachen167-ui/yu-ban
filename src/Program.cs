// 无安装、无管理员权限、单实例；测试和探针均为独立进程模式。
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EnglishCompanion {
    internal static class Program {
        [STAThread] static int Main(string[] args) {
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch (EntryPointNotFoundException) { }
            if (Array.IndexOf(args, "--probe") >= 0) { Console.OutputEncoding = new UTF8Encoding(false); Probe.Run(); return 0; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--export-icon") { using (var file = System.IO.File.Create(args[1])) AppIcon.Value.Save(file); return 0; }
            if (Array.IndexOf(args, "--input-diag") >= 0) return InputDiagnostics.Run(args);
            if (Array.IndexOf(args, "--settings-preview") >= 0) { using (var preview = new SettingsWindow(new Configuration {Theme=PreviewSkin(args)}, true)) preview.ShowDialog(); return 0; }
            if (Array.IndexOf(args, "--fixture") >= 0) { Application.Run(new Fixture()); return 0; }
            if (Array.IndexOf(args, "--panel-preview") >= 0) { using(var overlay=new Overlay(true)){overlay.ApplySkin(PreviewSkin(args));overlay.ShowOriginal=Array.IndexOf(args,"--translation-only")<0;overlay.Preview(Array.IndexOf(args,"--short-text")>=0?"short":Array.IndexOf(args,"--long-text")>=0?"long":"");}return 0; }
            bool demo = Array.IndexOf(args, "--demo") >= 0;
            bool created;
            using (var mutex = new Mutex(true, "Local\\EnglishCompanion-v1" + (demo ? "-demo" : ""), out created)) {
                if (!created) {
                    Native.AllowSetForegroundWindow(UInt32.MaxValue);
                    try { using (var signal = EventWaitHandle.OpenExisting("Local\\EnglishCompanion-OpenSettings" + (demo ? "-demo" : ""))) signal.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("程序正在启动，请稍后再次点击。", "语伴"); }
                    return 0;
                }
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                Configuration config;
                try { config = Configuration.Load(); }
                catch { MessageBox.Show("配置无法读取，可能来自另一 Windows 账户。为保护原配置，本次没有启动。", "语伴"); return 1; }
                Application.Run(new Companion(config, demo, !demo));
            }
            return 0;
        }
        static string PreviewSkin(string[] args){return Array.IndexOf(args,"--skin-ocean")>=0?"ocean":Array.IndexOf(args,"--skin-baby")>=0?"baby":"glass";}
    }
}
