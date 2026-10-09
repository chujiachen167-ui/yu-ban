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
                    result[Normalize(a[0])]=new WordEntry {Word=a[0],Phonetic=a[1]??"",Meaning=a[2]??""};
                    if(a.Length<4||String.IsNullOrEmpty(a[3])) continue;
                    foreach(var form in a[3].Split('/')) {var p=form.Split(':');if(p.Length==2 && p[0]!="0" && p[0]!="1") foreach(var word in p[1].Split(',')) if(!String.IsNullOrEmpty(word)&&!aliases.ContainsKey(Normalize(word))) aliases[Normalize(word)]=a[0];}
                }
            }
            foreach(var pair in aliases) if(!result.ContainsKey(pair.Key)) result[pair.Key]=result[Normalize(pair.Value)];
            return result;
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
        // 统一成与词典一致的形式：全小写、直引号。
        internal static string Normalize(string word) {
            return word.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').ToLowerInvariant();
        }
    }
}
