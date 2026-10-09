// 句组对照的离线检查：切句、逗号级分句、方向、单次请求、ID 校验、缺组降级与取消。
// 不调用真实账户。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnglishCompanion {
    internal static partial class Tests {
        sealed class PairHandler : HttpMessageHandler {
            internal Func<HttpRequestMessage, string> Reply;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Reply(request), Encoding.UTF8, "application/json") });
            }
        }
        static string Completion(string content) { return "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":" + Probe.Json.Serialize(content) + "}}]}"; }
        static string BodyOf(HttpRequestMessage request) { return request.Content.ReadAsStringAsync().GetAwaiter().GetResult(); }

        // 捕获一个可变对象，而不是 out 参数（C# 5 不允许在 lambda 里使用 out 变量）。
        sealed class PairRun { internal int Calls; internal string Request = ""; }

        static PairResult RunPair(string reply, PairRun run) {
            return RunPair(reply, "今天我想早点出门。\n因为晚上可能会下雨。", run);
        }
        static PairResult RunPair(string reply, string source, PairRun run) {
            run.Calls = 0; run.Request = "";
            var config = new Configuration { TranslationSecret = Configuration.Seal("offline-pair-contract") };
            Services.HandlerFactory = delegate {
                return new PairHandler { Reply = delegate(HttpRequestMessage request) { run.Calls++; run.Request = BodyOf(request); return Completion(reply); } };
            };
            try { return Services.TranslatePaired(config, source, CancellationToken.None).GetAwaiter().GetResult(); }
            finally { Services.HandlerFactory = null; }
        }

        static void SentencePairChecks() {
            // --- 句末标点仍然断句 ---
            var split = Sentences.Split("今天我想早点出门。因为晚上可能会下雨！明天呢？");
            Equal(3, split.Count, "sentence terminators still split");
            Equal("今天我想早点出门。", split[0], "first Chinese segment is verbatim");
            Equal(2, Sentences.Split("Line one here.\nLine two here.").Count, "English newline segments");
            // --- 逗号级切分：一句里按逗号、顿号再分 ---
            var clause = Sentences.Split("明天要去见客户，还得带上合同，然后送他去机场。");
            Equal(3, clause.Count, "commas inside one sentence split into clauses");
            Equal("明天要去见客户，", clause[0], "clause keeps its own trailing comma");
            Equal("还得带上合同，", clause[1], "second clause keeps its comma");
            Equal("然后送他去机场。", clause[2], "last clause ends with the full stop");
            Equal(2, Sentences.Split("我想买苹果、香蕉和橘子。").Count, "顿号 splits, the tail stays whole");
            // 分号按用户决定不断开
            Equal(1, Sentences.Split("他来了；然后走了。").Count, "semicolon does not split");
            // 连词引导的后半句不断开
            Equal(1, Sentences.Split("他来了，不过没说话。").Count, "Chinese connector keeps the clause whole");
            Equal(1, Sentences.Split("It rained, but we still went out.").Count, "English connector keeps the clause whole");
            Equal(1, Sentences.Split("She waited, and then she left.").Count, "and-then keeps the clause whole");
            // 逗号后不是连词则切开
            Equal(2, Sentences.Split("I packed my bag, then left the house.").Count, "plain comma splits");
            Equal(2, Sentences.Split("我先去买菜，晚上回家做饭。").Count, "plain Chinese comma splits");
            // --- 无标点口语：一整段仍是一个句组，不丢原文 ---
            var spoken = Sentences.Split("今天天气真不错啊我们出去走走吧");
            Equal(1, spoken.Count, "unpunctuated speech stays one group");
            Equal("今天天气真不错啊我们出去走走吧", spoken[0], "unpunctuated text preserved verbatim");
            // --- 引号、缩写、小数 ---
            Equal(2, Sentences.Split("他说“今天下雨”。然后我们就不去了。").Count, "closing quote stays with its sentence");
            Equal(1, Sentences.Split("It costs 3.5 dollars in total.").Count, "decimal point does not split");
            Equal(1, Sentences.Split("I work for Dr. Chen and I like it.").Count, "abbreviation does not split");
            Equal(1, Sentences.Split("We pack etc. and ship it tomorrow.").Count, "trailing abbreviation does not split");
            // 逗号切 + 问号切：这是两个独立切点，各自成组
            Equal(3, Sentences.Split("Wait, what? I have to go now.").Count, "comma and question mark both split");
            Equal(2, Sentences.Split("We finished at nine, etc. and left.").Count, "comma before an abbreviation still splits");
            // --- 方向 ---
            Equal(TextDirection.ChineseToEnglish, Sentences.Detect("今天我想早点出门。"), "Chinese input direction");
            Equal(TextDirection.EnglishToChinese, Sentences.Detect("I want to leave early today."), "English input direction");
            Equal(TextDirection.ChineseToEnglish, Sentences.Detect("今天我想早点出门 leave early。"), "mixed input with more Chinese leans Chinese");
            Equal(TextDirection.EnglishToChinese, Sentences.Detect("I want to leave early 早一点出门。"), "mixed input with more English leans English");
            Equal("English", Sentences.TargetLanguage("Auto", TextDirection.ChineseToEnglish), "Auto follows the input direction");
            // 设置里写着 English：用户输中文就得译成英文（正常路径）。
            Equal("English", Sentences.TargetLanguage("English", TextDirection.ChineseToEnglish), "a user learning English gets English from Chinese input");
            // 但同一把设置下输入英文，说明是在查另一种语言，必须译成中文而不是又回英文。
            Equal("Chinese", Sentences.TargetLanguage("English", TextDirection.EnglishToChinese), "English input flips to Chinese instead of echoing back English");
            Equal("Chinese", Sentences.TargetLanguage("Auto", TextDirection.EnglishToChinese), "English input yields Chinese pairs");
            // 回归：默认 Language 写死为 English。用户没动过设置时输入英文，必须译成中文。
            Equal("Chinese", Sentences.TargetLanguage(Configuration.DefaultLanguage, TextDirection.EnglishToChinese), "default config still flips direction for English input");
            Equal("English", Sentences.TargetLanguage(Configuration.DefaultLanguage, TextDirection.ChineseToEnglish), "default config keeps English target for Chinese input");
            // --- 稳定 ID，原文不可改写 ---
            var groups = Sentences.Groups("第一句。第二句。第三句。");
            Equal(3, groups.Count, "group count follows sentence count");
            Equal("s1", groups[0].Id, "first stable id");
            Equal("s3", groups[2].Id, "ids are sequential and stable");
            var clauseGroups = Sentences.Groups("明天要去见客户，还得带上合同。");
            Equal(2, clauseGroups.Count, "one sentence with a comma becomes two groups");
            Equal("s1", clauseGroups[0].Id, "clause ids stay stable");
            Equal("明天要去见客户，", clauseGroups[0].Source, "clause source is verbatim from the input");
            // --- 一次请求带回完整上下文 ---
            var run = new PairRun();
            var ok = RunPair("{\"target_language\":\"English\",\"segments\":[{\"id\":\"s1\",\"text\":\"I want to leave early today.\"},{\"id\":\"s2\",\"text\":\"It might rain this evening.\"}]}", run);
            Equal(1, run.Calls, "paired translation issues exactly one request");
            Equal(2, ok.Pairs.Count, "both groups paired");
            Equal("今天我想早点出门。", ok.Pairs[0].Source, "source comes from the input, never the model");
            Equal("I want to leave early today.", ok.Pairs[0].Target, "first paired translation");
            Equal("It might rain this evening.", ok.Pairs[1].Target, "second paired translation");
            Equal("I want to leave early today. It might rain this evening.", ok.Target, "complete translation joins every group");
            Equal(true, run.Request.Contains("id\\\":\\\"s1") && run.Request.Contains("id\\\":\\\"s2"), "request carries group ids");
            Equal(true, run.Request.Contains("今天我想早点出门。"), "request carries the full source context");
            // --- 缺组按用户选择 A：只显示真对齐的那几组，不伪造、不回退整句 ---
            var missing = RunPair("{\"segments\":[{\"id\":\"s1\",\"text\":\"Only one.\"}]}", run);
            Equal(1, run.Calls, "missing group still costs one request");
            Equal(2, missing.Pairs.Count, "every source clause is still tracked");
            Equal("Only one.", missing.Pairs[0].Target, "the aligned group keeps its translation");
            Equal("", missing.Pairs[1].Target, "the unaligned group carries no invented text");
            Equal("Only one.", missing.Target, "copy and speech text contains only aligned translations");
            // 浮窗只渲染带译文的组，未对齐的组不会出现在屏幕上
            Equal(1, missing.Pairs.Count(p => p.Target != null && p.Target.Length > 0), "only aligned groups are displayable");
            // 一个都没对齐才退回整段排版
            var none = RunPair("{\"segments\":[{\"id\":\"s9\",\"text\":\"Nothing matches.\"}]}", run);
            Equal(0, none.Pairs.Count, "no aligned group means no pairing at all");
            Equal("Nothing matches.", none.Target, "unmatched reply is kept as plain text");
            // 重复 ID 视为返回不可信：整段退回，不猜对应关系
            var duplicate = RunPair("{\"segments\":[{\"id\":\"s1\",\"text\":\"A.\"},{\"id\":\"s1\",\"text\":\"B.\"},{\"id\":\"s2\",\"text\":\"C.\"}]}", run);
            Equal(0, duplicate.Pairs.Count(p => p.Target != null && p.Target.Length > 0), "duplicate ids never produce a displayable pairing");
            Equal(1, run.Calls, "duplicate ids still cost exactly one request");
            var garbage = RunPair("Sorry, I cannot help with that.", run);
            Equal(0, garbage.Pairs.Count, "unstructured reply never becomes a pairing");
            Equal("Sorry, I cannot help with that.", garbage.Target, "unstructured reply is kept as plain text");
            var fenced = RunPair("```json\n{\"segments\":[{\"id\":\"s1\",\"text\":\"A.\"},{\"id\":\"s2\",\"text\":\"B.\"}]}\n```", run);
            Equal(2, fenced.Pairs.Count, "fenced json still pairs when ids are exact");            // 乱序但齐全：按 ID 还原，不按返回顺序错位。
            var reordered = RunPair("{\"segments\":[{\"id\":\"s2\",\"text\":\"It might rain this evening.\"},{\"id\":\"s1\",\"text\":\"I want to leave early today.\"}]}", run);
            Equal(2, reordered.Pairs.Count, "reordered but complete ids still pair");
            Equal("I want to leave early today.", reordered.Pairs[0].Target, "reordered reply is realigned by id, not by position");
            // --- 逗号级：3 组只对齐 1 组时，屏幕只显示 1 组 ---
            Equal(3, Sentences.Split("明天要去见客户，还得带上合同，然后送他去机场。").Count, "source really has three clauses");
            var partial = RunPair("{\"segments\":[{\"id\":\"s1\",\"text\":\"Meet a client tomorrow,\"}]}", "明天要去见客户，还得带上合同，然后送他去机场。", run);
            Equal(3, partial.Pairs.Count, "clause mode tracks every source clause");
            Equal("明天要去见客户，", partial.Pairs[0].Source, "clause mode source stays verbatim");
            Equal("Meet a client tomorrow,", partial.Target, "clause mode copy text is the aligned translation only");
            Equal(1, partial.Pairs.Count(p => p.Target != null && p.Target.Length > 0), "only the aligned clause is displayable");
            Equal(0, partial.Pairs[1].Target.Length, "unaligned clauses never receive invented text");
            // --- 取消：旧请求不得覆盖新输入 ---
            var sample = new Configuration { TranslationSecret = Configuration.Seal("offline-pair-contract") };
            Services.HandlerFactory = delegate { return new PairHandler { Reply = delegate { return Completion("{\"segments\":[{\"id\":\"s1\",\"text\":\"A.\"},{\"id\":\"s2\",\"text\":\"B.\"}]}"); } }; };
            bool canceled = false;
            try { Services.TranslatePaired(sample, "今天我想早点出门。\n因为晚上可能会下雨。", new CancellationToken(true)).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { canceled = true; }
            finally { Services.HandlerFactory = null; }
            Equal(true, canceled, "a canceled request never contacts the provider");
            // --- 一源句对多句译文：仍然作为同一组 ---
            var parsed = Services.ParseSegments("{\"segments\":[{\"id\":\"s1\",\"text\":\"First half. Second half.\"},{\"id\":\"s2\",\"text\":\"B.\"}]}");
            Equal(2, parsed.Count, "one source sentence may carry a multi-sentence translation");
            Equal("First half. Second half.", parsed[0].Value, "multi-sentence translation is preserved whole");
            // --- 解析容错与拒绝 ---
            Equal(1, Services.ParseSegments("Here you go: {\"segments\":[{\"id\":\"s1\",\"text\":\"A.\"}]} done").Count, "surrounding prose is tolerated");
            bool rejected = false;
            try { Services.ParseSegments("no json at all"); } catch (InvalidOperationException) { rejected = true; }
            Equal(true, rejected, "a reply with no structure is rejected instead of guessed");
            rejected = false;
            try { Services.ParseSegments("{\"segments\":[]}"); } catch (InvalidOperationException) { rejected = true; }
            Equal(true, rejected, "an empty segment list is rejected");
        }
    }
}
