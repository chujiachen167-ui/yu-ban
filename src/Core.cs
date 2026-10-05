// 输入差异与捕获会话：纯逻辑，可脱离桌面验证。
using System;
using System.Drawing;

namespace EnglishCompanion {
    public sealed class Snapshot {
        public string Id = "", Text = "", Selection = "", Reason = "", Process = "";
        public long Window;
        public bool Editable, Protected, Composing;
        public int X, Y, Width, Height;
        public int SelectionStart = -1, SelectionEnd = -1;
        public Rectangle Rect { get { return new Rectangle(X, Y, Math.Max(1, Width), Math.Max(1, Height)); } }
    }
    public static class Changes {
        public static string AddedAt(string before, string after, int start, int end) {
            if (before == after || before == null || after == null || before.Length > 6000 || after.Length > 6000) return "";
            if (start < 0 || end < start || end > before.Length) return Added(before, after);
            string prefix = before.Substring(0, start), suffix = before.Substring(end);
            if (after.Length < prefix.Length + suffix.Length || !after.StartsWith(prefix, StringComparison.Ordinal) || !after.EndsWith(suffix, StringComparison.Ordinal)) return Added(before, after);
            string added = after.Substring(prefix.Length, after.Length - prefix.Length - suffix.Length).Trim();
            return added.Length <= 1800 ? added : "";
        }
        public static string Added(string before, string after) {
            if (before == null || after == null || before.Length > 6000 || after.Length > 6000) return "";
            int first = 0;
            while (first < before.Length && first < after.Length && before[first] == after[first]) first++;
            int a = before.Length, b = after.Length;
            while (a > first && b > first && before[a - 1] == after[b - 1]) { a--; b--; }
            // Do not split a surrogate pair at a common-prefix boundary.
            if (first > 0 && first < after.Length && char.IsLowSurrogate(after[first])) first--;
            string value = after.Substring(first, b - first).Trim();
            return value.Length <= 1800 ? value : "";
        }
        public static Rectangle Place(Rectangle anchor, Size size, Rectangle area) {
            int x = Math.Max(area.Left, Math.Min(anchor.Left, area.Right - size.Width));
            int y = anchor.Top - size.Height - 10;
            if (y < area.Top) y = anchor.Bottom + 10;
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - size.Height));
            return new Rectangle(x, y, size.Width, size.Height);
        }
    }
    public sealed class CaptureSession {
        public string Id = "", Before = "", Candidate = "";
        public DateTime Deadline, Changed;
        public bool Active;
        bool immediate;
        int start, end;
        public void Start(Snapshot s, DateTime now, bool immediate = false) {
            Reset();
            if (s == null || !s.Editable || s.Protected) return;
            this.immediate=immediate;
            Id = s.Id; Before = s.Text; start = s.SelectionStart; end = s.SelectionEnd; Deadline = now.AddSeconds(20); Changed = now; Active = true;
        }
        public void Extend(DateTime now) { if (Active) Deadline = now.AddSeconds(20); }
        public void Reset() { Active = false; Id = Before = Candidate = ""; }
        public string Observe(Snapshot s, bool held, DateTime now) {
            if (!Active) return null;
            if (s == null || s.Protected || !s.Editable || s.Id != Id || now > Deadline) { Reset(); return null; }
            string next = Changes.AddedAt(Before, s.Text, start, end);
            if (next != Candidate) { Candidate = next; Changed = now; }
            if (!held && !s.Composing && Candidate.Length > 0 && (immediate || (now-Changed).TotalMilliseconds>=500)) {
                string result = Candidate; Reset(); return result;
            }
            return null;
        }
    }
    // A focus baseline is never submitted. Only a subsequent edit schedules the current sentence.
    public sealed class TypingSession {
        string id = "", previous = "", candidate;
        DateTime changed;
        public int Revision { get; private set; }
        public void Reset() { id = previous = ""; candidate = null; Revision++; }
        public void Baseline(Snapshot s) { id = s.Id; previous = s.Text; candidate = null; }
        public string Observe(Snapshot s, bool suspended, DateTime now) {
            if (s == null || s.Protected || !s.Editable || s.Text.Length > 6000) { Reset(); return null; }
            if (id != s.Id) { Baseline(s); return null; }
            if (suspended) { if (previous != s.Text) Revision++; Baseline(s); changed = now; return null; }
            if (s.Composing) { if (candidate != null) Revision++; candidate = null; changed = now; return null; }
            if (previous != s.Text) {
                int at = 0; while (at < previous.Length && at < s.Text.Length && previous[at] == s.Text[at]) at++;
                // The edit location, not selection of unrelated text, chooses the sentence.
                int end = s.Text.Length, oldEnd = previous.Length;
                while (end > at && oldEnd > at && s.Text[end - 1] == previous[oldEnd - 1]) { end--; oldEnd--; }
                candidate = Sentence(s.Text, Math.Max(at, end - 1)); previous = s.Text; changed = now; Revision++;
            }
            if (candidate != null && (now - changed).TotalMilliseconds >= 500) {
                var result = candidate; candidate = null; return result.Length == 0 ? null : result;
            }
            return null;
        }
        static bool Boundary(char c) { return c == '。' || c == '！' || c == '？' || c == '\n' || c == '!' || c == '?'; }
        public static string Sentence(string text, int at) {
            if (String.IsNullOrWhiteSpace(text)) return "";
            at = Math.Max(0, Math.Min(at, text.Length - 1)); int left = at, right = at;
            while (left > 0 && !Boundary(text[left - 1])) left--;
            while (right < text.Length && !Boundary(text[right])) right++;
            if (right < text.Length) right++;
            string result = text.Substring(left, right - left).Trim(); return result.Length <= 1800 ? result : "";
        }
    }
}
