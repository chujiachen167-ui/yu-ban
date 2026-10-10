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
            // 朗读偏好窗口：音色列表的实际内容
            foreach (var model in new[] { "qwen-audio-3.1-tts-flash", "qwen3-tts-flash" }) {
                var c = new Configuration { SpeechModel = model, Theme = "glass" };
                ProviderProfiles.Prepare(c, "千问", true); c.SpeechModel = model;
                var owner = new Window { Width = 1, Height = 1, ShowInTaskbar = false, Left = -5000, Top = -5000 };
                owner.Show();
                var options = new SpeechOptionsWindow(owner, c);
                var win = typeof(SpeechOptionsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(options) as Window;
                win.Left = 60; win.Top = 60; win.Show();
                Pump(500);
                var combo = (System.Windows.Controls.ComboBox)typeof(SpeechOptionsWindow).GetField("voice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(options);
                combo.IsDropDownOpen = true;
                Pump(300);
                combo.IsDropDownOpen = false;
                Pump(300);
                // 下拉列表是独立的 Popup，单独把它渲染出来。
                var list = combo.Template.FindName("PART_Popup", combo) as System.Windows.Controls.Primitives.Popup;
                if (list != null && list.Child != null) {
                    var host = list.Child as System.Windows.FrameworkElement;
                    if (host != null) {
                        host.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                        host.UpdateLayout();
                        int hh = (int)Math.Ceiling(host.ActualHeight);
                        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(host.ActualWidth)), Math.Max(1, hh), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        bmp.Render(host);
                        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                        using (var fs = System.IO.File.Create(System.IO.Path.Combine(directory, "voicelist-" + (model.Contains("3.1") ? "31" : "30") + ".png"))) enc.Save(fs);
                    }
                }
                win.Close(); owner.Close();
            }
            // 能力提示：两个模型各自能做什么
            foreach (var model in new[] { "qwen3-tts-flash", "qwen-audio-3.1-tts-flash" }) {
                var c = new Configuration { SpeechModel = model, Theme = "glass" };
                ProviderProfiles.Prepare(c, "千问", true); c.SpeechModel = model;
                var owner = new Window { Width = 1, Height = 1, ShowInTaskbar = false, Left = -5000, Top = -5000 };
                owner.Show();
                var options = new SpeechOptionsWindow(owner, c);
                var win = typeof(SpeechOptionsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(options) as Window;
                win.Left = 60; win.Top = 60; win.Show();
                Pump(500);
                Shot(win, System.IO.Path.Combine(directory, "capability-" + (model.Contains("3.1") ? "31" : "30") + ".png"), 0);
                win.Close(); owner.Close();
            }
            // 设置页顶部的语言入口：用户在这里决定自己学什么
            foreach (var theme in new[] { "glass", "ocean", "baby" }) {
                using (var settings = new SettingsWindow(new Configuration { Theme = theme, Language = "English" }, true)) {
                    var w = typeof(SettingsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(settings) as Window;
                    w.Left = 40; w.Top = 40; w.Show();
                    Pump(700);
                    // 报告真实布局：内容高度、窗口高度、是否溢出、确认按钮位置。
                    var root = w.Content as FrameworkElement;
                    var confirm = w.FindName("Confirm") as System.Windows.Controls.Button;
                    double need = root == null ? 0 : root.DesiredSize.Height;
                    var entry = w.FindName("LearningEntry") as System.Windows.Controls.Button;
                    Console.WriteLine("Layout[" + theme + "] window=" + w.ActualHeight
                        + " contentNeeded=" + Math.Round(need)
                        + " overflow=" + (need > w.ActualHeight ? "YES" : "no")
                        + (entry != null ? " languageEntry=" + entry.ActualHeight + "px/" + ((System.Windows.Controls.TextBlock)w.FindName("LearningLabel")).Text : ""));
                    // 卡片不能压到皮肤角色：三套皮肤的角色都占顶部一段，
                // 内容区起点必须让开，否则角色脸会被卡片盖住。
                var firstCard = w.FindName("TranslationCard") as System.Windows.Controls.Border;
                double top = firstCard == null ? 0 : firstCard.TranslatePoint(new Point(0, 0), w).Y;
                Console.WriteLine("CardTop[" + theme + "]=" + Math.Round(top)
                    + " => " + (theme == "glass" ? "(default skin has no character)" : (top < 200 ? "TOO HIGH — overlaps the character" : "clears the character")));
                Shot(w, System.IO.Path.Combine(directory, "settings-" + theme + ".png"), 0);
                    // 上拉列表：点开入口，确认它是往上弹而不是被窗口裁掉。
                    if (theme == "glass") {
                        entry.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        Pump(500);
                        double popupTop = double.NaN, entryTop = entry.TranslatePoint(new Point(0, 0), w).Y;
                        var field = typeof(SettingsWindow).GetField("learningPopup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var popup = field.GetValue(settings) as System.Windows.Controls.Primitives.Popup;
                        var popupContent = popup == null ? null : popup.Child as FrameworkElement;
                        if (popupContent != null) {
                            popupTop = popupContent.TranslatePoint(new Point(0, 0), w).Y;
                        }
                        Console.WriteLine("LearningPopup: entryTop=" + Math.Round(entryTop)
                            + " popupTop=" + (double.IsNaN(popupTop) ? "n/a" : Math.Round(popupTop).ToString())
                            + " => " + (!double.IsNaN(popupTop) && popupTop < entryTop ? "opens upward (correct)" : "CHECK PLACEMENT"));
                        if (popupContent != null) {
                            var enc2 = new PngBitmapEncoder();
                            var bmp2 = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(popupContent.ActualWidth)), Math.Max(1, (int)Math.Ceiling(popupContent.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
                            bmp2.Render(popupContent);
                            enc2.Frames.Add(BitmapFrame.Create(bmp2));
                            using (var fs2 = System.IO.File.Create(System.IO.Path.Combine(directory, "language-popup.png"))) enc2.Save(fs2);
                        }
                        if (popup != null) popup.IsOpen = false;
                    }
                    w.Close();
                }
            }
            // 朗读偏好窗口：控件一致性必须和主设置页一样，用户会两个窗口都看。
            foreach (var theme in new[] { "glass", "baby" }) {
                using (var owner = new SettingsWindow(new Configuration { Theme = theme }, true)) {
                    var ow = typeof(SettingsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(owner) as Window;
                    ow.Left = 30; ow.Top = 30; ow.Show(); Pump(400);
                    var cfg = new Configuration { Theme = theme, SpeechModel = "qwen-audio-3.1-tts-flash" };
                    ProviderProfiles.Prepare(cfg, "千问", true); cfg.SpeechModel = "qwen-audio-3.1-tts-flash";
                    var options = new SpeechOptionsWindow(ow, cfg);
                    var sw = typeof(SpeechOptionsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(options) as Window;
                    sw.Left = 120; sw.Top = 120; sw.Show(); Pump(700);
                    foreach (var name in new[] { "style", "voice" }) {
                        var box = typeof(SpeechOptionsWindow).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(options) as System.Windows.Controls.ComboBox;
                        if (box == null) continue;
                        var presenter = FindPresenter(box);
                        double offset = -1;
                        if (presenter != null && presenter.ActualHeight > 0) {
                            double want = box.ActualHeight / 2;
                            double got = presenter.TranslatePoint(new Point(0, presenter.ActualHeight / 2), box).Y;
                            offset = Math.Round(Math.Abs(got - want), 1);
                        }
                        Console.WriteLine("SpeechOptions[" + theme + "]." + name + " h=" + box.ActualHeight
                            + " font=" + box.FontSize + " centerOffset=" + (offset < 0 ? "n/a" : offset.ToString()));
                    }
                    Shot(sw, System.IO.Path.Combine(directory, "speech-" + theme + ".png"), 0);
                    sw.Close(); ow.Close();
                }
            }
            // 之前每次都在干净背景上截图，所以这个缺陷一直没被发现。
            var back = new Window { Width = 900, Height = 760, WindowStyle = WindowStyle.None, Left = 60, Top = 60, Background = System.Windows.Media.Brushes.White, ShowInTaskbar = false };
            {
                var canvas = new System.Windows.Controls.Canvas { Background = System.Windows.Media.Brushes.White };
                var text = new System.Windows.Controls.TextBlock {
                    Text = "背景文字测试 BACKGROUND 背景文字测试 BACKGROUND\n"
                         + "BACKGROUND 背景文字测试 BACKGROUND 背景文字\n"
                         + "背 景 文 字 测 试 BACKGROUND 背 景 文 字 测 试\n"
                         + "BACKGROUND TEXT 背景文字测试 BACKGROUND TEXT",
                    FontSize = 24, Width = 900, TextWrapping = System.Windows.TextWrapping.Wrap,
                    Foreground = System.Windows.Media.Brushes.Black
                };
                canvas.Children.Add(text);
                back.Content = canvas;
                back.Show(); Pump(300);
                using (var card = new SettingsWindow(new Configuration { Theme = "glass" }, true)) {
                    var w = typeof(SettingsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(card) as Window;
                    w.Left = 100; w.Top = 100; w.Show(); Pump(900);
                    int ww = (int)Math.Ceiling(w.ActualWidth), hh = (int)Math.Ceiling(w.ActualHeight);
                    // 先把背后的窗口画进同一张图，再把设置窗口叠上去，还原真实的桌面透字场景。
                    var backShot = new RenderTargetBitmap(ww, hh, 96, 96, PixelFormats.Pbgra32);
                    backShot.Render(back);
                    var layShot = new RenderTargetBitmap(ww, hh, 96, 96, PixelFormats.Pbgra32);
                    layShot.Render(w);
                    var comp = new DrawingVisual();
                    using (var dc = comp.RenderOpen()) {
                        dc.DrawImage(backShot, new Rect(0, 0, ww, hh));
                        dc.DrawImage(layShot, new Rect(0, 0, ww, hh));
                    }
                    var final = new RenderTargetBitmap(ww, hh, 96, 96, PixelFormats.Pbgra32);
                    final.Render(comp);
                    // 判定标准：比较「只有设置窗口」与「设置窗口叠在有文字的背景上」两张同尺寸图。
                    // 玻璃允许轻微半透明，所以看平均色差，不看有没有差异像素。
                    long total = 0; int sampled = 0;
                    byte[] a = new byte[4], b = new byte[4];
                    for (int y = 180; y < 520; y += 3) {
                        for (int x = 60; x < 700; x += 3) {
                            var rect = new System.Windows.Int32Rect(x, y, 1, 1);
                            layShot.CopyPixels(rect, a, 4, 0);
                            final.CopyPixels(rect, b, 4, 0);
                            sampled++;
                            total += Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) + Math.Abs(a[2] - b[2]);
                        }
                    }
                    double mean = sampled == 0 ? 0 : (double)total / sampled;
                    Console.WriteLine("Glass readability: meanDelta=" + mean.ToString("F1")
                        + " => " + (mean > 20 ? "TEXT BLEEDS THROUGH" : "cards stay readable"));
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(final));
                    using (var fs = System.IO.File.Create(System.IO.Path.Combine(directory, "glass-over-text.png"))) enc.Save(fs);
                    w.Close();
                }
                back.Close();
            }
            // 皮肤插画必须铺满整个窗口：单独量一次，确认它没有被行裁切或拉伸。
            using (var probe = new SettingsWindow(new Configuration { Theme = "glass" }, true)) {
                var w = typeof(SettingsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(probe) as Window;
                w.Left = 40; w.Top = 40; w.Show(); Pump(600);
                // 液面与插画必须铺满窗口。只量插画是不够的：液面曾经被行容器卡成顶部一条，
// 下面的卡片失去底色，背后的文字就会透出来 —— 这个缺陷只有量液面才能发现。
                var mist = (System.Windows.Controls.Canvas)w.FindName("Mist");
                Console.WriteLine("Mist: actual=" + Math.Round(mist.ActualWidth) + "x" + Math.Round(mist.ActualHeight)
                    + " coversWindow=" + (mist.ActualHeight >= w.ActualHeight - 4 ? "yes" : "NO — 会透出背后文字"));
                var art = (System.Windows.Controls.Image)w.FindName("Artwork");
                var entry = w.FindName("LearningEntry") as System.Windows.Controls.Button;
                if (entry != null) Console.WriteLine("LearningEntry: h=" + entry.ActualHeight + " label=" + ((System.Windows.Controls.TextBlock)w.FindName("LearningLabel")).Text);
                Console.WriteLine("Artwork: window=" + w.ActualWidth + "x" + w.ActualHeight
                    + " actual=" + Math.Round(art.ActualWidth) + "x" + Math.Round(art.ActualHeight)
                    + " top=" + Math.Round(art.TranslatePoint(new Point(0, 0), w).Y)
                    + " stretch=" + art.Stretch);
                w.Close();
            }
            // 语言入口必须在底栏内、不被裁切，并且是按钮而不是占满一行的大卡片。
            using (var m = new SettingsWindow(new Configuration { Theme = "glass" }, true)) {
                var w = typeof(SettingsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(m) as Window;
                w.Show(); Pump(600);
                var card = w.FindName("LanguageCard") as System.Windows.Controls.Border;
                var entry = w.FindName("LearningEntry") as System.Windows.Controls.Button;
                EqualShot(card == null, "the oversized language card is gone");
                EqualShot(entry != null, "the language entry exists in the footer");
                if (entry != null) {
                    double bottom = entry.TranslatePoint(new Point(0, entry.ActualHeight), w).Y;
                    Console.WriteLine("LanguageEntry bottom=" + Math.Round(bottom) + " window=" + w.ActualHeight
                        + " => " + (bottom > w.ActualHeight ? "CLIPPED" : "fits inside the footer"));
                }
                w.Close();
            }
            // 控件一致性：高度、字号、文字是否垂直居中。用户反复看到不统一。
            using (var probe = new SettingsWindow(new Configuration { Theme = "glass" }, true)) {
                var w = typeof(SettingsWindow).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(probe) as Window;
                w.Left = 40; w.Top = 40; w.Show(); Pump(800);
                foreach (var name in new[] { "SkinPicker", "TranslationProvider", "SpeechProvider", "LearningLanguage" }) {
                    var box = w.FindName(name) as System.Windows.Controls.ComboBox;
                    if (box == null) continue;
                    // 选中项文字块的中心与控件中心之差，就是垂直偏心量。
                    var content = box.Template.FindName("", box) as FrameworkElement;
                    double offset = -1;
                    var presenter = FindPresenter(box);
                    if (presenter != null && presenter.ActualHeight > 0) {
                        double want = box.ActualHeight / 2;
                        double got = presenter.TranslatePoint(new Point(0, presenter.ActualHeight / 2), box).Y;
                        offset = Math.Round(Math.Abs(got - want), 1);
                    }
                    Console.WriteLine("Control[" + name + "] h=" + box.ActualHeight + " font=" + box.FontSize
                        + " centerOffset=" + (offset < 0 ? "n/a" : offset.ToString()));
                }
                w.Close();
            }
            // 查词模式：输入一个英文词，浮窗直接给音标、词性与释义。
            using (var panel = new Overlay(true)) {
                panel.Learnable = true;
                var list = new System.Collections.Generic.List<WordEntry>();
                list.Add(new WordEntry { Word = "record", Phonetic = "ˈrekɔ:d", PartOfSpeech = "n.", Meaning = "记录；唱片；最高纪录" });
                list.Add(new WordEntry { Word = "record", Phonetic = "riˈkɔ:d", PartOfSpeech = "v.", Meaning = "记录；录音；登记" });
                panel.ShowWordEntries("record", list);
                panel.SetLookup(true);
                panel.Follow(new System.Drawing.Rectangle(60, 500, 2, 20));
                Pump(500); Pump(400);
                var lw = typeof(Overlay).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(panel) as Window;
                if (lw != null) Shot(lw, System.IO.Path.Combine(directory, "lookup-record.png"), 0);
                panel.ShowWordEntries("zzzznotawordzzzz", new System.Collections.Generic.List<WordEntry>());
                Pump(500);
                if (lw != null) Shot(lw, System.IO.Path.Combine(directory, "lookup-miss.png"), 0);
                var footerField = typeof(Overlay).GetField("footer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var footer = footerField == null ? null : footerField.GetValue(panel) as System.Windows.Controls.Border;
                if (footer != null) {
                    var dock = footer.Child as System.Windows.Controls.DockPanel;
                    var names = new System.Collections.Generic.List<string>();
                    if (dock != null) {
                        foreach (var child in dock.Children) {
                            var button = child as System.Windows.Controls.Button;
                            if (button != null) names.Add(System.Windows.Automation.AutomationProperties.GetName(button));
                        }
                    }
                    Console.WriteLine("Footer order: " + String.Join(" | ", names.ToArray()));
                }
            }
            Console.WriteLine("Panel screenshots written to " + directory);
            return 0;
        }
        // 找到 ComboBox 模板里承载选中文字的那个 ContentPresenter。
        static FrameworkElement FindPresenter(System.Windows.Controls.ComboBox box) {
            var grid = System.Windows.Media.VisualTreeHelper.GetChild(box, 0) as System.Windows.Media.Visual;
            if (grid == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(grid); i++) {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(grid, i) as FrameworkElement;
                if (child is System.Windows.Controls.ContentPresenter) return child;
            }
            return null;
        }
        // 截图自检用的轻量断言：不抛异常，只把结论打到控制台，方便一眼看出问题。
        static void EqualShot(bool ok, string what) {
            Console.WriteLine((ok ? "  OK   " : "  FAIL ") + what);
        }
        static void Pump(int ms = 500) {            var frame = new System.Windows.Threading.DispatcherFrame();
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
