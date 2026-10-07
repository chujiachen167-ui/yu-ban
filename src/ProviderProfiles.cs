using System;
using System.Collections.Generic;
namespace EnglishCompanion {
    public sealed class ModelProfile { public string Url, Model, Voice; }
    internal sealed class Provider {
        internal string Name, Url, Model, Voice, Protocol, Portal, Docs;
        internal Provider(string name,string url,string model,string protocol,string portal,string docs,string voice="") {
            Name=name;Url=url;Model=model;Protocol=protocol;Portal=portal;Docs=docs;Voice=voice;
        }
        internal ModelProfile Default() {return new ModelProfile {Url=Url,Model=Model,Voice=Voice};}
    }
    internal static class ProviderProfiles {
        internal static readonly Provider[] Translation={
            new Provider("DeepSeek","https://api.deepseek.com/chat/completions","deepseek-flash","chat","https://platform.deepseek.com/api_keys","https://api-docs.deepseek.com/"),
            new Provider("千问","https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions","qwen-plus","chat","https://platform.qianwenai.com/home/api-keys","https://help.aliyun.com/zh/model-studio/"),
            new Provider("OpenAI","https://api.openai.com/v1/chat/completions","gpt-4.1-mini","chat","https://platform.openai.com/api-keys","https://developers.openai.com/api/docs/"),
            new Provider("Kimi","https://api.moonshot.cn/v1/chat/completions","kimi-k2.6","chat","https://platform.moonshot.cn/console/api-keys","https://platform.moonshot.cn/docs/guide/kimi-k2-quickstart"),
            new Provider("智谱","https://open.bigmodel.cn/api/paas/v4/chat/completions","glm-4.7-flash","chat","https://bigmodel.cn/usercenter/proj-mgmt/apikeys","https://docs.bigmodel.cn/"),
            new Provider("豆包 / 火山方舟","https://ark.cn-beijing.volces.com/api/v3/chat/completions","doubao-seed-2-0-mini-260428","chat","https://console.volcengine.com/ark/region:ark+cn-beijing/apiKey","https://docs.volcengine.com/docs/ark/chat-api?lang=zh"),
            new Provider("硅基流动","https://api.siliconflow.cn/v1/chat/completions","Qwen/Qwen3-8B","chat","https://cloud.siliconflow.cn/account/ak","https://docs.siliconflow.cn/docs/api/chat-completions-post"),
            new Provider("Gemini","https://generativelanguage.googleapis.com/v1beta/models/","gemini-2.5-flash-lite","gemini","https://aistudio.google.com/api-keys","https://ai.google.dev/api/generate-content"),
            new Provider("Claude","https://api.anthropic.com/v1/messages","claude-haiku-4-5","claude","https://platform.claude.com/settings/keys","https://platform.claude.com/docs/en/api/messages/create")
        };
        internal static readonly Provider[] Speech={
            new Provider("千问","https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation","qwen3-tts-flash","qwen","https://platform.qianwenai.com/home/api-keys","https://help.aliyun.com/zh/model-studio/qwen-tts-api","Cherry"),
            new Provider("OpenAI","https://api.openai.com/v1/audio/speech","gpt-4o-mini-tts","wav","https://platform.openai.com/api-keys","https://developers.openai.com/api/docs/guides/text-to-speech","coral"),
            new Provider("硅基流动","https://api.siliconflow.cn/v1/audio/speech","FunAudioLLM/CosyVoice2-0.5B","wav","https://cloud.siliconflow.cn/account/ak","https://docs.siliconflow.cn/docs/api/audio-speech-post","FunAudioLLM/CosyVoice2-0.5B:alex"),
            new Provider("MiniMax","https://api.minimax.cn/v1/t2a_v2","speech-2.8-hd","minimax","https://platform.minimax.cn/user-center/basic-information/interface-key","https://platform.minimax.cn/docs/api-reference/speech-t2a-http","English_expressive_narrator"),
            new Provider("ElevenLabs","https://api.elevenlabs.io/v1/text-to-speech/","eleven_multilingual_v2","pcm","https://elevenlabs.io/app/settings/api-keys","https://elevenlabs.io/docs/api-reference/text-to-speech/convert","JBFqnCBsd6RMkjVDRZzb")
        };
        internal static Provider Find(string name,bool speech) {foreach(var p in speech?Speech:Translation)if(p.Name==name)return p;return null;}
        static string Detect(string url,bool speech,string selected) {
            Uri uri;if(Uri.TryCreate(url,UriKind.Absolute,out uri)) {
                foreach(var p in speech?Speech:Translation)if(new Uri(p.Url).Host==uri.Host)return p.Name;
                if(uri.Host=="dashscope-intl.aliyuncs.com"||speech&&uri.Host.EndsWith(".maas.aliyuncs.com",StringComparison.OrdinalIgnoreCase))return "千问";
                if(speech&&uri.Host=="api.minimax.io")return "MiniMax";
            }
            return Find(selected,speech)!=null?selected:"原有配置";
        }
        internal static string TranslationProvider(Configuration c) {return Detect(c.TranslationUrl,false,c.TranslationService);}
        internal static string SpeechProvider(Configuration c) {return c.LocalVoice?"系统语音":Detect(c.SpeechUrl,true,c.SpeechService);}
        internal static void SeedKeys(Configuration c) {
            string t=TranslationProvider(c),s=SpeechProvider(c);
            if(!String.IsNullOrEmpty(c.TranslationSecret))c.TranslationKeys[t]=c.TranslationSecret;
            if(c.ReuseKey&&t==s)c.SpeechKeys[s]=c.TranslationSecret;
            else if(!String.IsNullOrEmpty(c.SpeechSecret)&&s!="系统语音")c.SpeechKeys[s]=c.SpeechSecret;
            if(!c.TranslationProfiles.ContainsKey(t))c.TranslationProfiles[t]=new ModelProfile {Url=c.TranslationUrl,Model=c.TranslationModel};
            if(s!="系统语音"&&!c.SpeechProfiles.ContainsKey(s))c.SpeechProfiles[s]=new ModelProfile {Url=c.SpeechUrl,Model=c.SpeechModel,Voice=c.Voice};
        }
        internal static ModelProfile Profile(Configuration c,string name,bool speech) {
            ModelProfile profile;var profiles=speech?c.SpeechProfiles:c.TranslationProfiles;
            if(profiles.TryGetValue(name,out profile)&&profile!=null)return new ModelProfile {Url=profile.Url,Model=profile.Model,Voice=profile.Voice};
            var p=Find(name,speech);if(p==null)throw new InvalidOperationException("请选择服务商");return p.Default();
        }
        internal static void Prepare(Configuration c,string name,bool speech) {
            if(speech&&name=="系统语音"){c.LocalVoice=true;return;}
            var profile=Profile(c,name,speech);
            (speech?c.SpeechProfiles:c.TranslationProfiles)[name]=profile;
            if(speech){c.LocalVoice=false;c.SpeechService=name;c.SpeechUrl=profile.Url;c.SpeechModel=profile.Model;c.Voice=profile.Voice;}
            else {c.TranslationService=name;c.TranslationUrl=profile.Url;c.TranslationModel=profile.Model;}
        }
        internal static void ValidateProfile(string name,bool speech,ModelProfile profile) {
            var uri=Services.Endpoint(profile.Url);var p=Find(name,speech);
            if(!uri.IsDefaultPort||!String.IsNullOrEmpty(uri.Query)||!String.IsNullOrEmpty(uri.Fragment))throw new InvalidOperationException("请填写不含查询参数的 HTTPS 接口地址");
            bool match=p==null||uri.Host==new Uri(p.Url).Host;
            if(name=="千问")match=match||uri.Host=="dashscope-intl.aliyuncs.com"||uri.Host.EndsWith(".cn-beijing.maas.aliyuncs.com",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".ap-southeast-1.maas.aliyuncs.com",StringComparison.OrdinalIgnoreCase);
            if(name=="MiniMax")match=match||uri.Host=="api.minimax.io";
            if(!match)throw new InvalidOperationException("接口域名与所选服务商不匹配");
            if(String.IsNullOrWhiteSpace(profile.Model)||profile.Model.Length>160)throw new InvalidOperationException("请填写平台上的完整模型名称");
            if(speech&&(String.IsNullOrWhiteSpace(profile.Voice)||profile.Voice.Length>200))throw new InvalidOperationException("请填写音色名称或音色 ID");
        }
        // 翻译与语音分开处理：翻译配置有效就保存，语音的问题只影响朗读。
        // 语音侧缺模型或缺工作空间时保留已填 Key 与地址，不阻断翻译，也不丢弃内容。
        // 第三个返回值是语音侧的阻断原因，空字符串表示朗读已就绪。
        internal static string Apply(Configuration c,string translation,string translationKey,string speech,string speechKey) {
            Prepare(c,translation,false);Prepare(c,speech,true);
            ValidateProfile(translation,false,Profile(c,translation,false));
            c.TranslationSecret=Configuration.Seal(translationKey.Trim());c.TranslationKeys[translation]=c.TranslationSecret;
            c.ReuseKey=false;
            if(c.LocalVoice)return "";
            string speechValue=speechKey.Trim();
            c.SpeechSecret=Configuration.Seal(speechValue);c.SpeechKeys[speech]=c.SpeechSecret;
            try {
                ValidateProfile(speech,true,Profile(c,speech,true));
                var profile=Profile(c,speech,true);
                if(QwenWorkspace.Required(profile,speechValue)&&!QwenWorkspace.Ready(profile))return "语音还缺工作空间配置";
                if(String.IsNullOrWhiteSpace(profile.Model))return "尚未确认语音模型";
            } catch(InvalidOperationException) { return "语音模型或地址尚未填好"; }
            return "";
        }
        // 翻译是否可用：只看翻译这一侧，语音配置不参与判断。
        internal static bool TranslationReady(Configuration c) { return Configuration.Open(c.TranslationSecret).Length>0; }
        // 朗读是否可用，以及不可用时该告诉用户哪一件事。
        internal static string SpeechBlocker(Configuration c) {
            if(c.LocalVoice)return "";
            if(String.IsNullOrWhiteSpace(c.SpeechModel))return "尚未确认语音模型";
            string key=Configuration.Open(c.SpeechSecret);
            if(key.Length==0)return "尚未填写语音 API Key";
            return "";
        }
        internal static string Saved(Dictionary<string,string> keys,string provider) {string value;return keys.TryGetValue(provider,out value)?Configuration.Open(value):"";}
    }
}

