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
        static Dictionary<string,WordEntry> Load() {
            var result=new Dictionary<string,WordEntry>(StringComparer.OrdinalIgnoreCase);
            var aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var json=new JavaScriptSerializer();
            using(var raw=Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion.Dictionary.gz"))
            using(var zip=new GZipStream(raw,CompressionMode.Decompress))
            using(var reader=new StreamReader(zip,System.Text.Encoding.UTF8)) {
                string line; while((line=reader.ReadLine())!=null) {
                    var a=json.Deserialize<string[]>(line);
                    result[a[0]]=new WordEntry {Word=a[0],Phonetic=a[1],Meaning=a[2]};
                    foreach(var form in a[3].Split('/')) {var p=form.Split(':');if(p.Length==2 && p[0]!="0" && p[0]!="1") foreach(var word in p[1].Split(',')) if(!aliases.ContainsKey(word)) aliases[word]=a[0];}
                }
            }
            foreach(var pair in aliases) if(!result.ContainsKey(pair.Key)) result[pair.Key]=result[pair.Value];
            return result;
        }
        internal static async Task<WordEntry> Find(string word) {
            var all=await entries.Value.ConfigureAwait(false); WordEntry entry;
            return all.TryGetValue(word.Replace('’','\'').ToLowerInvariant(),out entry)?entry:null;
        }
    }
}
