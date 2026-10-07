// 阶段二检查：翻译路径不被语音配置挡住、失败可恢复、状态区分明确。
// 全部离线：只验证判定与恢复路径，不调用真实账户。
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnglishCompanion {
    internal static partial class Tests {
        sealed class Stage2Handler : HttpMessageHandler {
            internal Func<HttpRequestMessage, HttpResponseMessage> Reply;
            internal int Calls;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
                token.ThrowIfCancellationRequested();
                Calls++;
                return Task.FromResult(Reply(request));
            }
        }
        static HttpResponseMessage Text(string body) { return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }; }

        // 翻译可用性的判定：只看翻译这一侧，不牵连语音。
        static void TranslationReadyChecks() {
            // 有翻译 Key、无语音 Key：翻译必须可用。
            var only = new Configuration { TranslationSecret = Configuration.Seal("translation-only-key") };
            Equal(true, TranslationReady(only), "translation works with a translation key alone");
            // 系统语音不需要 Key，也不应影响翻译。
            var local = new Configuration { TranslationSecret = Configuration.Seal("k"), LocalVoice = true };
            Equal(true, TranslationReady(local), "system voice does not block translation");
            // 语音缺工作空间 / 缺模型：仍然只影响朗读，翻译照常。
            var broken = new Configuration { TranslationSecret = Configuration.Seal("k") };
            broken.SpeechModel = "";                                   // 未确认额度模型
            Equal(true, TranslationReady(broken), "unconfirmed speech model does not block translation");
            broken.SpeechModel = "qwen-audio-3.1-tts-flash";
            broken.SpeechUrl = "https://ws-broken.cn-beijing.maas.aliyuncs.com/api/v1/services/audio/tts/SpeechSynthesizer";
            Equal(true, TranslationReady(broken), "incomplete speech workspace does not block translation");
            // 只有真的没有翻译 Key 时才算不可用。
            Equal(false, TranslationReady(new Configuration()), "missing translation key is the only blocker");
            // 语音侧的阻断原因要能独立说明，且不要求用户先理解翻译错误。
            Equal("尚未填写语音 API Key", SpeechBlocker(only), "missing speech key is reported on the speech side only");
            Equal("", SpeechBlocker(local), "system voice has no blocker at all");
            // 无论语音处于什么状态，翻译都必须可用。
            foreach (var speech in new[] { "", "sk-ws-x", "not-a-key" }) {
                var probe = new Configuration();
                ProviderProfiles.Apply(probe, "DeepSeek", "translation-key", "千问", speech);
                Equal(true, ProviderProfiles.TranslationReady(probe), "translation stays usable whatever the speech field holds");
            }
        }

        // 朗读失败的分类：让用户知道下一步该做什么，而不是笼统的“失败”。
        static void FailurePathChecks() {
            var c = new Configuration { TranslationSecret = Configuration.Seal("k") };
            Func<string> run = delegate { Services.TranslatePaired(c, "今天下雨。", CancellationToken.None).GetAwaiter().GetResult(); return ""; };
            // 401：Key 问题，明确指向重新填写。
            Services.HandlerFactory = delegate { return new Stage2Handler { Reply = delegate { return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("") }; } }; };
            Equal(true, Mention("API Key 无效", run), "invalid key error names the key");
            // 429：额度/频率问题。
            Services.HandlerFactory = delegate { return new Stage2Handler { Reply = delegate { return new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("") }; } }; };
            Equal(true, Mention("额度", run), "quota error names the quota");
            // 400：模型名写错，指向设置而不是让用户重打。
            Services.HandlerFactory = delegate { return new Stage2Handler { Reply = delegate { return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("") }; } }; };
            Equal(true, Mention("设置", run), "bad request error points at settings");
            Services.HandlerFactory = null;
            // 取消：旧请求必须安静退出，不能当成失败提示给用户。
            var handler = new Stage2Handler { Reply = delegate { return Text("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"ok\"}}]}"); } };
            Services.HandlerFactory = delegate { handler.Calls = 0; return handler; };
            var source = new Configuration { TranslationSecret = Configuration.Seal("k") };
            bool surfaced = false;
            try { Services.TranslatePaired(source, "今天下雨。", new CancellationToken(true)).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { /* 取消是正常路径 */ }
            catch (Exception) { surfaced = true; }
            Services.HandlerFactory = null;
            Equal(false, surfaced, "cancellation is never reported as a failure");
            Equal(0, handler.Calls, "canceled translation never contacts the provider");
        }

        static bool Mention(string needle, Func<string> action) {
            try { action(); return false; }
            catch (Exception e) { var m = e as InvalidOperationException; return m != null && m.Message.Contains(needle); }
        }

        internal static bool TranslationReady(Configuration c) { return ProviderProfiles.TranslationReady(c); }
        internal static string SpeechBlocker(Configuration c) { return ProviderProfiles.SpeechBlocker(c); }

        // 保存路径：翻译与语音分开，语音没配好不阻断翻译，也不丢已填内容。
        static void SplitSaveChecks() {
            // 场景一：只有翻译 Key，语音什么都没填 —— 这是首次使用最常见的路径。
            var first = new Configuration();
            string blocker = ProviderProfiles.Apply(first, "DeepSeek", "translation-key-only", "千问", "");
            Equal("translation-key-only", Configuration.Open(first.TranslationSecret), "translation key is saved even with no speech key");
            Equal(true, ProviderProfiles.TranslationReady(first), "translation is usable right after saving with speech empty");
            Equal("尚未填写语音 API Key", ProviderProfiles.SpeechBlocker(first), "speech is reported as missing, not blocking");
            // 语音没配好时，翻译这一侧必须完全可用。
            Equal(true, ProviderProfiles.TranslationReady(first), "speech gaps never disable translation");

            // 场景二：填了千问工作空间 Key，但工作空间没配 —— 以前这一项会阻断整个保存。
            var workspace = new Configuration();
            blocker = ProviderProfiles.Apply(workspace, "DeepSeek", "translation-key", "千问", "sk-ws-not-configured");
            Equal("translation-key", Configuration.Open(workspace.TranslationSecret), "incomplete speech workspace no longer blocks saving the translation key");
            Equal(true, ProviderProfiles.TranslationReady(workspace), "translation works while speech workspace is incomplete");
            Equal(true, blocker.Length > 0, "incomplete speech is reported as a speech-side blocker");
            Equal("sk-ws-not-configured", Configuration.Open(workspace.SpeechSecret), "entered speech key is retained, not discarded");
            // 阻断原因只提到语音，不牵连翻译。
            Equal(false, blocker.Contains("翻译"), "the blocker never blames the translation side");

            // 场景三：语音配置完整 —— 朗读应就绪，不报阻断。
            var complete = new Configuration();
            complete.SpeechProfiles["千问"] = new ModelProfile { Url = "https://ws-ready.cn-beijing.maas.aliyuncs.com/api/v1/services/audio/tts/SpeechSynthesizer", Model = "qwen-audio-3.1-tts-flash", Voice = "Betty_v3.1" };
            ProviderProfiles.Prepare(complete, "千问", true);
            blocker = ProviderProfiles.Apply(complete, "DeepSeek", "translation-key", "千问", "sk-ws-ready");
            Equal("", blocker, "complete speech configuration reports no blocker");
            Equal("", ProviderProfiles.SpeechBlocker(complete), "complete speech is ready to read aloud");

            // 场景四：系统语音不需要 Key，也不该产生任何阻断。
            var local = new Configuration();
            ProviderProfiles.Apply(local, "DeepSeek", "translation-key", "系统语音", "");
            Equal(true, local.LocalVoice, "system voice selected");
            Equal("", ProviderProfiles.SpeechBlocker(local), "system voice never blocks reading aloud");

            // 场景五：翻译那一侧真的填错时，仍然要拦下来 —— 不能为了不拦语音就连翻译也放过。
            var bad = new Configuration();
            bad.TranslationProfiles["DeepSeek"] = new ModelProfile { Url = "https://api.moonshot.cn/v1/chat/completions", Model = "deepseek-flash" };
            bool rejected = false;
            try { ProviderProfiles.Apply(bad, "DeepSeek", "translation-key", "系统语音", ""); }
            catch (InvalidOperationException) { rejected = true; }
            Equal(true, rejected, "a translation endpoint that does not match its provider is still rejected");
            // 但语音侧的缺项不会走到这里。
            var good = new Configuration();
            ProviderProfiles.Apply(good, "DeepSeek", "translation-key", "系统语音", "");
            Equal(true, ProviderProfiles.TranslationReady(good), "a valid translation saves normally");
        }

        static void Stage2Checks() { TranslationReadyChecks(); FailurePathChecks(); SplitSaveChecks(); }
    }
}
