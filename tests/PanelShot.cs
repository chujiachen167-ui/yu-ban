// 面板真实渲染截图：把浮窗画到 PNG，供人工核对排版、边框与滚动。
// 只用内置演示数据，不联网、不读取用户配置。
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EnglishCompanion {
    internal static class PanelShot {
        [STAThread] internal static int Capture(string directory) {
            Directory.CreateDirectory(directory);
            using (var panel = new Overlay(true)) {
                panel.InspectableDemo();
                var result = new PairResult { Source = "明天要去见客户，还得带上合同，然后送他去机场。", Direction = "English" };
                result.Pairs.Add(new SentencePair { Id = "s1", Source = "明天要去见客户，", Target = "I need to meet a client tomorrow," });
                result.Pairs.Add(new SentencePair { Id = "s2", Source = "还得带上合同，", Target = "and I also have to bring the contract," });
                result.Pairs.Add(new SentencePair { Id = "s3", Source = "然后送他去机场。", Target = "and then drive him to the airport." });
                result.Target = "I need to meet a client tomorrow, and I also have to bring the contract, and then drive him to the airport.";
                panel.Original.Text = result.Source;
                panel.Translation.Text = result.Target;
                panel.ShowPairs(result);
                panel.Learnable = true;
                panel.ApplySkin("glass");
                // 走真实使用路径：Follow 会先显示窗口，再按真实文本高度对齐。
                panel.Follow(new System.Drawing.Rectangle(60, 500, 2, 20));
                var window = typeof(Overlay).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(panel) as Window;
                Pump(600);
                Shot(window, Path.Combine(directory, "panel-glass.png"), 0);

                panel.ApplySkin("ocean"); Pump();
                Shot(window, Path.Combine(directory, "panel-ocean.png"), 0);
                panel.ApplySkin("baby"); Pump();
                Shot(window, Path.Combine(directory, "panel-baby.png"), 0);

                // 展开 + 仅译文
                panel.ApplySkin("glass");
                panel.ShowOriginal = false; panel.SetExpanded(true); Pump(600);
                Shot(window, Path.Combine(directory, "panel-glass-expanded-translation-only.png"), 0);
                panel.ShowOriginal = true; Pump(400);
                Shot(window, Path.Combine(directory, "panel-glass-expanded.png"), 0);

                // 超长文本：应有滚动条且无横向滚动
                var many = new PairResult { Source = "长" };
                var join = new System.Text.StringBuilder();
                for (int i = 0; i < 24; i++) {
                    many.Pairs.Add(new SentencePair { Id = "s" + i, Source = "这是第" + i + "句中文原文，用来撑开滚动区域。", Target = "This is English sentence number " + i + ", long enough to make the panel scroll vertically." });
                    if (i > 0) join.Append(' ');
                    join.Append(many.Pairs[i].Target);
                }
                many.Target = join.ToString();
                panel.Translation.Text = many.Target; panel.ShowPairs(many); Pump(600);
                Shot(window, Path.Combine(directory, "panel-glass-long.png"), 0);

                // 连词不断开：一句里的「不过」前后保持同一组
                var joined = new PairResult { Source = "他来了，不过没说话。", Direction = "English" };
                joined.Pairs.Add(new SentencePair { Id = "s1", Source = "他来了，不过没说话。", Target = "He came, but he did not say anything." });
                joined.Target = "He came, but he did not say anything.";
                panel.Translation.Text = joined.Target; panel.ShowPairs(joined); Pump(600);
                Shot(window, Path.Combine(directory, "panel-glass-connector.png"), 0);

                // 四种状态：翻译中 / 失败 / 朗读中
                var state = new PairResult { Source = "明天要去见客户，还得带上合同。", Direction = "English" };
                state.Pairs.Add(new SentencePair { Id = "s1", Source = "明天要去见客户，", Target = "I need to meet a client tomorrow," });
                state.Pairs.Add(new SentencePair { Id = "s2", Source = "还得带上合同。", Target = "and I also have to bring the contract." });
                state.Target = "I need to meet a client tomorrow, and I also have to bring the contract.";
                panel.Translation.Text = state.Target; panel.ShowPairs(state); panel.Learnable = true;
                panel.Retry.Enabled = true; Pump(300);
                panel.SetTranslatingState(); Pump(500);
                Shot(window, Path.Combine(directory, "state-translating.png"), 0);
                panel.SetTranslating(false);
                panel.SetFailed("请求过于频繁或额度不足，请稍后重试", false); Pump(400);
                Shot(window, Path.Combine(directory, "state-failed.png"), 0);
                panel.SetFailed("", false);
                panel.SetPlayback(PlaybackState.Preparing); Pump(400);
                Shot(window, Path.Combine(directory, "state-speaking.png"), 0);
                panel.SetPlayback(PlaybackState.Idle);
                window.Close();
            }
            Console.WriteLine("Panel screenshots written to " + directory);
            return 0;
        }
        static void Pump(int ms = 500) {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        static void Shot(Window window, string path, int extra) {
            int w = (int)Math.Ceiling(window.ActualWidth), h = (int)Math.Ceiling(window.ActualHeight);
            if (w <= 0 || h <= 0) return;
            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
