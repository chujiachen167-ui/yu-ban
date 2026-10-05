using System;
using System.Text.RegularExpressions;
namespace EnglishCompanion {
    internal static class QwenWorkspace {
        internal const string Guide="https://help.aliyun.com/zh/model-studio/obtain-the-app-id-and-workspace-id";
        internal const string AudioGuide="https://help.aliyun.com/zh/model-studio/non-realtime-tts-user-guide";
        internal const string Console="https://bailian.console.aliyun.com/";
        internal const string Path="/api/v1/services/audio/tts/SpeechSynthesizer";
        internal static bool Required(ModelProfile profile,string key) {
            return (key??"").Trim().StartsWith("sk-ws-",StringComparison.Ordinal)||
                profile!=null&&(profile.Model??"").StartsWith("qwen-audio-",StringComparison.OrdinalIgnoreCase)||Id(profile)!="";
        }
        internal static string Id(ModelProfile profile) {
            Uri uri;if(profile==null||!Uri.TryCreate(profile.Url,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!="")return "";
            foreach(string region in new[]{"cn-beijing","ap-southeast-1"}) {
                string suffix="."+region+".maas.aliyuncs.com";
                if(uri.Host.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)) {
                    string id=uri.Host.Substring(0,uri.Host.Length-suffix.Length);
                    return Regex.IsMatch(id,"^[a-zA-Z0-9][a-zA-Z0-9-]{0,62}$")?id:"";
                }
            }
            return "";
        }
        internal static bool Ready(ModelProfile profile) {Uri uri;return Id(profile)!=""&&Uri.TryCreate(profile.Url,UriKind.Absolute,out uri)&&uri.AbsolutePath==Path;}
        internal static string Region(ModelProfile profile) {return profile!=null&&(profile.Url??"").Contains(".ap-southeast-1.maas.aliyuncs.com")?"ap-southeast-1":"cn-beijing";}
        internal static ModelProfile Create(string id,string region,string model,string voice) {
            id=(id??"").Trim();
            if(!Regex.IsMatch(id,"^[a-zA-Z0-9][a-zA-Z0-9-]{0,62}$"))throw new InvalidOperationException("请填写控制台里的工作空间 ID，通常以 ws- 开头；这里不是 API Key。");
            if(region!="cn-beijing"&&region!="ap-southeast-1")throw new InvalidOperationException("请选择工作空间对应的地域。");
            var profile=new ModelProfile {Url="https://"+id+"."+region+".maas.aliyuncs.com"+Path,Model=(model??"").Trim(),Voice=(voice??"").Trim()};
            ProviderProfiles.ValidateProfile("千问",true,profile);return profile;
        }
    }
}
