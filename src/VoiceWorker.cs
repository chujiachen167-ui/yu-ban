using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace EnglishCompanion {
    internal sealed class VoiceRequest { public string Audio, Text, Language; public bool Local; public bool FailForTest {get;set;} }
    internal static class WaveAudio {
        internal static byte[] Normalize(byte[] data) {
            // Audio 3.1 returns this streaming PCM header even in a completed HTTP file.
            // Repair only the observed sentinel pair; ordinary truncated WAVs remain errors.
            if(data!=null && data.Length>=44 && data.Length<=16000000 && Encoding.ASCII.GetString(data,0,4)=="RIFF" && Encoding.ASCII.GetString(data,8,8)=="WAVEfmt " && BitConverter.ToUInt32(data,16)==16 && Encoding.ASCII.GetString(data,36,4)=="data" && BitConverter.ToUInt32(data,4)==0x7fffffbf && BitConverter.ToUInt32(data,40)==0x7fffff9b) {
                data=(byte[])data.Clone(); Buffer.BlockCopy(BitConverter.GetBytes(data.Length-8),0,data,4,4); Buffer.BlockCopy(BitConverter.GetBytes(data.Length-44),0,data,40,4);
            }
            Validate(data); return data;
        }
        internal static double Validate(byte[] data) {
            if(data==null || data.Length<44 || data.Length>16000000 || Encoding.ASCII.GetString(data,0,4)!="RIFF" || Encoding.ASCII.GetString(data,8,4)!="WAVE") throw new InvalidOperationException("语音返回了无效音频，请重试");
            int channels=0, rate=0, bits=0, align=0; long bytes=0;
            for(long p=12;p+8<=data.Length;) {
                int at=(int)p; uint length=BitConverter.ToUInt32(data,at+4); long end=p+8+length;
                if(end>data.Length) throw new InvalidOperationException("语音音频不完整，请重试");
                string chunk=Encoding.ASCII.GetString(data,at,4);
                if(chunk=="fmt ") {
                    if(length<16 || BitConverter.ToUInt16(data,at+8)!=1) throw new InvalidOperationException("暂不支持此音频编码，请选择 WAV PCM 语音输出");
                    channels=BitConverter.ToUInt16(data,at+10); rate=BitConverter.ToInt32(data,at+12); align=BitConverter.ToUInt16(data,at+20); bits=BitConverter.ToUInt16(data,at+22);
                }
                if(chunk=="data") bytes+=length;
                p=end+(length%2);
            }
            if(channels<1 || channels>2 || rate<8000 || rate>96000 || (bits!=8 && bits!=16) || align!=channels*bits/8 || bytes==0 || bytes%align!=0) throw new InvalidOperationException("音频参数不受支持，请重试或更换音色");
            double seconds=bytes/(double)(rate*align); if(seconds>120) throw new InvalidOperationException("音频过长，请选取较短的一句"); return seconds;
        }
    }
    internal static class VoiceHost {
        [STAThread] static int Main() {
            Console.InputEncoding=new UTF8Encoding(false); Console.OutputEncoding=new UTF8Encoding(false);
            // Child-only native audio. Access violations here cannot terminate the input companion.
            try {
                string input=Console.In.ReadLine(); if(input==null || input.Length>23000000) return 2;
                var request=new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength=24000000 }.Deserialize<VoiceRequest>(input);
                if(request.FailForTest) { Environment.Exit(23); return 23; }
                if(request.Local) {
                    using(var voice=new SpeechSynthesizer()) {
                        string code=request.Language=="Japanese"?"ja":request.Language=="Korean"?"ko":request.Language=="Spanish"?"es":"en";
                        string name=null; foreach(var v in voice.GetInstalledVoices()) if(v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName==code) { name=v.VoiceInfo.Name; break; }
                        if(name==null) { Console.WriteLine("NO_VOICE"); return 3; }
                        voice.SelectVoice(name); voice.Speak(request.Text);
                    }
                } else {
                    byte[] bytes=Convert.FromBase64String(request.Audio); WaveAudio.Validate(bytes);
                    using(var stream=new MemoryStream(bytes)) using(var player=new SoundPlayer(stream)) { player.Load(); player.PlaySync(); }
                }
                Console.WriteLine("OK"); return 0;
            } catch { Console.WriteLine("AUDIO_ERROR"); return 4; }
        }
    }
    internal sealed class VoiceProcess : IDisposable {
        Process process;
        internal async Task Play(VoiceRequest request,CancellationToken token) {
            if(request.Audio!=null) WaveAudio.Validate(Convert.FromBase64String(request.Audio));
            var info=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CompanionVoice.exe")) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8 };
            var child=Process.Start(info); process=child;
            try {
                using(token.Register(delegate { StopChild(child); })) {
                    var json=new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength=24000000 };
                    byte[] payload=Encoding.UTF8.GetBytes(json.Serialize(request)+"\n");
                    await child.StandardInput.BaseStream.WriteAsync(payload,0,payload.Length,token); child.StandardInput.Close();
                    bool exited=await Task.Run(delegate { return child.WaitForExit(130000); });
                    token.ThrowIfCancellationRequested();
                    if(!exited) { StopChild(child); throw new InvalidOperationException("朗读超时，请重试"); }
                    string result=await child.StandardOutput.ReadToEndAsync();
                    if(child.ExitCode!=0) throw new InvalidOperationException(result.Contains("NO_VOICE")?"Windows 尚未安装英语音色，请使用千问语音":"播放模块未能完成朗读，伴读仍在运行，请重试");
                }
            } catch(IOException) { token.ThrowIfCancellationRequested(); throw new InvalidOperationException("播放模块已退出，伴读仍在运行，请重试"); }
            finally { if(process==child) process=null; StopChild(child); child.Dispose(); }
        }
        static void StopChild(Process child) { try { if(!child.HasExited) child.Kill(); } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { } }
        public void Dispose() { var child=process; process=null; if(child!=null) StopChild(child); }
    }
}
