// 按句中英对照：句段划分、翻译方向判定、带稳定 ID 的句组结果。
// 纯逻辑，不联网、不读配置，可脱离桌面验证。
using System;
using System.Collections.Generic;
using System.Text;

namespace EnglishCompanion {
    // 一组「原文句 → 对应译文」。Id 在一次翻译请求内稳定，用于配对与渲染。
    public sealed class SentencePair {
        public string Id = "", Source = "", Target = "";
    }
    // 一次翻译的完整结果：保留原始整段文字、句组顺序和完整译文。
    public sealed class PairResult {
        public string Source = "", Target = "";
        public string Direction = "";          // 模型回填的目标语言名
        public List<SentencePair> Pairs = new List<SentencePair>();
        public bool Paired { get { return Pairs != null && Pairs.Count > 0; } }
    }
    internal enum TextDirection { ChineseToEnglish, EnglishToChinese, Mixed }
    internal static class Sentences {
        internal const int MaxSource = 1800;

        // 句末标点。中英文共用；顿号、分号不切句，避免把从句拆开。
        static readonly char[] Terminators = { '。', '！', '？', '!', '?', '…', '\n' };
        // 收尾标点后面可能跟的收束符号：引号、括号、书名号。
        static readonly char[] Closers = { '"', '\'', '”', '’', ')', '）', '】', '》', '」', '』', '〉' };
        // 次级切点：逗号与顿号。按用户决定，这两种断开成组；分号不断。
        static readonly char[] ClauseBreaks = { '，', ',', '、' };
        // 连词引导的从句不断开：「不过」「但是」这类前后是完整语义，切开会碎。
        static readonly string[] Connectors = { "不过", "但是", "但", "可是", "然而", "所以", "因此", "因为", "如果", "虽然", "而且", "并且", "and", "but", "so", "because", "although", "while" };

        // 英文缩写与常见口语缩写：句中的点号不是句号。
        static readonly HashSet<string> Abbreviations = new HashSet<string>(StringComparer.Ordinal) {
            "mr","mrs","ms","dr","prof","sr","jr","st","mt","ft","vs","etc","eg","ie","inc","ltd","co","corp",
            "no","vol","fig","approx","dept","est","fig","al","inc","univ","govt","capt","sgt","lt","col","gen",
            "e.g","i.e","a.m","p.m","u.s","u.k"
        };

