// 悬停取词：找出鼠标位置下那个词。
// 中文没有空格，所以要在词典里做最长匹配；英文沿用原有词形规则。
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EnglishCompanion {
    internal static class WordAt {
        // 英文词：字母，允许词内连字符与撇号（well-known、don't）。
        static readonly Regex English = new Regex(@"[A-Za-z]+(?:['’\-][A-Za-z]+)*", RegexOptions.Compiled);

        internal static string Pick(string text, int index, bool chinese) {
            if (String.IsNullOrEmpty(text) || index < 0 || index >= text.Length) return "";
            if (chinese) return Han(text, index, 4);
            foreach (Match m in English.Matches(text))
                if (index >= m.Index && index < m.Index + m.Length) return m.Value;
            return "";
        }

        // 中文最长匹配：以光标所在的那串连续汉字为范围，从长到短在词典里找，
        // 只接受真正收录在词典里的词。找不到就退到光标处的单字。
        internal static string Han(string text, int index, int maxLength) {
            int left = index;
            while (left > 0 && IsHan(text[left - 1])) left--;
            // 光标不在汉字上时没有中文词可查。
            if (!IsHan(text[index])) return "";
            int cursor = index - left;
            int length = 0;
            while (left + length < text.Length && IsHan(text[left + length])) length++;
            for (int len = Math.Min(maxLength, length); len > 0; len--) {
                // 起点往前滑：只接受覆盖光标且词典里收录的词，取最长的那个。
                for (int at = Math.Min(cursor, length - len); at >= 0; at--) {
                    if (at > cursor || cursor >= at + len) continue;   // 不覆盖光标就跳过
                    string word = text.Substring(left + at, len);
                    if (Known(word)) return word;
                }
            }
            return text.Substring(index, 1);
        }

        static bool Known(string word) {
            return WordDictionary.FindChinese(word).GetAwaiter().GetResult() != null;
        }

        static bool IsHan(char c) { return c >= 0x4E00 && c <= 0x9FFF; }
    }
}
