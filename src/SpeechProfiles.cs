using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
namespace EnglishCompanion {
    internal static class SpeechProfiles {
        internal const string Sample="Are you coming with us? I thought the meeting was tomorrow, but it's actually today. That's wonderful news! Let's take it one step at a time.";
        internal static readonly string[] StyleIds={"original","natural","clear"};
        internal static readonly string[] StyleNames={"现状","自然会话","清晰伴读"};
        // 一条可读的英语音色：ID 是各服务商的真实参数，描述沿用官方文档的说法。
        internal sealed class VoiceOption {
            internal string Id, Name, Note;
            internal VoiceOption(string id,string name,string note="") {Id=id;Name=name;Note=note;}
        }
        // 音色按「服务商 + 模型」给出，不做跨服务商的统一列表：
        // 同一个 Betty 在别家不存在，选项必须反映当下这家到底支持什么。
        internal static List<VoiceOption> Catalog(Configuration c) {
            var list=new List<VoiceOption>();
            list.Add(new VoiceOption("","原有音色"));
            string provider=ProviderProfiles.SpeechProvider(c);
            if(provider!="千问") { list.AddRange(GenericVoices(provider,c)); return list; }
            if(c.SpeechModel=="qwen-audio-3.1-tts-flash") {
                // 官方「精品英文音色」共 15 个，文档只标注性别与口音，没有场景描述，这里不自行编写。
                list.Add(new VoiceOption("Eric_v3.1","Eric · 英式男声"));
                list.Add(new VoiceOption("Luca_v3.1","Luca · 英式男声"));
                list.Add(new VoiceOption("Emily_v3.1","Emily · 英式女声"));
                list.Add(new VoiceOption("Luna_v3.1","Luna · 英式女声"));
                list.Add(new VoiceOption("Brian_v3.1","Brian · 美式男声"));
                list.Add(new VoiceOption("David_v3.1","David · 美式男声"));
                list.Add(new VoiceOption("Andy_v3.1","Andy · 美式男声"));
                list.Add(new VoiceOption("Betty_v3.1","Betty · 美式女声"));
                list.Add(new VoiceOption("Ava_v3.1","Ava · 美式女声"));
                list.Add(new VoiceOption("Abby_v3.1","Abby · 美式女声"));
                list.Add(new VoiceOption("Annie_v3.1","Annie · 美式女声"));
                list.Add(new VoiceOption("Beth_v3.1","Beth · 美式女声"));
                list.Add(new VoiceOption("Cally_v3.1","Cally · 美式女声"));
                list.Add(new VoiceOption("Cindy_v3.1","Cindy · 美式女声"));
                list.Add(new VoiceOption("Donna_v3.1","Donna · 美式女声"));
                // 这三个童声在官方文档里属于中文音色表，3.1 未标注支持英语，因此不列在这里。
                return list;
            }
            if(c.SpeechModel=="qwen3-tts-flash") {
                // 这批是通用音色，官方文档明确列出支持英语。音色名沿用官方英文名。
                list.Add(new VoiceOption("Cherry","Cherry · 女声"));
                list.Add(new VoiceOption("Ethan","Ethan · 男声"));
                list.Add(new VoiceOption("Serena","Serena · 女声"));
                list.Add(new VoiceOption("Chelsie","Chelsie · 女声"));
                list.Add(new VoiceOption("Dylan","Dylan · 男声"));
                list.Add(new VoiceOption("Jada","Jada · 女声"));
                // 童声：官方描述为小女孩、小男孩、少年、少女；英语音质官方未说明，先给用户试听入口。
                list.Add(new VoiceOption("Bunny","Bunny · 小女孩","官方描述：烂漫的小女孩"));
                list.Add(new VoiceOption("Pip","Pip · 小男孩","官方描述：淘气的小男孩"));
                list.Add(new VoiceOption("Mochi","Mochi · 少年音","官方描述：机灵的少年"));
                list.Add(new VoiceOption("Stella","Stella · 少女音","官方描述：甜美的少女"));
                return list;
            }
            list.AddRange(GenericVoices(provider,c));
            return list;
        }
        // 其他服务商用各自真实公开的目录；没有公开口音信息的，不自行编写描述。
        static IEnumerable<VoiceOption> GenericVoices(string provider,Configuration c) {
            if(provider=="OpenAI") {
                // 官方 API 枚举的 13 个内置音色。官方未公布性别或口音，这里不编造。
                foreach(var id in new[]{"marin","cedar","alloy","ash","ballad","coral","echo","fable","nova","onyx","sage","shimmer","verse"})
                    yield return new VoiceOption(id, id);
                yield break;
            }
            if(provider=="硅基流动") {
                foreach(var id in new[]{"alex","anna","bella","benjamin","charles","claire","david","diana"})
                    yield return new VoiceOption("FunAudioLLM/CosyVoice2-0.5B:"+id, id, "官方未标注口音");
                yield break;
            }
            // ElevenLabs 的内置音色 2026-12-31 到期，不列出具体 ID，只提示用默认音色。
            if(provider=="ElevenLabs") { yield return new VoiceOption(c.Voice, "账户音色", "请在平台控制台选择"); yield break; }
            yield return new VoiceOption(c.Voice, string.IsNullOrEmpty(c.Voice) ? "平台默认音色" : c.Voice, "该平台未提供公开音色目录");
        }
        // 朗读偏好只在真正支持的平台上开放，不把一家能力说成所有家都支持。
        // 当前这台平台与模型到底支持什么。不支持的部分要说清楚原因，而不是让人调了没反应。
        internal sealed class Capability {
            internal bool Styles, Voices, ChildVoices, BritishVoices;
            internal string Reason = "";
        }
        internal static Capability CapabilityOf(Configuration c) {
            var cap = new Capability();
            string provider = ProviderProfiles.SpeechProvider(c);
            if (c.LocalVoice) { cap.Reason = "系统语音由 Windows 提供，没有可选音色与读法"; return cap; }
            if (c.Language != "English") { cap.Reason = "当前只对英语朗读提供音色与读法"; return cap; }
            if (provider == "千问") {
                if (c.SpeechModel == "qwen-audio-3.1-tts-flash") {
                    cap.Styles = cap.Voices = cap.BritishVoices = true; return cap;
                }
                if (c.SpeechModel == "qwen3-tts-flash") {
                    // 这个模型只接受 voice，不接 instruction，所以有音色（含童声）但没有读法。
                    cap.Voices = cap.ChildVoices = true;
                    cap.Reason = "当前模型提供音色与童声，但不接受读法指令；需要「现状／自然会话／清晰伴读」请改用 Audio 3.1 模型";
                    return cap;
                }
                cap.Reason = "当前千问模型不在已验证列表中";
                return cap;
            }
            if (provider == "OpenAI" && c.SpeechModel == "gpt-4o-mini-tts") {
                cap.Styles = cap.Voices = true;
                cap.Reason = "OpenAI 通过文字指令控制口音与情绪，没有单独的音色目录说明";
                return cap;
            }
            if (provider == "硅基流动") { cap.Voices = true; cap.Reason = "该平台未公布口音信息，音色按名称列出"; return cap; }
            cap.Reason = provider + " 未提供公开音色目录";
            return cap;
        }
        internal static bool Supported(Configuration c) {
            if(c.LocalVoice||c.Language!="English")return false;
            string provider=ProviderProfiles.SpeechProvider(c);
            if(provider=="千问")return c.SpeechModel=="qwen-audio-3.1-tts-flash"||c.SpeechModel=="qwen3-tts-flash";
            return provider=="OpenAI"&&c.SpeechModel=="gpt-4o-mini-tts";
        }
        internal static string Voice(Configuration c) {
            return Supported(c)&&!String.IsNullOrEmpty(c.EnglishVoice)?c.EnglishVoice:c.Voice;
        }
        // OpenAI 用 instructions 表达口音、情绪与语速；口音是官方列出的可控项。
        internal static string Guide(Configuration c) {
            if(!Supported(c))return "";
            if(ProviderProfiles.SpeechProvider(c)!="OpenAI")return "";
            if(c.SpeechStyle=="natural")return "Speak in natural conversational English, as if talking to a friend. Use meaningful phrase pauses and appropriate question intonation. Read only the supplied text.";
            if(c.SpeechStyle=="clear")return "Read in clear English for a language learner. Emphasize important words and pause at phrase boundaries, without reading word by word. Read only the supplied text.";
            return "";
        }
        internal static string Instruction(Configuration c) {
            if(!Supported(c))return "";
            // qwen3-tts-flash 只接受 voice，不接受 instruction；只对 3.1 的英语朗读下发。
            if(c.SpeechModel!="qwen-audio-3.1-tts-flash")return "";
            if(c.SpeechStyle=="natural")return "Speak in natural conversational English, as if talking to a friend. Use meaningful phrase pauses, sentence stress, connected speech and appropriate question intonation. Let emotion follow the meaning subtly, without theatrical delivery. Read only the supplied text, without adding or omitting words.";
            if(c.SpeechStyle=="clear")return "Read in clear, natural English for a language learner. Keep connected speech, emphasize important words, pause at meaningful phrase boundaries and use appropriate question intonation. Be calm and expressive, not flat or word-by-word. Read only the supplied text, without adding or omitting words.";
            return "";
        }
        internal static string SamplePath(Configuration c) {
            // Only a fixed, public demo sentence is persisted; never cache user input here.
            byte[] bytes=Encoding.UTF8.GetBytes(c.SpeechUrl+"\n"+Probe.Json.Serialize(Services.SpeechBody(c,Sample)));
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EnglishCompanion","voice-samples",hash+".wav");
        }
        internal static async Task<byte[]> Preview(Configuration c,CancellationToken token) {
            if(!Supported(c))throw new InvalidOperationException("当前试听适用于千问 Audio 3.1 的英语朗读");
            token.ThrowIfCancellationRequested();string path=SamplePath(c);
            if(File.Exists(path)) {
                try {var saved=File.ReadAllBytes(path);WaveAudio.Validate(saved);return saved;}
                catch(InvalidOperationException) { }
                catch(IOException) { }
            }
            var audio=await Services.Speak(c,Sample,token);token.ThrowIfCancellationRequested();
            WaveAudio.Validate(audio);
            try {Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,audio);}
            catch(IOException) { } catch(UnauthorizedAccessException) { }
            return audio;
        }
    }
}
