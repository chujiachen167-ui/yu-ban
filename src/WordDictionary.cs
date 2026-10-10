using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace EnglishCompanion {
    internal sealed class WordEntry {
        internal string Word, Phonetic, Meaning;
        // 词性（n. / v. / adj. 等）。ECDICT 的释义本身以词性开头，这里拆出来单独显示，
        // 查词模式下用户最想先看到的就是词性和读音。
        internal string PartOfSpeech;
    }
    internal static class WordDictionary {
        static readonly Lazy<Task<Dictionary<string,WordEntry>>> entries=new Lazy<Task<Dictionary<string,WordEntry>>>(delegate {return Task.Run((Func<Dictionary<string,WordEntry>>)Load);});
        internal static Dictionary<string,WordEntry> Load() {
            var result=new Dictionary<string,WordEntry>(StringComparer.OrdinalIgnoreCase);
            var aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var json=new JavaScriptSerializer();
            using(var raw=Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion.Dictionary.gz"))
            using(var zip=new GZipStream(raw,CompressionMode.Decompress))
            using(var reader=new StreamReader(zip,System.Text.Encoding.UTF8)) {
                string line; while((line=reader.ReadLine())!=null) {
                    // 词库是外部数据，单行损坏不能让整本词典失效。
                    string[] a;
                    try { a=json.Deserialize<string[]>(line); } catch { continue; }
                    if(a==null||a.Length<3||String.IsNullOrEmpty(a[0])) continue;
                    result[Normalize(a[0])]=new WordEntry {Word=a[0],Phonetic=a[1]??"",Meaning=a[2]??"",PartOfSpeech=PartOf(a[2])};
                    if(a.Length<4||String.IsNullOrEmpty(a[3])) continue;
                    foreach(var form in a[3].Split('/')) {var p=form.Split(':');if(p.Length==2 && p[0]!="0" && p[0]!="1") foreach(var word in p[1].Split(',')) if(!String.IsNullOrEmpty(word)&&!aliases.ContainsKey(Normalize(word))) aliases[Normalize(word)]=a[0];}
                }
            }
            foreach(var pair in aliases) if(!result.ContainsKey(pair.Key)) result[pair.Key]=result[Normalize(pair.Value)];
            return result;
        }
        // 中文词典：CC-CEDICT，与英文词典同构，因此共用一套读取与查询逻辑。
        static readonly Lazy<Task<Dictionary<string,WordEntry>>> chinese = new Lazy<Task<Dictionary<string,WordEntry>>>(delegate { return Task.Run((Func<Dictionary<string,WordEntry>>)LoadChinese); });
        internal static Dictionary<string,WordEntry> LoadChinese() {
            var result = new Dictionary<string,WordEntry>(StringComparer.Ordinal);
            var json = new JavaScriptSerializer();
            using (var raw = Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion.ChineseDictionary.gz"))
            using (var zip = new GZipStream(raw, CompressionMode.Decompress))
            using (var reader = new StreamReader(zip, System.Text.Encoding.UTF8)) {
                string line;
                while ((line = reader.ReadLine()) != null) {
                    string[] a;
                    try { a = json.Deserialize<string[]>(line); } catch { continue; }
                    if (a == null || a.Length < 3 || String.IsNullOrEmpty(a[0])) continue;
                    // 中文按原样匹配，不做大小写折叠；拼音单独给一行。
                    result[a[0]] = new WordEntry { Word = a[0], Phonetic = a[1] ?? "", Meaning = a[2] ?? "", PartOfSpeech = "" };
                }
            }
            return result;
        }
        // 查中文词。含汉字才走这里；一个词的多种写法（繁简）在生成时已补齐。
        internal static async Task<WordEntry> FindChinese(string word) {
            if (String.IsNullOrWhiteSpace(word)) return null;
            var all = await chinese.Value.ConfigureAwait(false);
            WordEntry entry;
            string key = word.Trim();
            if (all.TryGetValue(key, out entry)) return entry;
            return null;
        }
        // 查询要容错：浮窗取词可能带空格、连字符或弯引号，大小写也要能命中。
        internal static async Task<WordEntry> Find(string word) {
            if (String.IsNullOrWhiteSpace(word)) return null;
            var all = await entries.Value.ConfigureAwait(false);
            WordEntry entry;
            string key = word.Trim();
            if (all.TryGetValue(key, out entry)) return entry;
            if (all.TryGetValue(Normalize(key), out entry)) return entry;
            // 复合词退一步再试："counter-intuitive" 归到 "counterintuitive"。
            if (key.IndexOf('-') > 0 && all.TryGetValue(key.Replace("-", ""), out entry)) return entry;
            if (key.IndexOf('’') >= 0 && all.TryGetValue(key.Replace('’', '\''), out entry)) return entry;
            return null;
        }
        // 从释义开头取词性标记。ECDICT 的格式形如 "n. 记录\nv. 记录"，取第一个标记即可。
        // 取不到就返回空，不编造词性。
        internal static string PartOf(string meaning) {
            if (String.IsNullOrEmpty(meaning)) return "";
            string text = meaning.TrimStart();
            int end = text.IndexOfAny(new[] { ' ', '\n', '\r', '\t' });
            string token = end < 0 ? text : text.Substring(0, end);
            if (token.Length < 2 || token.Length > 12) return "";
            // 只接受以点结尾、由字母与点组成的标记，避免把普通单词当词性。
            if (!token.EndsWith(".", StringComparison.Ordinal)) return "";
            foreach (char c in token) if (!Char.IsLetter(c) && c != '.' && c != '&') return "";
            return token;
        }
        // 统一成与词典一致的形式：全小写、直引号。
        internal static string Normalize(string word) {            return word.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').ToLowerInvariant();
        }
    }
}
