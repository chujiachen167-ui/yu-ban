using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace EnglishCompanion {
    internal static partial class Tests {
        sealed class ContractHandler:HttpMessageHandler {
            internal Func<HttpRequestMessage,HttpResponseMessage> Reply;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(Reply(request));}
        }
        static HttpResponseMessage JsonResponse(string json){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};}
        static Dictionary<string,object> RequestBody(HttpRequestMessage request){return (Dictionary<string,object>)Probe.Json.DeserializeObject(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());}
        static void ProviderContractChecks() {
            QwenWorkspaceChecks();
            const string key="contract-test-credential";const string input="今天要下雨。";
            Equal(9,ProviderProfiles.Translation.Length,"translation provider count");Equal(5,ProviderProfiles.Speech.Length,"cloud voice provider count");
            try {
                foreach(var provider in ProviderProfiles.Translation) {
                    var c=new Configuration();ProviderProfiles.Prepare(c,provider.Name,false);c.TranslationSecret=Configuration.Seal(key);
                    ProviderProfiles.ValidateProfile(provider.Name,false,provider.Default());
                    int calls=0;Services.HandlerFactory=delegate {return new ContractHandler {Reply=delegate(HttpRequestMessage request){
                        calls++;Equal(provider.Url.StartsWith(request.RequestUri.GetLeftPart(UriPartial.Authority)),true,provider.Name+" endpoint host");
                        Equal(false,request.RequestUri.Query.Contains(key),"credentials never occur in URL");
                        var body=RequestBody(request);string output;
                        if(provider.Name=="Claude") {
                            Equal(key,String.Join("",request.Headers.GetValues("x-api-key")),"Claude auth header");Equal(null,request.Headers.Authorization,"Claude omits Bearer");
                            Equal("2023-06-01",String.Join("",request.Headers.GetValues("anthropic-version")),"Claude version header");
                            Equal(input,((Dictionary<string,object>)((object[])body["messages"])[0])["content"],"Claude user text");Equal(true,body.ContainsKey("system"),"Claude top-level system");
                            output="{\"content\":[{\"type\":\"thinking\",\"thinking\":\"private reasoning\"},{\"type\":\"text\",\"text\":\"It will rain today.\"}]}";
                        }else if(provider.Name=="Gemini") {
                            Equal(key,String.Join("",request.Headers.GetValues("x-goog-api-key")),"Gemini auth header");Equal(null,request.Headers.Authorization,"Gemini omits Bearer");
                            Equal(true,request.RequestUri.AbsolutePath.EndsWith(provider.Model+":generateContent"),"Gemini model in route");
                            Equal(input,((Dictionary<string,object>)((object[])((Dictionary<string,object>)((object[])body["contents"])[0])["parts"])[0])["text"],"Gemini user text");
                            output="{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"thought\":true,\"text\":\"private reasoning\"},{\"text\":\"It will rain today.\"}]}}]}";
                        }else {
                            Equal(key,request.Headers.Authorization.Parameter,provider.Name+" Bearer credential");Equal(provider.Model,body["model"],provider.Name+" selected model");
                            Equal(input,((Dictionary<string,object>)((object[])body["messages"])[1])["content"],provider.Name+" separate user text");
                            output="{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"It will rain today.\"}}]}";
                        }
                        return JsonResponse(output);
                    }};};
                    Equal("It will rain today.",Services.Translate(c,input,CancellationToken.None).GetAwaiter().GetResult(),provider.Name+" translated response");Equal(1,calls,provider.Name+" no duplicate request");
                }
                byte[] wav=Services.PcmWave(new byte[4800],24000);string hex=BitConverter.ToString(wav).Replace("-","");
                foreach(var provider in ProviderProfiles.Speech) {
                    var c=new Configuration();ProviderProfiles.Prepare(c,provider.Name,true);c.SpeechSecret=Configuration.Seal(key);int calls=0;
                    Services.HandlerFactory=delegate {return new ContractHandler {Reply=delegate(HttpRequestMessage request){
                        calls++;if(provider.Name=="千问"&&calls==2){Equal(null,request.Headers.Authorization,"Qwen audio download never sends key");Equal(HttpMethod.Get,request.Method,"audio download is GET");return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(wav)};}
                        var body=RequestBody(request);
                        if(provider.Name=="ElevenLabs") {
                            Equal(key,String.Join("",request.Headers.GetValues("xi-api-key")),"ElevenLabs auth");Equal("?output_format=pcm_24000",request.RequestUri.Query,"ElevenLabs PCM format");Equal(true,request.RequestUri.AbsolutePath.EndsWith(provider.Voice),"ElevenLabs voice route");Equal("Hello.",body["text"],"ElevenLabs speech text");
                            return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[4800])};
                        }
                        Equal(key,request.Headers.Authorization.Parameter,provider.Name+" voice Bearer");Equal(provider.Model,body["model"],provider.Name+" voice model");
                        if(provider.Name=="千问"){Equal("Hello.",((Dictionary<string,object>)body["input"])["text"],"Qwen speech text");return JsonResponse("{\"output\":{\"audio\":{\"url\":\"https://audio.example.test/fixed.wav\"}}}");}
                        if(provider.Name=="MiniMax") {Equal("Hello.",body["text"],"MiniMax speech text");Equal("wav",((Dictionary<string,object>)body["audio_setting"])["format"],"MiniMax playable WAV");Equal(provider.Voice,((Dictionary<string,object>)body["voice_setting"])["voice_id"],"MiniMax voice selection");return JsonResponse("{\"base_resp\":{\"status_code\":0},\"data\":{\"audio\":\""+hex+"\"}}");}
                        Equal("Hello.",body["input"],provider.Name+" speech text");Equal("wav",body["response_format"],provider.Name+" playable WAV");return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(wav)};
                    }};};
                    Equal(.1,WaveAudio.Validate(Services.Speak(c,"Hello.",CancellationToken.None).GetAwaiter().GetResult()),provider.Name+" playable speech");Equal(provider.Name=="千问"?2:1,calls,provider.Name+" expected request count");
                }
                var sample=new Configuration {TranslationSecret=Configuration.Seal(key)};
                foreach(var status in new[]{HttpStatusCode.Unauthorized,(HttpStatusCode)429,HttpStatusCode.InternalServerError,HttpStatusCode.Redirect}) {
                    Services.HandlerFactory=delegate {return new ContractHandler {Reply=delegate {return new HttpResponseMessage(status){Content=new StringContent(key)};}};};
                    string message="";try{Services.Translate(sample,input,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException e){message=e.Message;}
                    Equal(true,message.Length>0,"HTTP failure reported: "+status);Equal(false,message.Contains(key),"failure body never exposes credentials");
                }
                Services.HandlerFactory=delegate {return new ContractHandler {Reply=delegate {return JsonResponse("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"truncated\"}}]}");}};};
                bool rejected=false;try{Services.Translate(sample,input,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){rejected=true;}Equal(true,rejected,"truncated translation is not treated as complete");
                Services.HandlerFactory=delegate {throw new Exception("Unexpected network request");};sample.TranslationSecret="";
                rejected=false;try{Services.Translate(sample,input,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){rejected=true;}Equal(true,rejected,"empty credential blocked before request");
                sample.TranslationSecret=Configuration.Seal(key);var canceled=new CancellationToken(true);
                rejected=false;try{Services.Translate(sample,input,canceled).GetAwaiter().GetResult();}catch(OperationCanceledException){rejected=true;}Equal(true,rejected,"canceled request never contacts provider");
            }finally {Services.HandlerFactory=null;}
            var legacy=new Configuration {SpeechUrl="https://sample.cn-beijing.maas.aliyuncs.com/api/v1/services/audio/tts/SpeechSynthesizer",SpeechModel="qwen-audio-3.1-tts-flash",Voice="Betty_v3.1",SpeechSecret=Configuration.Seal(key)};
            ProviderProfiles.SeedKeys(legacy);string url=legacy.SpeechUrl;ProviderProfiles.Apply(legacy,"OpenAI","other-contract-key","OpenAI","new-contract-key");
            Equal("",ProviderProfiles.Saved(legacy.TranslationKeys,"千问"),"other provider credential not copied to Qwen");
            ProviderProfiles.Apply(legacy,"DeepSeek","","千问",ProviderProfiles.Saved(legacy.SpeechKeys,"千问"));
            Equal(url,legacy.SpeechUrl,"Qwen workspace survives provider switch");Equal("Betty_v3.1",legacy.Voice,"Qwen voice survives provider switch");Equal(key,Configuration.Open(legacy.SpeechSecret),"Qwen key survives provider switch");
            var copied=Probe.Json.Deserialize<Configuration>(Probe.Json.Serialize(legacy));Equal("gpt-4o-mini-tts",copied.SpeechProfiles["OpenAI"].Model,"model profiles survive serialization");
            bool denied=false;try{ProviderProfiles.ValidateProfile("OpenAI",false,new ModelProfile {Url="https://api.moonshot.cn/v1/chat/completions",Model="example"});}catch(InvalidOperationException){denied=true;}Equal(true,denied,"provider and endpoint mismatch rejected");
        }
    }
}
