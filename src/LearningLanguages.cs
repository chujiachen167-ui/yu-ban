// 「我在学」的候选语言。当前只有中英两种经过验证，但结构按可扩展设计：
// 以后接入日语、韩语、法语、西语时，在这里加一行、并确认翻译方向与朗读都可用即可。
using System;

namespace EnglishCompanion {
    internal sealed class LearningTarget {
        internal string Id, Label, Hint;
        // Ready 表示翻译方向与朗读都已打通；未就绪的语言不会列给用户，避免承诺做不到的事。
        internal bool Ready;
        internal LearningTarget(string id, string label, string hint, bool ready) {
            Id = id; Label = label; Hint = hint; Ready = ready;
        }
    }
    internal static class LearningLanguages {
        internal static readonly LearningTarget[] All = {
            new LearningTarget("English", "英语", "输入中文或英文，译成英文；朗读用英语音色", true),
            new LearningTarget("Chinese", "中文", "输入中文或英文，译成中文；朗读用中文音色", true),
        };
        // 只把已验证的语言交给界面；未就绪的留在这里备查，不显示成选项。
        internal static LearningTarget[] Available() {
            var list = new System.Collections.Generic.List<LearningTarget>();
            foreach (var item in All) if (item.Ready) list.Add(item);
            return list.ToArray();
        }
        internal static LearningTarget Find(string id) {
            foreach (var item in All) if (String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }
        internal static string LabelOf(string id) {
            var item = Find(id);
            return item == null ? "英语" : item.Label;
        }
    }
}
