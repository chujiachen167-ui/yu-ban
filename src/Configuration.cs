// 凭据使用 Windows 当前用户加密；发布目录不包含任何个人配置。
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace EnglishCompanion {
    public sealed class Configuration {
        public string TranslationUrl = "https://api.deepseek.com/chat/completions";
        public string TranslationModel = "deepseek-flash";
        public string TranslationSecret = "";
        public string SpeechUrl = "https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation";
        public string SpeechModel = "qwen3-tts-flash", Voice = "Cherry", SpeechSecret = "";
        public string Language = "English", Style = "自然口语";
        // 默认目标语言。输入语言与它相同时自动取反，所以这个默认值不锁死翻译方向。
        public const string DefaultLanguage = "English";
        public string SpeechStyle = "original", EnglishVoice = "";
        public string Theme = "glass";
        public string TranslationService = "", SpeechService = "";
        public Dictionary<string, ModelProfile> TranslationProfiles = new Dictionary<string, ModelProfile>();
        public Dictionary<string, ModelProfile> SpeechProfiles = new Dictionary<string, ModelProfile>();
        public bool ReuseKey = false, LocalVoice, AutoTyping = true;
        public bool ShowOriginal = true;
        public int InputVersion;
        public Dictionary<string, string> TranslationKeys = new Dictionary<string, string>();
        public Dictionary<string, string> SpeechKeys = new Dictionary<string, string>();
        public static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnglishCompanion", "settings.json"); } }
        public static Configuration Load() {
            if (!File.Exists(FilePath)) return new Configuration();
            var c = Probe.Json.Deserialize<Configuration>(File.ReadAllText(FilePath, Encoding.UTF8));
            if (c == null) throw new InvalidDataException();
            if (c.TranslationKeys == null) c.TranslationKeys = new Dictionary<string, string>();
            if (c.SpeechKeys == null) c.SpeechKeys = new Dictionary<string, string>();
            if (c.TranslationProfiles == null) c.TranslationProfiles = new Dictionary<string, ModelProfile>();
            if (c.SpeechProfiles == null) c.SpeechProfiles = new Dictionary<string, ModelProfile>();
            if (c.InputVersion < 13) { c.AutoTyping = true; c.InputVersion = 13; }
            return c;
        }
        public void Save() {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, Probe.Json.Serialize(this), Encoding.UTF8);
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static string Seal(string value) {
            return value.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
        }
        public static string Open(string value) {
            if (String.IsNullOrEmpty(value)) return "";
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser));
        }
    }
}
