// 桌面生命周期、触发、过时结果取消与朗读；不保存输入历史。
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnglishCompanion {
    internal sealed class Companion : ApplicationContext {
        readonly Overlay overlay;
        readonly TrayIcon tray;
        readonly Hotkeys hotkeys;
        readonly ProbeClient probe = new ProbeClient();
        readonly CaptureSession session = new CaptureSession();
        readonly TypingSession typing = new TypingSession();
        readonly System.Collections.Generic.Dictionary<string,PairResult> cache = new System.Collections.Generic.Dictionary<string,PairResult>();
        long ownerWindow;
        bool hasContent;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 80 };
        readonly bool demo;
        Configuration config;
        Snapshot last;
        DateTime lastRead = DateTime.MinValue, altStart;
        bool busy, held, paused, editing, disposed;
        int request, generation, voiceGeneration;
        string owner = "", dismissed = "", source = "", translated = "", audioText = "";
        byte[] audio;
        CancellationTokenSource translationJob, speechJob;
        readonly VoiceProcess voice = new VoiceProcess();
        Form diagnostics;
        Label health;
        readonly EventWaitHandle settingsSignal;
        volatile SettingsWindow activeSettings;
        internal Companion(Configuration config, bool demo, bool settings) {
            this.config = config; this.demo = demo;
            settingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\EnglishCompanion-OpenSettings" + (demo ? "-demo" : ""));
            if (demo) {
                diagnostics = new Form { Text = "语伴 · 演示状态", Size = new Size(530, 150), StartPosition = FormStartPosition.Manual, Location = new Point(30, 40) };
                health = new Label { Dock = DockStyle.Fill, Padding = new Padding(10), Text = "等待输入框…", Font = new Font("Microsoft YaHei UI", 10) };
                diagnostics.Controls.Add(health); diagnostics.Show();
            }
            overlay = new Overlay(demo); overlay.ApplySkin(config.Theme);overlay.ShowOriginal=config.ShowOriginal; var handle = overlay.Handle;
            if(demo)overlay.InspectableDemo();
            overlay.Settings.Click += delegate { OpenSettings(); };
            overlay.Retry.Click += async delegate { if (source.Length > 0) await Translate(source); };
            overlay.Speak.Click += async delegate { await Speak(); };
            overlay.Manual.Click += delegate {Manual();};
            overlay.Dismissed += delegate { dismissed=owner;StopVoice(); };
            overlay.SpeakWord = async delegate(string word) { if(!demo) await Speak(word); };
            overlay.ExplainWord = delegate(string word,string sentence,CancellationToken token) { return demo?Task.FromResult("离线演示未调用模型。正式模式下会解释本句中的含义与搭配。"):Services.Explain(config,word,sentence,token); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("设置", null, delegate { OpenSettings(); });
            menu.Items.Add("粘贴一句话翻译", null, delegate { Manual(); });
            var autoTyping=new ToolStripMenuItem("自动翻译打字") {CheckOnClick=true,Checked=config.AutoTyping};
            autoTyping.CheckedChanged+=delegate {config.AutoTyping=autoTyping.Checked;config.InputVersion=13;typing.Reset();CancelTranslation();try{config.Save();}catch{MessageBox.Show("本次开关已生效，但未能保存，下次启动可能恢复。","语伴");}};
            menu.Items.Add(autoTyping);
            var pause = new ToolStripMenuItem("暂停伴读") { CheckOnClick = true };
            pause.CheckedChanged += delegate { paused = pause.Checked; Reset(); overlay.Hide(); if (paused) probe.Dispose(); };
            menu.Items.Add(pause);
            menu.Items.Add("退出", null, delegate { ExitThread(); });
            tray = new TrayIcon(menu, delegate { overlay.BeginInvoke(new Action(OpenSettings)); }, demo);
            hotkeys = new Hotkeys(); hotkeys.Pressed += delegate(int id) { if (id == 101) Manual(); else request = id; };
            timer.Tick += async delegate { await Tick(); }; timer.Start();
            if (settings) { overlay.BeginInvoke(new Action(OpenSettings)); }
        }
        async Task Tick() {
            if (!disposed && settingsSignal.WaitOne(0)) { OpenSettings(); return; }
            if (paused || editing || disposed) return;
            bool alt = Native.Down(0x12), combo = Native.Down(0x11) || Native.Down(0x09) || Native.Down(0x5B) || Native.Down(0x5C);
            if (alt && !held) {
                altStart = DateTime.UtcNow;
                if (!combo && last != null && last.Window == Native.GetForegroundWindow().ToInt64() && (DateTime.UtcNow - lastRead).TotalMilliseconds < 900) {
                    session.Start(last, DateTime.UtcNow,true); dismissed = "";
                    if(session.Active) {
                        CancelTranslation();StopVoice();hasContent=true;source=translated="";overlay.Learnable=false;
                        overlay.ClearPairs();overlay.Original.Text="";overlay.Translation.Text="正在听写…";overlay.Speak.Enabled=overlay.Retry.Enabled=false;overlay.Follow(last.Rect);
                    }
                }
            }
            if (alt && combo) session.Reset();
            if (alt && !combo) session.Extend(DateTime.UtcNow);
            if (!alt && held && (DateTime.UtcNow - altStart).TotalMilliseconds < 220) session.Reset();
            held = alt;
            if (busy || (DateTime.UtcNow - lastRead).TotalMilliseconds < (last!=null&&last.Editable?80:200)) return;
            uint foregroundPid;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out foregroundPid);
            if (foregroundPid == (uint)Process.GetCurrentProcess().Id) return;
            int action = request; request = 0; busy = true;
            Snapshot s;
            try { s = await probe.ReadAsync(action == 102); }
            finally { busy = false; }
            if (disposed || paused || editing) return;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out foregroundPid);
            if(foregroundPid==(uint)Process.GetCurrentProcess().Id)return;
            if (health != null) health.Text = "输入框读取：" + (s.Editable ? "可用" : "不可用") + "\n" + s.Reason + "\n进程：" + s.Process + "；浮层：" + overlay.Visible + "；原文/译文长度：" + source.Length + "/" + translated.Length;
            lastRead = DateTime.UtcNow;
            if (s.Window == 0) {s.Window=Native.GetForegroundWindow().ToInt64();s.Id="unavailable:"+s.Window;}
            if(s.Id.Length==0)s.Id="unavailable:"+s.Window;
            if (s.Window != Native.GetForegroundWindow().ToInt64()) { Reset(); overlay.Hide(); last = null; return; }
            if (s.Protected) { Reset(); overlay.Hide(); last = null; return; }
            if(s.Window==ownerWindow && hasContent && !s.Editable && !held && action==0) {if(dismissed!=owner)overlay.Follow(last!=null?last.Rect:s.Rect);return;}
            if (s.Id != owner || s.Window!=ownerWindow) {
                Reset(); overlay.Hide();owner = s.Id;ownerWindow=s.Window;dismissed = "";
                overlay.ClearPairs();overlay.Original.Text = "";overlay.Translation.Text="";
            }
            overlay.Fallback=!s.Editable;
            if (!s.Editable && (action != 0 || (held && !combo))) { hasContent=true;dismissed="";overlay.Learnable=false;overlay.ClearPairs();overlay.Translation.Text=s.Reason; }
            if (action == 103) { session.Start(s, DateTime.UtcNow); dismissed = ""; overlay.ClearPairs();overlay.Translation.Text = "本轮捕获已开始，请输入或粘贴一句中文"; }
            if (action == 102) {
                if (s.Selection.Length > 0) { dismissed = ""; overlay.Follow(s.Rect); var selectedTask=Translate(s.Selection); }
                else { hasContent=true;overlay.Fallback=true;overlay.ClearPairs();overlay.Translation.Text = "未读到选中文字，可点击下方“粘贴翻译”。"; }
            }
            bool capturing=held||session.Active;
            int revision=typing.Revision;
            string typed=typing.Observe(s,!config.AutoTyping||capturing,DateTime.UtcNow);
            if(typing.Revision!=revision && s.Editable) {CancelTranslation();StopVoice();dismissed="";}
            string sentence = session.Observe(s, held, DateTime.UtcNow);
            last = s;
            if (sentence != null || typed != null) { dismissed = "";hasContent=true;overlay.Follow(s.Rect);var translateTask=Translate(sentence??typed); }
            if(hasContent&&dismissed!=owner)overlay.Follow(s.Rect);else overlay.Hide();
        }
        void CancelTranslation() { generation++;if(translationJob!=null){translationJob.Cancel();translationJob.Dispose();translationJob=null;} }
        void Reset() {
            session.Reset();typing.Reset();CancelTranslation();hasContent=false;overlay.Learnable=false;
            StopVoice(); source = translated = audioText = ""; audio = null;
            overlay.Speak.Enabled = overlay.Retry.Enabled = false;
        }
        async Task Translate(string text) {
            if (text.Length == 0) return;
            hasContent=true;overlay.Learnable=false;
            generation++; int current = generation;
            if (translationJob != null) { translationJob.Cancel(); translationJob.Dispose(); }
            translationJob = new CancellationTokenSource(); var token = translationJob.Token;
            StopVoice(); source = text; translated = ""; audio = null; audioText = "";
            overlay.ClearPairs();overlay.Original.Text = text; overlay.Translation.Text = "正在翻译整句…";
            overlay.Speak.Enabled = false; overlay.Retry.Enabled = true;
            try {
                PairResult result;
                if (demo) {
                    await Task.Delay(250, token);
                    result = Demo(text);
                } else if(!cache.TryGetValue(text,out result)) { result=await Services.TranslatePaired(config,text,token);if(current==generation){if(cache.Count>=32)cache.Clear();cache[text]=result;} }
                if (current != generation || disposed) return;
                // 逐句对照与整段排版用同一份结果；整段按钮与朗读只取完整译文。
                translated = result.Target; overlay.Translation.Text = translated;
                overlay.ShowPairs(result);
                overlay.Learnable=true;overlay.Speak.Enabled = !demo;
            } catch (OperationCanceledException) { }
            catch (Exception e) { if (current == generation && !disposed) { overlay.ClearPairs(); overlay.Translation.Text = e is InvalidOperationException ? e.Message : "处理失败，请检查设置后重试"; } }
        }
        // 离线演示不联网，用固定对照展示排版与交互。
        static PairResult Demo(string text) {
            var groups = Sentences.Groups(text);
            var result = new PairResult { Source = text, Direction = "English" };
            var samples = new[]{"I'd like to go to the movies tomorrow.","I was planning to finish it today, but something came up.","Okay, let me try again now.","That sounds like a good idea to me.","Let me take a look at it for a moment."};
            var parts = new StringBuilder();
            for (int i = 0; i < groups.Count; i++) {
                string target = groups.Count == 1 && text == "我明天想去看电影。" ? samples[0]
                    : groups.Count == 1 && text == "我原本打算今天完成，但临时有别的事情。" ? samples[1] : samples[2];
                groups[i].Target = target; if (i > 0) parts.Append(' '); parts.Append(target);
                result.Pairs.Add(groups[i]);
            }
            result.Target = parts.Length > 0 ? parts.ToString() : "[离线演示] 已捕获本轮输入；真实译文需要填写 API Key。";
            return result;
        }
        async Task Speak(string selectedWord=null) {
            if (speechJob != null) { StopVoice(); return; }
            if (translated.Length == 0 && selectedWord==null) return;
            speechJob = new CancellationTokenSource(); var token = speechJob.Token; int current = ++voiceGeneration;
            string text = selectedWord??translated; overlay.SetPlayback(PlaybackState.Preparing,selectedWord!=null);
            try {
                if (!config.LocalVoice && (audio == null || audioText != text)) { var bytes = await Services.Speak(config, text, token); if (current != voiceGeneration) return; WaveAudio.Validate(bytes); audio = bytes; audioText = text; }
                if (current != voiceGeneration || disposed) return;
                overlay.SetPlayback(PlaybackState.Playing,selectedWord!=null);
                await voice.Play(new VoiceRequest { Local=config.LocalVoice, Text=text, Language=config.Language, Audio=config.LocalVoice?null:Convert.ToBase64String(audio) },token);
            } catch (OperationCanceledException) { }
            catch (Exception e) { if (current == voiceGeneration && !disposed) { StopVoice(); overlay.Message(e is InvalidOperationException ? e.Message : "朗读暂不可用，请重试"); } }
            finally { if(current==voiceGeneration && !disposed) StopVoice(); }
        }
        void StopVoice() {
            voiceGeneration++;
            if (speechJob != null) { speechJob.Cancel(); speechJob.Dispose(); speechJob = null; }
            voice.Dispose();
            overlay.SetPlayback(PlaybackState.Idle);
        }
        async void OpenSettings() {
            if (activeSettings != null) { activeSettings.BringForward(); return; }
            if (editing || disposed) return;
            editing = true; Reset(); overlay.Hide(); probe.Dispose();
            try {
                var done=new TaskCompletionSource<Configuration>(); var original=config;
                var thread=new Thread(delegate() {
                    try { using(var window=new SettingsWindow(original)) { activeSettings=window; window.ShowDialog(); done.TrySetResult(window.Result); } }
                    catch(Exception e) { done.TrySetException(e); }
                    finally { activeSettings=null; System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
                });
                thread.IsBackground=true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
                var result=await done.Task; if(result!=null) { config=result;cache.Clear();overlay.ApplySkin(config.Theme);overlay.ShowOriginal=config.ShowOriginal; }
            }
            catch { MessageBox.Show("无法读取或保存配置。原配置未被覆盖。", "语伴"); }
            finally { activeSettings = null; editing = false; last = null; owner = ""; }
        }
        void Manual() {
            if (editing || disposed) return;
            editing = true; session.Reset(); StopVoice();
            using (var window = new Form { Text = "语伴 · 粘贴翻译", Size = new Size(560, 260), StartPosition = FormStartPosition.CenterScreen, Font = new Font("Microsoft YaHei UI", 10) }) {
                var input = new TextBox { Multiline = true, Dock = DockStyle.Fill, MaxLength = 1800, ScrollBars = ScrollBars.Vertical, AccessibleName = "要翻译的原文" };
                var send = new Button { Text = "翻译这段文字", Dock = DockStyle.Bottom, Height = 42 };
                window.Controls.Add(input); window.Controls.Add(send);
                string value = null; send.Click += delegate { if (input.Text.Trim().Length > 0) { value = input.Text.Trim(); window.Close(); } };
                window.ShowDialog();
                editing = false;
                if (value != null) { dismissed = ""; overlay.Follow(last != null ? last.Rect : new Rectangle(Cursor.Position, new Size(1, 20))); var task = Translate(value); }
            }
        }
        protected override void ExitThreadCore() {
            disposed = true; timer.Stop(); timer.Dispose(); Reset(); probe.Dispose(); hotkeys.Dispose(); settingsSignal.Dispose(); tray.Dispose(); overlay.Dispose(); if (diagnostics != null) diagnostics.Dispose(); base.ExitThreadCore();
        }
    }
}
