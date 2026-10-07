// UI Automation 在独立进程运行；异常提供程序由父进程超时回收。
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Web.Script.Serialization;

namespace EnglishCompanion {
    internal static class ProbeHost {
        [MTAThread] static int Main(string[] args) {
            try {
                Console.OutputEncoding = new System.Text.UTF8Encoding(false);
                if (Array.IndexOf(args, "--echo-test") >= 0) { Console.ReadLine(); Console.WriteLine("{\"Text\":\"IPC OK\"}"); return 0; }
                try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch (EntryPointNotFoundException) { }
                Probe.Run(); return 0;
            } catch (Exception e) { Console.Error.WriteLine(e.GetType().FullName + ":" + e.HResult); return 1; }
        }
    }
    internal static class Probe {
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 100000 };
        internal static void Run() {
            string line;
            while ((line = Console.ReadLine()) != null) {
                try { Console.WriteLine(Json.Serialize(Read(line == "selection"))); }
                catch (Exception e) { Console.WriteLine(Json.Serialize(new Snapshot { Reason = "这个输入框暂时无法读取（" + e.GetType().Name + "）" })); }
                Console.Out.Flush();
            }
        }
        internal static Snapshot Read(bool selected) {
            var s = new Snapshot();
            IntPtr foreground = Native.GetForegroundWindow(); uint pid;
            uint thread = Native.GetWindowThreadProcessId(foreground, out pid);
            s.Window = foreground.ToInt64();
            if (pid == 0) return s;
            using (var p = Process.GetProcessById((int)pid)) s.Process = p.ProcessName;
            string process = s.Process.ToLowerInvariant();
            if (process.Contains("keepass") || process.Contains("1password") || process.Contains("bitwarden") || process == "credentialuibroker" || process == "lockapp") {
                s.Protected = true; return s;
            }
            Native.Rect windowRect;
            if (Native.GetWindowRect(foreground, out windowRect)) {
                s.X = windowRect.Left + 30; s.Y = windowRect.Bottom - 70; s.Width = 1; s.Height = 20;
            }
            AutomationElement element = AutomationElement.FocusedElement;
            if (element == null || element.Current.ProcessId != (int)pid) { s.Reason = "未找到当前输入框"; return s; }
            if (element.Current.IsPassword) { s.Protected = true; return s; }
            s.Id = s.Window.ToString(CultureInfo.InvariantCulture) + ":" + String.Join("-", element.GetRuntimeId().Select(n => n.ToString(CultureInfo.InvariantCulture)));
            var bounds = element.Current.BoundingRectangle;
            if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0) {
                s.X = (int)bounds.Left; s.Y = (int)bounds.Top; s.Width = (int)bounds.Width; s.Height = (int)bounds.Height;
            }
            object pattern;
            TextPattern text = null;
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) text = (TextPattern)pattern;
            var type = element.Current.ControlType;
            bool input = type == ControlType.Edit || type == ControlType.Document;
            if (text != null) {
                var range = text.DocumentRange;
                object readOnly = range.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
                // A generic Document with an unknown read-only state may be a chat history/page.
                s.Editable = input && (readOnly is bool ? !(bool)readOnly : type == ControlType.Edit);
                if (s.Editable) s.Text = range.GetText(6001);
                var selection = text.GetSelection();
                if (selection.Length > 0) {
                    if (s.Editable) {
                        var prefix = range.Clone();
                        prefix.MoveEndpointByRange(System.Windows.Automation.Text.TextPatternRangeEndpoint.End, selection[0], System.Windows.Automation.Text.TextPatternRangeEndpoint.Start);
                        int start = prefix.GetText(6001).Length;
                        string selectedText = selection[0].GetText(6001);
                        if (start + selectedText.Length <= s.Text.Length) { s.SelectionStart = start; s.SelectionEnd = start + selectedText.Length; }
                    }
                    if (selected) s.Selection = selection[0].GetText(1801).Trim();
                    var boxes = selection[0].GetBoundingRectangles();
                    if (boxes.Length > 0) { s.X = (int)boxes[0].Left; s.Y = (int)boxes[0].Top; s.Width = Math.Max(1, (int)boxes[0].Width); s.Height = Math.Max(1, (int)boxes[0].Height); }
                }
            } else if (input && element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) {
                var value = (ValuePattern)pattern;
                s.Editable = !value.Current.IsReadOnly;
                if (s.Editable) s.Text = value.Current.Value;
            }
            var info = new Native.GuiInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.GuiInfo)) };
            bool hasInfo = Native.GetGUIThreadInfo(thread, ref info);
            if (hasInfo) s.Composing = Native.Composing(info.Focus);
            if (hasInfo && info.Caret != IntPtr.Zero) {
                var point = new Native.Point { X = info.CaretRect.Left, Y = info.CaretRect.Top };
                if (Native.ClientToScreen(info.Caret, ref point)) {
                    s.X = point.X; s.Y = point.Y; s.Width = 2; s.Height = Math.Max(16, info.CaretRect.Bottom - info.CaretRect.Top);
                }
            }
            if (s.Text.Length > 6000) { s.Text = ""; s.Editable = false; s.Reason = "内容较长，请选中本句后按 Ctrl+Shift+F8"; }
            if (s.Selection.Length > 1800) { s.Selection = ""; s.Reason = "一次请选中不超过 1800 字"; }
            if (!s.Editable && s.Reason == "") {
                // 自绘控件（微信、QQ 等）不向系统暴露可读接口，这里如实说明而不是笼统说“暂时无法读取”。
                s.Reason = SelfDrawn(process)
                    ? "这个应用的输入框不向 Windows 开放读取接口，所以读不到你打的字。点“粘贴翻译”一样能用：复制内容后在浮窗里粘贴即可。"
                    : "这个输入框暂时无法自动读取。可点击下方“粘贴翻译”，本提示会保留。";
            }
            if (Native.GetForegroundWindow() != foreground) return new Snapshot();
            return s;
        }
        // 这类应用自己绘制输入框，不注册 UIA 控件；换输入法或换应用即可恢复自动读取。
        static readonly string[] SelfDrawnApps = { "weixin", "wechat", "weixinapp", "qq", "tim", "qqnt" };
        internal static bool SelfDrawn(string process) {
            if (String.IsNullOrEmpty(process)) return false;
            process = process.ToLowerInvariant();
            foreach (var name in SelfDrawnApps) if (process.Contains(name)) return true;
            return false;
        }
    }
    internal sealed class ProbeClient : IDisposable {
        Process process;
        internal async Task<Snapshot> ReadAsync(bool selected) {
            try {
                if (process == null || process.HasExited) {
                    Dispose();
                    process = Process.Start(new ProcessStartInfo(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "CompanionProbe.exe")) {
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    });
                }
                process.StandardInput.WriteLine(selected ? "selection" : "focus"); process.StandardInput.Flush();
                Task<string> read = process.StandardOutput.ReadLineAsync();
                if (await Task.WhenAny(read, Task.Delay(1800)) != read) { Dispose(); return new Snapshot { Reason = "输入框响应超时，可按 Ctrl+Shift+F7 粘贴翻译" }; }
                string line = await read;
                if (line == null) throw new InvalidOperationException();
                return Probe.Json.Deserialize<Snapshot>(line);
            } catch (Exception e) { string kind = e.GetType().Name; if (process != null) { try { if (process.HasExited) kind += ":exit=" + process.ExitCode; } catch { } } Dispose(); return new Snapshot { Reason = "读取暂不可用（" + kind + "），可按 Ctrl+Shift+F7 粘贴翻译" }; }
        }
        public void Dispose() {
            if (process != null) { try { if (!process.HasExited) process.Kill(); } catch { } process.Dispose(); process = null; }
        }
    }
}
