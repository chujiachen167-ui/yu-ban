using System;
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
        internal static readonly string[] VoiceIds={"","Betty_v3.1","Brian_v3.1","Emily_v3.1"};
        internal static readonly string[] VoiceNames={"原有音色","Betty · 美式女声","Brian · 美式男声","Emily · 英式女声"};
        internal static bool Supported(Configuration c) {return !c.LocalVoice&&ProviderProfiles.SpeechProvider(c)=="千问"&&c.SpeechModel=="qwen-audio-3.1-tts-flash"&&c.Language=="English";}
        internal static string Voice(Configuration c) {return Supported(c)&&Array.IndexOf(VoiceIds,c.EnglishVoice)>0?c.EnglishVoice:c.Voice;}
        internal static string Instruction(Configuration c) {
            if(!Supported(c))return "";
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
