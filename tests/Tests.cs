using System;
using System.Drawing;
using System.Collections.Generic;

namespace EnglishCompanion {
    internal static partial class Tests {
        static int count;
        static void Equal<T>(T expected, T actual, string label) { count++; if (!Object.Equals(expected, actual)) throw new Exception(label + ": expected=" + expected + ", actual=" + actual); }
        static Snapshot S(string text, string id) { return new Snapshot { Id = id, Text = text, Editable = true }; }
        static T Field<T>(object target,string name) {return (T)target.GetType().GetField(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(target);}
        static void PumpLayout(int milliseconds=350) {
            var frame=new System.Windows.Threading.DispatcherFrame();
            var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milliseconds)};
            timer.Tick+=delegate {timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        static PairResult SamplePairs() {
            var result=new PairResult {Source="今天我想早点出门。\n因为晚上可能会下雨。",Target="I want to leave early today. It might rain this evening.",Direction="English"};
            result.Pairs.Add(new SentencePair {Id="s1",Source="今天我想早点出门。",Target="I want to leave early today."});
            result.Pairs.Add(new SentencePair {Id="s2",Source="因为晚上可能会下雨。",Target="It might rain this evening."});
            return result;
        }
        static void PanelLayoutChecks() {
            using(var panel=new Overlay(true)) {
                panel.Original.Text="测试原文";panel.Translation.Text="This is a short sentence.";panel.Learnable=true;
                panel.Follow(new Rectangle(600,600,2,20));PumpLayout();
                var window=Field<System.Windows.Window>(panel,"window");var fold=Field<OverlayButton>(panel,"fold");
                // 富文本首次布局后再取基准；比较目标高度，避免动画中间帧干扰。
                PumpLayout();double compact=window.Height;
                Equal(true,Field<bool>(panel,"foldPointsUp"),"above-input compact arrow points upward to expand");
                var close=Field<OverlayButton>(panel,"close");
                double foldX=fold.Control.TranslatePoint(new System.Windows.Point(fold.Control.ActualWidth/2,0),window).X;
                double closeX=close.Control.TranslatePoint(new System.Windows.Point(close.Control.ActualWidth/2,0),window).X;
                Equal(true,Math.Abs(foldX-closeX)<.6,"fold and close share one vertical centerline");
                fold.Control.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));PumpLayout();
                Equal(false,Field<bool>(panel,"foldPointsUp"),"above-input expanded arrow points downward to collapse");
                Equal(true,window.ActualHeight>=compact+8&&window.ActualHeight<160,"short expansion adds modest spacing instead of empty 240px panel");
                Equal(true,Field<bool>(panel,"expanded"),"fold enables expanded word lookup mode");
                fold.Control.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));PumpLayout();
                double restored=window.ActualHeight;
                Equal(true,Math.Abs(restored-compact)<2,"fold button restores compact height");
                panel.Translation.Text="We can read the complete sentence, listen carefully, and learn how each word is used in its context.";PumpLayout();compact=window.ActualHeight;
                fold.Control.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));PumpLayout();
                Equal(true,window.ActualHeight>compact+8&&window.ActualHeight<240,"medium expansion fits content without forced blank area");
                fold.Control.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));PumpLayout();
                panel.Translation.Text=String.Join(" ",System.Linq.Enumerable.Repeat("This scrollable sentence is long enough to require a visible scrollbar.",30));
                fold.Control.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));PumpLayout();
                var scroll=Field<System.Windows.Controls.ScrollViewer>(panel,"scroll");
                Equal(true,scroll.ScrollableHeight>100,"long translation overflows expanded viewport");
                var bar=(System.Windows.Controls.Primitives.ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar",scroll);
                Equal(true,bar.IsVisible,"expanded long translation shows scrollbar");
                var track=(System.Windows.Controls.Primitives.Track)bar.Template.FindName("PART_Track",bar);
                var thumbMark=(System.Windows.Controls.Border)track.Thumb.Template.FindName("SlimThumb",track.Thumb);
                Equal(3.5,thumbMark.Width,"scroll thumb visible width is halved");
                Equal(true,track.Thumb.ActualWidth>=7,"slimmer thumb retains drag hit area");
                Equal(true,bar.TranslatePoint(new System.Windows.Point(0,0),scroll).X<12,"scrollbar stays at left edge");
                scroll.ScrollToEnd();PumpLayout();
                Equal(true,scroll.VerticalOffset>100&&Math.Abs(scroll.VerticalOffset-scroll.ScrollableHeight)<2,"expanded viewport reaches end of long translation");
                panel.SetExpanded(false);PumpLayout();double bottom=window.Top+window.Height;
                if(System.Windows.SystemParameters.ClientAreaAnimation) {
                    // Observe real rendered frames instead of assuming a timer fires at exactly 70 ms.
                    bool sampled=false,continuous=false;
                    EventHandler sample=delegate {
                        double height=window.Height;
                        if(sampled||height<=141||height>=359)return;
                        sampled=true;double top=window.Top;panel.SetExpanded(false);
                        continuous=Math.Abs(window.Height-height)<2&&Math.Abs(window.Top-top)<2;
                    };
                    System.Windows.Media.CompositionTarget.Rendering+=sample;
                    try {panel.SetExpanded(true);PumpLayout(700);}
                    finally {System.Windows.Media.CompositionTarget.Rendering-=sample;}
                    Equal(true,sampled,"expansion presents intermediate rendered geometry");
                    Equal(true,continuous,"reversing motion preserves current geometry");
                    Equal(true,Math.Abs(window.Top+window.Height-bottom)<2,"fold preserves anchored bottom edge");
                }
                panel.SetExpanded(true);PumpLayout();panel.Translation.Text="Done.";PumpLayout();
                Equal(true,window.Height<160&&scroll.ScrollableHeight<2,"long to short removes stale height and scroll extent");
                panel.ShowOriginal=false;PumpLayout();
                Equal(true,panel.Original.Control.Visibility==System.Windows.Visibility.Collapsed,"translation-only removes original layout space");
                panel.Hide();panel.SetExpanded(false);panel.Follow(new Rectangle(600,10,2,20));PumpLayout();
                Equal(false,Field<bool>(panel,"foldPointsUp"),"below-input compact arrow points downward to expand");
                panel.SetExpanded(true);PumpLayout();Equal(true,Field<bool>(panel,"foldPointsUp"),"below-input expanded arrow points upward to collapse");
                // Render only native panel controls: corner stroke must survive opaque footer backgrounds.
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render((System.Windows.Media.Visual)window.Content);
                byte[] pixel=new byte[4];
                bitmap.CopyPixels(new System.Windows.Int32Rect(6,(int)window.ActualHeight-5,1,1),pixel,4,0);
                Equal(true,pixel[3]>100&&pixel[0]<245,"bottom-left rounded outline remains visible above footer");
                bitmap.CopyPixels(new System.Windows.Int32Rect((int)window.ActualWidth-7,(int)window.ActualHeight-5,1,1),pixel,4,0);
                Equal(true,pixel[3]>100&&pixel[0]<245,"bottom-right rounded outline remains visible above footer");
            }
        }
        // 按句对照的浮窗行为：逐句成组、跨组选择、整段复制、查词上下文、收起展开共用。
        static void PairPanelChecks() {
            using(var panel=new Overlay(true)) {
                var pairs=SamplePairs();
                panel.Translation.Text=pairs.Target;panel.ShowPairs(pairs);panel.Learnable=true;
                panel.Follow(new Rectangle(600,600,2,20));PumpLayout();
                var editor=Field<System.Windows.Controls.RichTextBox>(panel,"editor");
                var document=editor.Document;
                Equal(4,document.Blocks.Count,"each group renders its own two lines");
                // BlockCollection 在本绑定下没有索引器：按文档顺序枚举。
                Func<int,System.Windows.Documents.Paragraph> block=delegate(int wanted){
                    var all=new System.Collections.Generic.List<System.Windows.Documents.Block>();foreach(var b in document.Blocks)all.Add(b);
                    return (System.Windows.Documents.Paragraph)all[wanted];
                };
                Func<System.Windows.Documents.Paragraph,string> line=delegate(System.Windows.Documents.Paragraph p){return new System.Windows.Documents.TextRange(p.ContentStart,p.ContentEnd).Text;};
                var first=block(0);var second=block(1);var third=block(2);var fourth=block(3);
                Equal("今天我想早点出门。",line(first),"group one source line");
                Equal("I want to leave early today.",line(second),"group one target line");
                Equal("因为晚上可能会下雨。",line(third),"group two source line");
                Equal("It might rain this evening.",line(fourth),"group two target line");
                // 组内距离小于组间距离，只靠段间距区分，不加分割线或卡片。
                double inside=first.Margin.Bottom, between=second.Margin.Bottom;
                Equal(true,inside<between,"distance inside a group is smaller than between groups");
                // 跨组选择仍然连续可用。
                int firstLength=line(first).Length+line(second).Length+line(third).Length+line(fourth).Length;
                panel.SelectRange(0,firstLength);PumpLayout();
                Equal(true,panel.SelectedLength()>0,"selection can span across groups");
                panel.SelectRange(0,firstLength);
                Equal(true,panel.SelectedLength()>0,"repeated selection stays usable");
                editor.SelectAll();PumpLayout();
                Equal(true,panel.SelectedLength()>0,"select all covers every group");
                // 整段按钮只拿完整译文，不含原文与排版标记。
                Equal("I want to leave early today. It might rain this evening.",pairs.Target,"whole-translation copy contains only the translation");
                Equal(false,pairs.Target.Contains("今天我想早点出门。"),"whole-translation copy never includes the source");
                // 仅译文模式去掉原文行，但同一份结果仍可用于朗读与复制。
                panel.ShowOriginal=false;PumpLayout();
                Equal(2,editor.Document.Blocks.Count,"translation-only shows one line per group");
                Equal("I want to leave early today.",line(block(0)),"translation-only keeps the target line");
                panel.ShowOriginal=true;PumpLayout();
                Equal(4,editor.Document.Blocks.Count,"bilingual restores the source line");
                // 查词上下文取所在句组，不串到相邻组。
                var ranges=panel.Ranges;
                Equal(4,ranges.Count,"each rendered line is tracked for hover lookup");
                Equal(0,ranges[0].Group,"first line belongs to group one");
                Equal(0,ranges[1].Group,"group one target shares the group");
                Equal(1,ranges[2].Group,"third line starts group two");
                Equal(true,ranges[1].Target&&!ranges[0].Target,"only the target line offers word lookup");
                var groupText=(string)typeof(Overlay).GetMethod("GroupText",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(panel,new object[]{1});
                Equal(true,groupText.Contains("因为晚上可能会下雨。")&&groupText.Contains("It might rain this evening."),"word lookup uses its own group context");
                // 展开与收起使用同一份结果，不要求额外点击生成对照。
                var before=editor.Document.Blocks.Count;panel.SetExpanded(true);PumpLayout();
                Equal(before,editor.Document.Blocks.Count,"expanding reuses the same paired result");
                var scroll=Field<System.Windows.Controls.ScrollViewer>(panel,"scroll");
                Equal(true,scroll.ExtentHeight>0,"paired content still measures inside the viewport");
                panel.SetExpanded(false);PumpLayout();
                Equal(before,editor.Document.Blocks.Count,"collapsing keeps the same paired result");
                // 长段可滚动，且不出现横向滚动条。
                var many=new PairResult {Source="长",Target=""};
                for(int i=0;i<40;i++)many.Pairs.Add(new SentencePair {Id="s"+i,Source="这是第"+i+"句中文，用来撑开滚动区。",Target="This is English sentence number "+i+", long enough to require scrolling in the panel."});
                var joined=new System.Text.StringBuilder();
                for(int i=0;i<40;i++){if(i>0)joined.Append(' ');joined.Append(many.Pairs[i].Target);}
                many.Target=joined.ToString();
                panel.Translation.Text=many.Target;panel.ShowPairs(many);panel.SetExpanded(true);PumpLayout();
                Equal(80,editor.Document.Blocks.Count,"long text keeps every group");
                Equal(true,scroll.ScrollableHeight>100,"long paired text overflows the expanded viewport");
                Equal(System.Windows.Controls.ScrollBarVisibility.Disabled,scroll.HorizontalScrollBarVisibility,"long paired text never scrolls horizontally");
                scroll.ScrollToEnd();PumpLayout();
                Equal(true,scroll.VerticalOffset>100,"paired viewport reaches the end of the text");
                panel.ApplySkin("glass");panel.ApplySkin("ocean");panel.ApplySkin("baby");PumpLayout();
                Equal(80,editor.Document.Blocks.Count,"switching skins never rebuilds or drops groups");
                panel.SetExpanded(false);PumpLayout();
            }
        }
        static void LiquidChecks() {
            var canvas=new System.Windows.Controls.Canvas();
            var window=new System.Windows.Window {Title="语伴 · 动效检查",Width=820,Height=590,Content=canvas,ShowInTaskbar=false};
            var material=new GlassMist(window,canvas);material.SetEnabled(true);window.Show();PumpLayout();
            Console.WriteLine("Liquid GPU="+material.Hardware+"; frames="+material.FrameCount);
            Equal(true,material.Hardware,"test machine supports native liquid GPU shader");
            if(System.Windows.SystemParameters.ClientAreaAnimation)Equal(true,material.FrameCount>0,"visible liquid renders frames");
            window.WindowState=System.Windows.WindowState.Minimized;PumpLayout();int paused=material.FrameCount;PumpLayout();
            Equal(false,material.Running,"minimized liquid unsubscribes frame loop");Equal(paused,material.FrameCount,"minimized liquid does not update frames");
            window.WindowState=System.Windows.WindowState.Normal;PumpLayout();material.SetEnabled(false);int disabled=material.FrameCount;PumpLayout();
            Equal(disabled,material.FrameCount,"other skins stop liquid frames");
            material.SetEnabled(true);PumpLayout();window.Close();int closed=material.FrameCount;PumpLayout();
            Equal(false,material.Running,"closed settings unsubscribes frame loop");Equal(closed,material.FrameCount,"closed settings never resumes rendering");
        }
        static double Luminance(string hex) {
            var c=(System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            Func<byte,double> linear=delegate(byte b){double v=b/255.0;return v<=0.04045?v/12.92:Math.Pow((v+0.055)/1.055,2.4);};
            return .2126*linear(c.R)+.7152*linear(c.G)+.0722*linear(c.B);
        }
        static double Contrast(string a,string b) {double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
        static string Composite(string foreground,string background) {
            var f=(System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(foreground);var b=(System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(background);double alpha=f.A/255.0;
            return String.Format("#{0:X2}{1:X2}{2:X2}",(int)Math.Round(f.R*alpha+b.R*(1-alpha)),(int)Math.Round(f.G*alpha+b.G*(1-alpha)),(int)Math.Round(f.B*alpha+b.B*(1-alpha)));
        }
        static void SkinChecks() {
            using(var settings=new SettingsWindow(new Configuration(),true)) {
                var w=Field<System.Windows.Window>(settings,"window");w.Show();PumpLayout();
                var picker=(System.Windows.Controls.ComboBox)w.FindName("SkinPicker");
                var entry=Field<KeyEntry>(settings,"translationKey");entry.Value="local-test-value";
                for(int i=0;i<3;i++) {
                    picker.SelectedIndex=i;PumpLayout();var s=Skin.Get(i==1?"ocean":i==2?"baby":"glass");
                    foreach(string fg in new[]{s.Ink,s.Muted,s.Pending})Equal(true,Contrast(fg,s.Panel)>=4.5,s.Name+" panel text contrast");
                    Equal(true,Contrast(s.ButtonInk,s.Accent)>=4.5,s.Name+" confirm contrast");
                    Equal(true,Contrast(s.Muted,s.Id=="glass"?s.Panel:s.Field)>=4.5,s.Name+" placeholder contrast");
                    Equal(true,Contrast(s.FieldEdge,s.Id=="glass"?s.Panel:s.Field)>=3,s.Name+" input boundary contrast");
                    Equal(true,Contrast(s.Focus,s.Id=="glass"?s.Panel:s.Field)>=3,s.Name+" focused input contrast");
                    var password=Field<System.Windows.Controls.PasswordBox>(entry,"password");
                    Equal(Skin.Brush(s.Ink).ToString(),password.Foreground.ToString(),s.Name+" actual password foreground");
                    Equal("local-test-value",entry.Value,s.Name+" theme change preserves typed key");
                    Equal(System.Windows.Visibility.Collapsed,((System.Windows.Controls.Image)w.FindName("BrandIcon")).Visibility,"settings never displays dimensional app icon");
                    Equal(i==0?System.Windows.Visibility.Visible:System.Windows.Visibility.Collapsed,((System.Windows.Controls.Border)w.FindName("BrandPlate")).Visibility,s.Name+" wordmark visibility");
                    var eye=Field<System.Windows.Controls.Primitives.ToggleButton>(entry,"eye");eye.IsChecked=true;
                    Equal(entry.Value,Field<System.Windows.Controls.TextBox>(entry,"visible").Text,s.Name+" eye reveals test value");
                    entry.HideSecret();Equal("",Field<System.Windows.Controls.TextBox>(entry,"visible").Text,s.Name+" hiding clears visible buffer");
                }
                Equal("小奶蛙",picker.Items[2],"correct frog skin name");
                var glass=Skin.Get("glass");                picker.SelectedIndex=0;PumpLayout();
                foreach(string cardName in new[]{"TranslationCard","SpeechCard"}) {
                    var card=(System.Windows.Controls.Border)w.FindName(cardName);
                    var material=(System.Windows.Media.GeometryDrawing)((System.Windows.Media.DrawingBrush)card.Background).Drawing;
                    Equal(true,material.Geometry.FillContains(new System.Windows.Point(40,25)),"outer card retains frosted material behind heading");
                    string prefix=cardName=="TranslationCard"?"Translation":"Speech";
                    foreach(string part in new[]{"Provider","KeyHost"}) {
                        var child=(System.Windows.FrameworkElement)w.FindName(prefix+part);
                        var center=child.TranslatePoint(new System.Windows.Point(child.ActualWidth/2,child.ActualHeight/2),card);
                        Equal(false,material.Geometry.FillContains(center),"inner field cuts through outer material to liquid");
                    }
                }
                Equal((byte)0,((System.Windows.Media.SolidColorBrush)Field<System.Windows.Controls.Border>(entry,"frame").Background).Color.A,"actual key field has no tinted fill");
                var wordmark=(System.Windows.Shapes.Path)w.FindName("BrandWordmark");
                Equal(true,wordmark.ActualWidth>=175&&wordmark.ActualHeight>=75&&!wordmark.Data.IsEmpty(),"large vector wordmark renders without installed font");
                // Conservative lower bound of the shader color and alpha over a black desktop.
                // Also check white: these bound the channelwise desktop compositions, not arbitrary desktop detail.
                foreach(string desktop in new[]{"#000000","#FFFFFF"}) {
                    string material=Composite("#B887B8E8",Composite("#40FFFFFF",desktop));
                    foreach(string ink in new[]{glass.Ink,glass.Muted,glass.Pending})Equal(true,Contrast(ink,material)>=4.5,"text readable through transparent fields over bounded desktop");
                    Equal(true,Contrast(glass.FieldEdge,material)>=3,"transparent input boundary contrast");
                }
            }
        }
        static void PetChecks() {
            using(var panel=new Overlay(true)) {
                panel.Translation.Text="A little company while you learn.";panel.Learnable=true;panel.ShowOriginal=false;panel.ApplySkin("ocean");panel.Follow(new Rectangle(700,600,2,20));PumpLayout();
                var pet=Field<PetAdornment>(panel,"pet");var window=Field<System.Windows.Window>(panel,"window");var editor=Field<System.Windows.Controls.RichTextBox>(panel,"editor");
                Equal(false,pet.IsHitTestVisible,"pet never intercepts text or button input");
                Equal(true,editor.TranslatePoint(new System.Windows.Point(0,0),window).X>pet.TranslatePoint(new System.Windows.Point(pet.ActualWidth,0),window).X,"pet has its own nonoverlapping rail");
                if(System.Windows.SystemParameters.ClientAreaAnimation){Equal(true,pet.FrameCount>0,"visible themed pet animates");PumpLayout(4450);Equal(true,pet.BlinkCount>0,"pet changes to blink pose");}
                panel.SelectRange(0,4);PumpLayout();int frames=pet.FrameCount;PumpLayout();Equal(false,pet.Running,"selected text pauses decoration");Equal(frames,pet.FrameCount,"selection does not advance animation");
                panel.SelectRange(0,0);panel.Hide();PumpLayout();frames=pet.FrameCount;PumpLayout();Equal(false,pet.Running,"hidden panel stops pet");Equal(frames,pet.FrameCount,"hidden panel has no pet updates");
                panel.ApplySkin("baby");panel.Follow(new Rectangle(700,600,2,20));PumpLayout();Equal(true,pet.Visibility==System.Windows.Visibility.Visible,"frog pet is visible");
                var frog=Field<System.Windows.Controls.Image>(pet,"open").Source;panel.ApplySkin("ocean");PumpLayout();Equal(false,Object.ReferenceEquals(frog,Field<System.Windows.Controls.Image>(pet,"open").Source),"skin switch changes character source");
                panel.ApplySkin("glass");PumpLayout();Equal(false,pet.Running,"official default has no pet loop");Equal(System.Windows.Visibility.Collapsed,pet.Visibility,"official default has no pet rail");
                panel.ApplySkin("baby");PumpLayout();panel.Dispose();frames=pet.FrameCount;PumpLayout();Equal(false,pet.Running,"disposed pet unsubscribes");Equal(frames,pet.FrameCount,"closed pet does not update");
            }
        }
        static byte[] SampleWave(int milliseconds) {
            int bytes=16000*2*milliseconds/1000;
            using(var stream=new System.IO.MemoryStream()) using(var w=new System.IO.BinaryWriter(stream)) {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36+bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(16000); w.Write(32000); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes); w.Write(new byte[bytes]); return stream.ToArray();
            }
        }
        static byte[] ActivityPixels(AudioActivity activity) {
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(18,18,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(activity);var pixels=new byte[18*18*4];bitmap.CopyPixels(pixels,18*4,0);return pixels;
        }
        static void PlaybackChecks() {
            using(var panel=new Overlay(true)) {
                panel.Translation.Text="Listen to this sentence.";panel.Follow(new Rectangle(650,600,2,20));PumpLayout();panel.Speak.Enabled=true;
                double width=panel.Speak.Control.ActualWidth;
                panel.SetPlayback(PlaybackState.Preparing);PumpLayout();var activity=Field<AudioActivity>(panel.Speak,"activity");
                Equal("准备中",panel.Speak.Text,"pending audio has distinct preparing state");
                panel.SetPlayback(PlaybackState.Playing);PumpLayout(90);var pixels=ActivityPixels(activity);int frames=activity.FrameCount;PumpLayout(170);
                if(System.Windows.SystemParameters.ClientAreaAnimation) {
                    Equal(true,activity.FrameCount>frames,"playing bars advance visible frames");
                    Equal(false,System.Linq.Enumerable.SequenceEqual(pixels,ActivityPixels(activity)),"playing bars actually change rendered pixels");
                }
                Equal(width,panel.Speak.Control.ActualWidth,"activity does not shift neighboring buttons");
                Equal("停止朗读",System.Windows.Automation.AutomationProperties.GetName(panel.Speak.Control),"active audio announces stop action");
                foreach(string theme in new[]{"glass","ocean","baby"}) {panel.ApplySkin(theme);PumpLayout(50);Equal(Skin.Brush(Skin.Get(theme).Ink).ToString(),activity.GetValue(AudioActivity.InkProperty).ToString(),"activity follows theme foreground");}
                panel.Hide();PumpLayout();frames=activity.FrameCount;PumpLayout();Equal(false,activity.Running,"hidden activity stops timer");Equal(frames,activity.FrameCount,"hidden activity consumes no animation ticks");
                panel.Follow(new Rectangle(650,600,2,20));PumpLayout();panel.SetPlayback(PlaybackState.Idle);PumpLayout();Equal(false,activity.Running,"idle stops animation");Equal("朗读",panel.Speak.Text,"idle restores original label");
                panel.SetPlayback(PlaybackState.Playing);PumpLayout();panel.Dispose();PumpLayout();Equal(false,activity.Running,"closing panel stops animation");
            }
            // Explicit test mode only. Use an existing fixed demo WAV; never synthesize or spend quota.
            var saved=Configuration.Load();saved.SpeechStyle="original";saved.EnglishVoice="";
            if(!System.IO.File.Exists(SpeechProfiles.SamplePath(saved)))throw new Exception("Cached comparison audio required; refusing a network test");
            using(var settings=new SettingsWindow(saved,true)) {
                var owner=Field<System.Windows.Window>(settings,"window");owner.Show();PumpLayout();
                var options=new SpeechOptionsWindow(owner,saved);var dialog=Field<System.Windows.Window>(options,"window");dialog.Show();PumpLayout();
                var method=typeof(SpeechOptionsWindow).GetMethod("Listen",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var activity=Field<AudioActivity>(options,"activity");
                var playing=(System.Threading.Tasks.Task)method.Invoke(options,null);PumpLayout(350);Equal(PlaybackState.Playing,activity.State,"real cached playback activates bars");
                Field<System.Windows.Controls.Button>(options,"listen").RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));PumpLayout(450);
                Equal(true,playing.IsCompleted,"stop cancels actual audio worker");Equal(PlaybackState.Idle,activity.State,"stop resets preview indicator");
                playing=(System.Threading.Tasks.Task)method.Invoke(options,null);var watch=System.Diagnostics.Stopwatch.StartNew();while(!playing.IsCompleted&&watch.ElapsedMilliseconds<20000)PumpLayout(100);
                Equal(true,playing.IsCompleted,"cached playback completes within duration bound");playing.GetAwaiter().GetResult();Equal(PlaybackState.Idle,activity.State,"natural completion resets preview indicator");
                Field<Configuration>(options,"config").SpeechModel="unsupported-test-model";
                playing=(System.Threading.Tasks.Task)method.Invoke(options,null);PumpLayout();Equal(true,playing.IsCompleted,"unsupported preview failure handled without network");Equal(PlaybackState.Idle,activity.State,"failure resets preview indicator");
                dialog.Close();Equal(false,activity.Running,"closed audition window stops animation");
            }
        }
        [STAThread] internal static int Main(string[] args) {
            try {
                if(Array.IndexOf(args,"--saved-settings-check")>=0){SavedSettingsUiChecks();Console.WriteLine("PASS: "+count+" saved settings assertions; credentials not printed; no network");return 0;}
                if(Array.IndexOf(args,"--qwen-ui-check")>=0){QwenSetupUiChecks();Console.WriteLine("PASS: "+count+" workspace dialog assertions; no network");return 0;}
                if(Array.IndexOf(args,"--speech-ui-check")>=0) {
                    using(var settings=new SettingsWindow(new Configuration(),true)) {
                        var owner=Field<System.Windows.Window>(settings,"window");owner.Show();PumpLayout();
                        var source=new Configuration {SpeechModel="qwen-audio-3.1-tts-flash"};
                        var options=new SpeechOptionsWindow(owner,source);
                        owner.Dispatcher.BeginInvoke(new Action(delegate {
                            Field<System.Windows.Controls.ComboBox>(options,"style").SelectedIndex=2;
                            Field<System.Windows.Controls.ComboBox>(options,"voice").SelectedIndex=3;
                            Field<System.Windows.Controls.Button>(options,"apply").RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        }));
                        options.Show();Equal("clear",source.SpeechStyle,"adopt transfers selected reading style into draft");Equal("Emily_v3.1",source.EnglishVoice,"adopt transfers selected English voice into draft");
                        var cancelled=new SpeechOptionsWindow(owner,source);
                        owner.Dispatcher.BeginInvoke(new Action(delegate {
                            Field<System.Windows.Controls.ComboBox>(cancelled,"style").SelectedIndex=1;
                            Field<System.Windows.Window>(cancelled,"window").Close();
                        }));
                        cancelled.Show();Equal("clear",source.SpeechStyle,"closing without adopt preserves draft");
                    }
                    Console.WriteLine("PASS: "+count+" speech dialog assertions; no network");return 0;
                }
                if(Array.IndexOf(args,"--voice-compare")>=0) {
                    System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
                    var original=Configuration.Load();
                    if(!SpeechProfiles.Supported(original))throw new InvalidOperationException("Comparison requires configured Audio 3.1 English");
                    foreach(var mode in new[]{"original","natural","clear","Betty_v3.1","Brian_v3.1","Emily_v3.1"}) {
                        var comparisonConfig=Probe.Json.Deserialize<Configuration>(Probe.Json.Serialize(original));
                        bool voice=mode.EndsWith("_v3.1");comparisonConfig.SpeechStyle=voice?"natural":mode;comparisonConfig.EnglishVoice=voice?mode:"";
                        bool cached=System.IO.File.Exists(SpeechProfiles.SamplePath(comparisonConfig));var timer=System.Diagnostics.Stopwatch.StartNew();
                        var data=SpeechProfiles.Preview(comparisonConfig,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                        Console.WriteLine(mode+"; cached="+cached+"; seconds="+WaveAudio.Validate(data).ToString("F2")+"; fetch_ms="+timer.ElapsedMilliseconds+"; file="+SpeechProfiles.SamplePath(comparisonConfig));
                    }
                    return 0;
                }
                if(Array.IndexOf(args,"--learning-smoke")>=0) {
                    System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
                    var savedConfig=Configuration.Load();var token=System.Threading.CancellationToken.None;
                    string translated=Services.Translate(savedConfig,"我明天想去看电影。",token).GetAwaiter().GetResult();
                    if(translated.Length<5)throw new Exception("No translation");Console.WriteLine("Translation returned, characters="+translated.Length);
                    string explanation=Services.Explain(savedConfig,"tomorrow",translated,token).GetAwaiter().GetResult();
                    if(explanation.Length<5)throw new Exception("No contextual explanation");Console.WriteLine("Contextual explanation returned, characters="+explanation.Length);
                    var audio=Services.Speak(savedConfig,"tomorrow",token).GetAwaiter().GetResult();Console.WriteLine("Word audio validated, seconds="+WaveAudio.Validate(audio));
                    using(var player=new VoiceProcess())player.Play(new VoiceRequest {Audio=Convert.ToBase64String(audio)},token).GetAwaiter().GetResult();
                    Console.WriteLine("Word playback completed");return 0;
                }
                if(Array.IndexOf(args,"--wave-smoke")>=0) {
                    var bytes=WaveAudio.Normalize(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"EnglishCompanion-voice-smoke.wav")));
                    Console.WriteLine("Normalized cloud WAV; seconds="+WaveAudio.Validate(bytes).ToString("F2"));
                    using(var voice=new VoiceProcess()) voice.Play(new VoiceRequest {Audio=Convert.ToBase64String(bytes)},System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    Console.WriteLine("Cloud playback completed"); return 0;
                }
                if (Array.IndexOf(args,"--voice-smoke")>=0) {
                    System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
                    var current=Configuration.Load();
                    var bytes=Services.Speak(current,"Small steps, big progress.",System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"EnglishCompanion-voice-smoke.wav"),bytes);
                    Console.WriteLine("Audio bytes="+bytes.Length+"; header="+BitConverter.ToString(bytes,0,Math.Min(80,bytes.Length)));
                    Console.WriteLine("Cloud WAV received; seconds="+WaveAudio.Validate(bytes).ToString("F2"));
                    using(var voice=new VoiceProcess()) voice.Play(new VoiceRequest {Audio=Convert.ToBase64String(bytes)},System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    Console.WriteLine("Cloud playback completed"); return 0;
                }
                if (Array.IndexOf(args, "--config-status") >= 0) {
                    var saved = Configuration.Load(); ProviderProfiles.SeedKeys(saved);
                    Console.WriteLine("ConfigPath=" + Configuration.FilePath);
                    Console.WriteLine("Model=" + saved.SpeechModel);
                    Console.WriteLine("HasSpeechKey=" + (ProviderProfiles.Saved(saved.SpeechKeys, "千问").Length > 0));
                    Console.WriteLine("HasTranslationKey=" + (saved.TranslationSecret.Length > 0));
                    using (var window = new SettingsWindow(saved, true)) Console.WriteLine("WindowHasSpeechKey=" + window.HasSpeechKey);
                    if (Array.IndexOf(args, "--resave") >= 0) { saved.Save(); Console.WriteLine("ConfigSaved=True"); }
                    return 0;
                }
                Equal("我明天想去看电影。", Changes.Added("", "我明天想去看电影。"), "empty input");
                Equal("新增句子。", Changes.Added("已有内容。", "已有内容。新增句子。"), "only append");
                Equal("明", Changes.Added("我今天去。", "我明天去。"), "minimal replacement fallback");
                Equal("明天", Changes.AddedAt("我今天去。", "我明天去。", 1, 3), "selection replacement preserves common suffix");
                Equal("哈哈", Changes.AddedAt("哈哈。", "哈哈哈哈。", 0, 0), "repeated insertion anchored at caret");
                Equal("", Changes.AddedAt("旧选区", "旧选区", 0, 3), "unchanged selection is never sent");
                Equal("喜欢", Changes.Added("我你。", "我喜欢你。"), "insert middle");
                Equal("", Changes.Added("已有内容。", "已有"), "deletion never sends history");
                Equal("", Changes.Added("不变", "不变"), "no change");
                Equal("", Changes.Added("", new string('中', 1801)), "max request bound");
                Equal("", Changes.Added(new string('a', 6001), "句子"), "max source bound");
                Equal("😃", Changes.Added("😀", "😃"), "surrogate boundaries");
                var now = new DateTime(2026, 10, 3);
                var session = new CaptureSession();
                session.Start(S("已有", "a"), now);
                Equal<string>(null, session.Observe(S("已有新句。", "a"), true, now.AddSeconds(1)), "held does not send");
                Equal<string>(null, session.Observe(S("已有新句子。", "a"), false, now.AddSeconds(2)), "wait for stable text");
                Equal("新句子。", session.Observe(S("已有新句子。", "a"), false, now.AddSeconds(3)), "stable whole addition");
                Equal<string>(null, session.Observe(S("已有新句子。", "a"), false, now.AddSeconds(4)), "no repeated send");
                session.Start(S("", "a"), now); session.Observe(S("甲", "a"), false, now);
                Equal<string>(null, session.Observe(S("甲", "b"), false, now.AddSeconds(2)), "focus switch discards");
                Equal(false, session.Active, "focus switch resets");
                session.Start(S("", "a"), now); session.Observe(S("甲", "a"), false, now);
                Equal<string>(null, session.Observe(new Snapshot { Id = "a", Protected = true }, false, now.AddSeconds(2)), "password cancels");
                session.Start(S("", "a"), now);
                Equal<string>(null, session.Observe(S("迟到", "a"), false, now.AddSeconds(21)), "timeout cancels");
                session.Start(S("", "a"), now); session.Extend(now.AddSeconds(40));
                session.Observe(S("长听写", "a"), false, now.AddSeconds(41));
                Equal("长听写", session.Observe(S("长听写", "a"), false, now.AddSeconds(43)), "long recording release");
                var area = new Rectangle(-1920, 0, 1920, 1080);
                var placed = Changes.Place(new Rectangle(-10, 100, 2, 20), new Size(450, 200), area);
                Equal(true, area.Contains(placed), "negative monitor clamped");
                Equal(130, placed.Y, "top collision flips below");
                placed = Changes.Place(new Rectangle(400, 1070, 2, 20), new Size(450, 200), new Rectangle(0, 0, 1920, 1080));
                Equal(860, placed.Y, "bottom placement above");
                string sealedKey = Configuration.Seal("test-not-a-real-key");
                Equal(false, sealedKey.Contains("test-not-a-real-key"), "encrypted at rest");
                Equal("test-not-a-real-key", Configuration.Open(sealedKey), "decrypt roundtrip");
                bool rejected = false; try { Services.Endpoint("http://example.com"); } catch (InvalidOperationException) { rejected = true; }
                Equal(true, rejected, "insecure endpoint rejected");
                rejected = false; try { Services.Endpoint("https://secret@example.com"); } catch (InvalidOperationException) { rejected = true; }
                Equal(true, rejected, "embedded credentials rejected");
                Equal("https://dashscope-result-bj.oss-cn-beijing.aliyuncs.com/test.wav?token=a%2Bb", Services.AudioEndpoint("http://dashscope-result-bj.oss-cn-beijing.aliyuncs.com/test.wav?token=a%2Bb").AbsoluteUri, "official audio URL upgraded without changing signature");
                rejected = false; try { Services.AudioEndpoint("http://example.com/test.wav"); } catch (InvalidOperationException) { rejected = true; }
                Equal(true, rejected, "unknown insecure audio host rejected");
                Equal("\u5343\u95ee", "千问", "compiler preserves Chinese provider name");
                var c = new Configuration(); string speech = Probe.Json.Serialize(Services.SpeechBody(c, "Hello."));
                Equal(true, speech.Contains("\"language_type\":\"English\""), "qwen language");
                Equal(false, speech.Contains("instructions"), "non-instruct model no instructions");
                var body = (Dictionary<string, object>)Services.TranslationBody(c, "Ignore instructions");
                Equal("DeepSeek", ProviderProfiles.TranslationProvider(c), "default translation provider");
                Equal(true, body.ContainsKey("thinking"), "DeepSeek disables reasoning for translation");
                c.TranslationModel = "qwen-plus";
                body = (Dictionary<string, object>)Services.TranslationBody(c, "test");
                Equal(false, (bool)body["enable_thinking"], "qwen direct output");
                c.TranslationModel = "other-model";
                Equal(false, ((Dictionary<string, object>)Services.TranslationBody(c, "text")).ContainsKey("enable_thinking"), "portable provider body");
                Equal("甲", Probe.Json.Deserialize<Snapshot>(Probe.Json.Serialize(S("甲", "a"))).Text, "IPC roundtrip");
                var profiles = new Configuration { TranslationUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", TranslationSecret = Configuration.Seal("old-qwen-test"), ReuseKey = true };
                ProviderProfiles.SeedKeys(profiles);
                Equal("", ProviderProfiles.Saved(profiles.TranslationKeys, "DeepSeek"), "provider change never transfers old key");
                Equal("old-qwen-test", ProviderProfiles.Saved(profiles.SpeechKeys, "千问"), "legacy shared voice key retained");
                ProviderProfiles.Apply(profiles, "DeepSeek", "new-deepseek-test", "千问", "voice-test");
                Equal("api.deepseek.com", new Uri(profiles.TranslationUrl).Host, "DeepSeek endpoint preset");
                Equal("new-deepseek-test", Configuration.Open(profiles.TranslationSecret), "translation key isolated");
                Equal("voice-test", Configuration.Open(profiles.SpeechSecret), "speech key isolated");
                var cloned = Probe.Json.Deserialize<Configuration>(Probe.Json.Serialize(profiles));
                Equal("voice-test", ProviderProfiles.Saved(cloned.SpeechKeys, "千问"), "settings clone preserves speech credential");
                Equal(false, profiles.ReuseKey, "no implicit cross-provider key reuse");
                Equal("old-qwen-test", ProviderProfiles.Saved(profiles.TranslationKeys, "千问"), "switching preserves per-provider credentials");
                var audioConfig = new Configuration { SpeechModel = "qwen-audio-3.1-tts-flash", Voice = "test-voice" };
                string audioBody = Probe.Json.Serialize(Services.SpeechBody(audioConfig, "Hello."));
                Equal(true, audioBody.Contains("\"format\":\"wav\""), "Audio TTS explicitly requests playable WAV");
                Equal(false, audioBody.Contains("language_type"), "Audio TTS does not use legacy schema");
                Equal(false,audioBody.Contains("instruction"),"original preserves baseline request");
                audioConfig.SpeechStyle="natural";audioConfig.EnglishVoice="Emily_v3.1";
                var express=(Dictionary<string,object>)Probe.Json.DeserializeObject(Probe.Json.Serialize(Services.SpeechBody(audioConfig,"Hello?")));
                var expressiveInput=(Dictionary<string,object>)express["input"];
                Equal("Hello?",expressiveInput["text"],"speech instructions never alter spoken text");
                Equal("Emily_v3.1",expressiveInput["voice"],"English voice overrides base voice");
                Equal(true,expressiveInput.ContainsKey("instruction")&&!expressiveInput.ContainsKey("instructions"),"Audio HTTP uses singular input instruction");
                string naturalPath=SpeechProfiles.SamplePath(audioConfig);audioConfig.SpeechStyle="clear";
                Equal(false,naturalPath==SpeechProfiles.SamplePath(audioConfig),"sample cache separates reading styles");
                audioConfig.SpeechStyle="natural";audioConfig.Language="Japanese";
                audioBody=Probe.Json.Serialize(Services.SpeechBody(audioConfig,"Hello."));
                Equal(false,audioBody.Contains("instruction")||audioBody.Contains("Emily_v3.1"),"English preferences never override Japanese reading");
                audioConfig.Language="English";audioConfig.SpeechStyle="original";audioConfig.EnglishVoice="";
                audioConfig.SpeechModel = "qwen-audio-3.1-tts-next";
                audioBody = Probe.Json.Serialize(Services.SpeechBody(audioConfig, "Hello."));
                Equal(true, audioBody.Contains("text_prompt"), "Audio Next prompt schema");
                Equal(false, audioBody.Contains("\"voice\""), "Audio Next omits unsupported voice");
                rejected = false; try { Services.ValidateSpeech(audioConfig, "sk-ws-test"); } catch (InvalidOperationException) { rejected = true; }
                Equal(true, rejected, "workspace key cannot call legacy endpoint");
                audioConfig.SpeechUrl = "https://example.cn-beijing.maas.aliyuncs.com/api/v1/services/audio/tts/SpeechSynthesizer";
                Services.ValidateSpeech(audioConfig, "sk-ws-test");
                audioConfig.SpeechModel = "";
                rejected = false; try { Services.ValidateSpeech(audioConfig, "test"); } catch (InvalidOperationException) { rejected = true; }
                Equal(true, rejected, "unconfirmed quota model is blocked before network");
                var start = new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CompanionProbe.exe"), "--echo-test") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
                using (var child = System.Diagnostics.Process.Start(start)) {
                    child.StandardInput.WriteLine("fixture"); child.StandardInput.Close();
                    var read = child.StandardOutput.ReadLineAsync();
                    if (!read.Wait(3000)) { child.Kill(); throw new Exception("IPC startup timed out"); }
                    Equal("IPC OK", Probe.Json.Deserialize<Snapshot>(read.Result).Text, "real helper process IPC");
                    Equal(true, child.WaitForExit(2000), "helper clean exit");
                    Equal(0, child.ExitCode, "helper success");
                }
                Equal("glass",Skin.Get("unknown").Id,"unknown theme fallback");
                Equal("ocean",Probe.Json.Deserialize<Configuration>(Probe.Json.Serialize(new Configuration {Theme="ocean"})).Theme,"theme roundtrip");
                Equal(0.1,WaveAudio.Validate(SampleWave(100)),"valid PCM duration");
                var streamed=SampleWave(100); Buffer.BlockCopy(BitConverter.GetBytes(0x7fffffbf),0,streamed,4,4); Buffer.BlockCopy(BitConverter.GetBytes(0x7fffff9b),0,streamed,40,4);
                Equal(0.1,WaveAudio.Validate(WaveAudio.Normalize(streamed)),"real Qwen streaming header normalized");
                Equal(0x7fffffbf,BitConverter.ToInt32(streamed,4),"original audio stays immutable");
                var broken=SampleWave(100); Array.Resize(ref broken,broken.Length-1);
                rejected=false;try {WaveAudio.Validate(broken);} catch(InvalidOperationException){rejected=true;}
                Equal(true,rejected,"truncated audio rejected");
                broken=SampleWave(100);broken[20]=3;
                rejected=false;try {WaveAudio.Validate(broken);} catch(InvalidOperationException){rejected=true;}
                Equal(true,rejected,"unsupported audio encoding rejected");
                using(var voice=new VoiceProcess()) {
                    rejected=false;try {voice.Play(new VoiceRequest {FailForTest=true},System.Threading.CancellationToken.None).GetAwaiter().GetResult();} catch(InvalidOperationException){rejected=true;}
                    Equal(true,rejected,"child process failure contained");
                    var playback=voice.Play(new VoiceRequest {Audio=Convert.ToBase64String(SampleWave(100))},System.Threading.CancellationToken.None); playback.GetAwaiter().GetResult();
                    Equal(System.Threading.Tasks.TaskStatus.RanToCompletion,playback.Status,"playback recovers after child failure");
                }
                ProviderContractChecks();
                SentencePairChecks();
                Stage2Checks();
                if(Array.IndexOf(args,"--desktop-check")>=0) using(var tray=new TrayIcon(new System.Windows.Forms.ContextMenuStrip(),delegate {},true)) {
                    Equal(true,tray.Registered,"Windows accepts tray registration");
                    Equal(true,tray.HasRectangle(),"Windows exposes tray icon rectangle");
                    using(var overlay=new Overlay(true)) { Equal(false,overlay.Handle==IntPtr.Zero,"overlay initializes before first show"); overlay.Follow(new Rectangle(100,400,2,20)); Equal(true,overlay.Visible,"overlay displays through real HWND"); }
                }
                if(Array.IndexOf(args,"--panel-shot")>=0) return PanelShot.Capture(args[Array.IndexOf(args,"--panel-shot")+1]);
                if(Array.IndexOf(args,"--panel-check")>=0) { PanelLayoutChecks(); PairPanelChecks(); }
                if(Array.IndexOf(args,"--liquid-check")>=0) LiquidChecks();
                if(Array.IndexOf(args,"--skin-check")>=0) SkinChecks();
                if(Array.IndexOf(args,"--pet-check")>=0) PetChecks();
                if(Array.IndexOf(args,"--playback-check")>=0) PlaybackChecks();
                var typing=new TypingSession();var clock=new DateTime(2026,10,4);
                Equal<string>(null,typing.Observe(S("已有内容。","t"),false,clock),"focus does not upload existing content");
                Equal<string>(null,typing.Observe(S("已有内容。我想去看电影","t"),false,clock.AddMilliseconds(50)),"typing waits for pause");
                Equal<string>(null,typing.Observe(S("已有内容。我明天想去看电影。","t"),false,clock.AddMilliseconds(600)),"editing resets debounce");
                Equal("我明天想去看电影。",typing.Observe(S("已有内容。我明天想去看电影。","t"),false,clock.AddMilliseconds(1650)),"whole edited sentence excludes history");
                Equal<string>(null,typing.Observe(S("已有内容。我明天想去看电影。","t"),false,clock.AddMilliseconds(3000)),"unchanged input does not repeat");
                typing.Observe(S("已有内容。我想去看电影。","t"),false,clock.AddMilliseconds(3100));
                Equal("我想去看电影。",typing.Observe(S("已有内容。我想去看电影。","t"),false,clock.AddMilliseconds(4200)),"deletion translates revised sentence");
                Equal<string>(null,typing.Observe(S("another existing document","other"),false,clock.AddMilliseconds(5000)),"focus change resets baseline");
                typing.Baseline(S("","ime"));var composing=S("wo","ime");composing.Composing=true;
                Equal<string>(null,typing.Observe(composing,false,clock),"composition is never submitted");
                Equal<string>(null,typing.Observe(composing,false,clock.AddSeconds(2)),"long composition still suppressed");
                typing.Observe(S("我","ime"),false,clock.AddSeconds(3));
                Equal("我",typing.Observe(S("我","ime"),false,clock.AddSeconds(4.1)),"committed IME text translates");
                var secret=S("hidden","ime");secret.Protected=true;
                Equal<string>(null,typing.Observe(secret,false,clock.AddSeconds(6)),"protected input cancels typing");
                Equal("",TypingSession.Sentence(new string('字',1801),12),"long sentence excluded");
                Equal("世界。",TypingSession.Sentence("你好。世界。后文。",5),"edited sentence has no neighboring history");
                var entry=WordDictionary.Find("tomorrow").GetAwaiter().GetResult();
                Equal(true,entry!=null&&entry.Meaning.Contains("明天"),"embedded local dictionary has real definition");
                Equal(true,entry.Phonetic.Length>0,"embedded dictionary has phonetics");
                var scrollable=WordDictionary.Find("scrollable").GetAwaiter().GetResult();
                Equal(true,scrollable!=null&&scrollable.Word=="scrollable"&&scrollable.Meaning.Contains("卷动"),"unranked adjective scrollable retains its own meaning");
                Equal(true,WordDictionary.Find("clickable").GetAwaiter().GetResult()!=null,"unranked computing word clickable remains available");
                Equal<WordEntry>(null,WordDictionary.Find("zzzznotawordzzzz").GetAwaiter().GetResult(),"unknown word not fabricated");
                var fastTyping=new TypingSession();fastTyping.Baseline(S("","fast"));
                fastTyping.Observe(S("我明天想去看电影。","fast"),false,clock);
                Equal<string>(null,fastTyping.Observe(S("我明天想去看电影。","fast"),false,clock.AddMilliseconds(499)),"typing waits until 500ms");
                Equal("我明天想去看电影。",fastTyping.Observe(S("我明天想去看电影。","fast"),false,clock.AddMilliseconds(500)),"typing fires at 500ms");
                var voiceCapture=new CaptureSession();voiceCapture.Start(S("原有。","voice"),clock,true);
                Equal<string>(null,voiceCapture.Observe(S("原有。听写中","voice"),true,clock.AddMilliseconds(100)),"voice never sends while held");
                Equal("听写完成。",voiceCapture.Observe(S("原有。听写完成。","voice"),false,clock.AddMilliseconds(110)),"voice release submits without stability delay");
                voiceCapture.Start(S("","voice"),clock,true);
                Equal<string>(null,voiceCapture.Observe(S("","voice"),false,clock.AddMilliseconds(100)),"voice release waits for actual text");
                Equal("迟到的识别结果",voiceCapture.Observe(S("迟到的识别结果","voice"),false,clock.AddMilliseconds(300)),"late voice result submits on arrival");
                voiceCapture.Start(S("","voice"),clock,true);var preedit=S("拼音","voice");preedit.Composing=true;
                Equal<string>(null,voiceCapture.Observe(preedit,false,clock.AddMilliseconds(400)),"voice still respects composition");
                Equal(false,Probe.Json.Deserialize<Configuration>(Probe.Json.Serialize(new Configuration {ShowOriginal=false})).ShowOriginal,"English only survives config roundtrip");
                Equal(true,Probe.Json.Deserialize<Configuration>("{}").ShowOriginal,"existing config defaults to bilingual");
                Console.WriteLine("PASS: " + count + " assertions"); return 0;
            } catch (Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
        }
    }
}