        // 判定输入方向。混合语言时以占比高者为准，但只在结果里如实记录，不猜。
        internal static TextDirection Detect(string text) {
            if (String.IsNullOrEmpty(text)) return TextDirection.EnglishToChinese;
            int han = 0, latin = 0;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c >= 0x4E00 && c <= 0x9FFF) han++;
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) latin++;
            }
            if (han == 0) return TextDirection.EnglishToChinese;
            if (latin == 0) return TextDirection.ChineseToEnglish;
            return han * 2 >= latin ? TextDirection.ChineseToEnglish : TextDirection.EnglishToChinese;
        }

        // 目标语言由用户在设置里显式选择：「我在学」是什么语言，就译成什么语言。
        // 输入语言与自己学的语言相同时方向取反——那说明他是在拿母语查另一种说法。
        // 界面上必须能选，所以 Auto 保留为"跟随输入"的选项。
        internal static string TargetLanguage(string configured, TextDirection direction) {
            string fallback = direction == TextDirection.ChineseToEnglish ? "English" : "Chinese";
            if (String.IsNullOrWhiteSpace(configured) || configured.Equals("Auto", StringComparison.OrdinalIgnoreCase)) return fallback;
            if (!configured.Equals("Chinese", StringComparison.OrdinalIgnoreCase) && !configured.Equals("English", StringComparison.OrdinalIgnoreCase))
                return configured;   // 其它语种按用户填写的目标语言译。
            bool inputIsTarget = (direction == TextDirection.ChineseToEnglish && configured.Equals("Chinese", StringComparison.OrdinalIgnoreCase))
                                 || (direction == TextDirection.EnglishToChinese && configured.Equals("English", StringComparison.OrdinalIgnoreCase));
            if (!inputIsTarget) return configured;
            return configured.Equals("Chinese", StringComparison.OrdinalIgnoreCase) ? "English" : "Chinese";
        }

        // 按标点与段落稳健切句。保留每个句段的确切原文与顺序，不做任何改写。
        internal static List<string> Split(string text) {
            var result = new List<string>();
            if (String.IsNullOrWhiteSpace(text)) return result;
            int start = 0;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == '\r') continue;
                if (c == '\n') { Add(text, start, i, result); start = i + 1; continue; }
                // 次级切点：逗号与顿号在句中就断开，标点跟着前一段走。
                if (Array.IndexOf(ClauseBreaks, c) >= 0 && i > start) {
                    int end = i + 1;
                    while (end < text.Length && Array.IndexOf(Closers, text[end]) >= 0) end++;
                    if (!ContinuesAfter(text, end)) { Add(text, start, end, result); start = end; i = end - 1; continue; }
                    continue;
                }
                if (!IsTerminator(c)) continue;
                // 连续标点（如 ？！ 或 ……）属于同一句。
                int stop = i + 1;
                while (stop < text.Length && (IsTerminator(text[stop]) || text[stop] == '…')) stop++;
                // 收束引号与括号并入本句。
                while (stop < text.Length && Array.IndexOf(Closers, text[stop]) >= 0) stop++;
                if (c != '.' || !IsAbbreviationDot(text, i, start)) {
                    Add(text, start, stop, result);
                    start = stop;
                    i = stop - 1;
                }
            }
            Add(text, start, text.Length, result);
            return result;
        }
        // 连词开头的前半句不断开：逗号后若紧跟「但是」这类词，说明它是一整句的转折后半。
        static bool ContinuesAfter(string text, int at) {
            int i = at;
            while (i < text.Length && (text[i] == ' ' || text[i] == '　')) i++;
            if (i >= text.Length) return false;
            for (int k = 0; k < Connectors.Length; k++) {
                string w = Connectors[k];
                if (i + w.Length > text.Length) continue;
                bool match = true;
                for (int j = 0; j < w.Length; j++) if (Char.ToLowerInvariant(text[i + j]) != Char.ToLowerInvariant(w[j])) { match = false; break; }
                if (!match) continue;
                // 英文连词需要词边界，避免把 andy 之类误判成 and。
                if (w[0] >= 'a' && w[0] <= 'z') {
                    if (i > 0 && Char.IsLetterOrDigit(text[i - 1])) continue;
                    if (i + w.Length < text.Length && Char.IsLetter(text[i + w.Length])) continue;
                }
                return true;
            }
            return false;
        }

        static void Add(string text, int start, int end, List<string> result) {
            if (end <= start) return;
            string piece = text.Substring(start, end - start).Trim();
            if (piece.Length > 0) result.Add(piece);
        }

        static bool IsTerminator(char c) {
            foreach (char t in Terminators) if (c == t) return true;
            return false;
        }

        // 英文缩写的小数点、缩写点不得切句；数字之间的小数点同理。
        static bool IsAbbreviationDot(string text, int at, int start) {
            char c = text[at];
            if (c != '.') return false;
            char next = at + 1 < text.Length ? text[at + 1] : ' ';
            if (next == ' ') {
                int left = at - 1; while (left >= start && text[left] == ' ') left--;
                int right = at + 1; while (right < text.Length && text[right] == ' ') right++;
                if (right >= text.Length) return false;
                // 句号后直接跟新的大写词：通常是缩写，交给下一轮再判断。
                int wordStart = left, wordEnd = left + 1;
                while (wordStart - 1 >= start && IsWordChar(text[wordStart - 1])) wordStart--;
                while (wordEnd < text.Length && IsWordChar(text[wordEnd])) wordEnd++;
                if (wordEnd > wordStart) {
                    string word = text.Substring(wordStart, wordEnd - wordStart).ToLowerInvariant();
                    if (Abbreviations.Contains(word)) return true;
                }
                return false;
            }
            if (char.IsDigit(next)) return true;                       // 3.14
            char prev = at > 0 ? text[at - 1] : ' ';
            if (char.IsDigit(prev) && char.IsDigit(next)) return true; // 1.000
            return false;
        }
        static bool IsWordChar(char c) { return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'); }

        // 建立带稳定 ID 的句组：ID 用 s1..sN，不依赖模型，只依赖输入顺序。
        internal static List<SentencePair> Groups(string text) {
            var pairs = new List<SentencePair>();
            var parts = Split(text);
            for (int i = 0; i < parts.Count; i++)
                pairs.Add(new SentencePair { Id = "s" + (i + 1), Source = parts[i], Target = "" });
            return pairs;
        }
    }
}
