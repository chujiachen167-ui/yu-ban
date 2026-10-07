// 输入框诊断：报告 UIA 在当前聚焦窗口里真正看到什么，用来判断某个输入框为什么读不到。
// 只读，不改任何东西，不发消息。
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace EnglishCompanion {
    internal static class InputDiagnostics {
        [STAThread] static int Main(string[] args) { return Run(args); }
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        [STAThread] internal static int Run(string[] args) {
            Console.OutputEncoding = new UTF8Encoding(false);            IntPtr fg = GetForegroundWindow();
            uint pid; GetWindowThreadProcessId(fg, out pid);
            string process = "?";
            try { process = Process.GetProcessById((int)pid).ProcessName; } catch { }
            Console.WriteLine("Foreground process: " + process + " (pid " + pid + ")");

            var el = AutomationElement.FocusedElement;
            if (el == null) { Console.WriteLine("FocusedElement: null"); return 1; }
            Console.WriteLine("ControlType : " + el.Current.ControlType.ProgrammaticName);
            Console.WriteLine("ClassName   : " + el.Current.ClassName);
            Console.WriteLine("Name        : " + el.Current.Name);
            Console.WriteLine("IsPassword  : " + el.Current.IsPassword);
            Console.WriteLine("IsEnabled   : " + el.Current.IsEnabled);
            Console.WriteLine("HasKeyboard : " + el.Current.HasKeyboardFocus);
            Console.WriteLine("FrameworkId : " + el.Current.FrameworkId);
            var b = el.Current.BoundingRectangle;
            Console.WriteLine("Bounds      : " + b.X + "," + b.Y + " " + b.Width + "x" + b.Height);

            foreach (var p in new AutomationPattern[] {
                TextPattern.Pattern, ValuePattern.Pattern, ScrollPattern.Pattern, InvokePattern.Pattern })
            {
                object o;
                Console.WriteLine("Supports " + p.ProgrammaticName + " : " + el.TryGetCurrentPattern(p, out o));
            }

            object pattern;
            if (el.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) {
                var tp = (TextPattern)pattern;
                try {
                    var doc = tp.DocumentRange;
                    object ro = doc.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
                    Console.WriteLine("  IsReadOnly=" + ro);
                } catch (Exception e) { Console.WriteLine("  attributes failed: " + e.GetType().Name); }
                try { Console.WriteLine("  DocumentRange text: [" + tp.DocumentRange.GetText(200).Replace("\r", "\\r").Replace("\n", "\\n") + "]"); }
                catch (Exception e) { Console.WriteLine("  DocumentRange failed: " + e.GetType().Name); }
                try { var s = tp.GetSelection(); Console.WriteLine("  Selection length: " + s.Length); } catch { }
            }
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) {
                var vp = (ValuePattern)pattern;
                Console.WriteLine("  Value readOnly: " + vp.Current.IsReadOnly);
                try { Console.WriteLine("  Value: [" + vp.Current.Value + "]"); } catch (Exception e) { Console.WriteLine("  Value failed: " + e.GetType().Name); }
            }
            // 往上找一层：很多自绘输入框的真实可读元素是父级。
            var walk = el;
            for (int depth = 0; depth < 3 && walk != null; depth++) {
                walk = TreeWalker.ControlViewWalker.GetParent(walk);
                if (walk == null) break;
                Console.WriteLine("  parent[" + depth + "] " + walk.Current.ControlType.ProgrammaticName + " / " + walk.Current.ClassName);
                object up;
                if (walk.TryGetCurrentPattern(TextPattern.Pattern, out up)) {
                    try {
                        var text = ((TextPattern)up).DocumentRange.GetText(200);
                        Console.WriteLine("     parent text: [" + text.Replace("\r", "\\r").Replace("\n", "\\n") + "]");
                    } catch { }
                }
            }
            Console.WriteLine("--- runtime id ---");
            try { Console.WriteLine(String.Join("-", el.GetRuntimeId().Select(n => n.ToString(CultureInfo.InvariantCulture)))); } catch { }
            return 0;
        }
    }
}
