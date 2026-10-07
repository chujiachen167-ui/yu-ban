using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Rectangle=System.Drawing.Rectangle;
using Forms=System.Windows.Forms;

namespace EnglishCompanion {
    // 浮窗：按句中英对照渲染、整段复制、悬停查词、展开收起与动画。
    internal sealed class Overlay : IDisposable {
        readonly Window window;
        readonly Border shell,footer,outline;
        readonly PetAdornment pet;
        readonly System.Windows.Shapes.Path foldShape;
        double expandedHeight=180;
        bool foldPointsUp;
        readonly ScrollViewer scroll;
        readonly StackPanel textContent;
        // 一个只读富文本承载全部内容：逐句对照是排版，不是多个独立控件，
        // 因此拖选可以跨组、选区不会因为查词或重绘而丢失。
        readonly RichTextBox editor;
        readonly FlowDocument document = NewDocument();
        readonly List<GroupRange> ranges = new List<GroupRange>();
        // 供检查使用：当前渲染出的每行所属的句组。
        internal List<GroupRange> Ranges { get { return ranges; } }
        readonly TextBlock status;
        readonly OverlayButton fold,close,copy,pin,wordAudio,contextButton,cardClose;
        readonly System.Windows.Shapes.Path pinShape;
        internal readonly OverlayText Original,Translation;
        internal readonly OverlayButton Speak,Retry,Settings,Manual;
        internal event Action Dismissed;
        internal Action<string> SpeakWord;
        internal Func<string,string,CancellationToken,Task<string>> ExplainWord;
        readonly Popup popup;
        readonly Border card;
        readonly TextBlock wordTitle,meaning,explanation;
        readonly DispatcherTimer hover=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(300)};
        readonly DispatcherTimer leave=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
        readonly DispatcherTimer copyFeedback=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(1400)};
        CancellationTokenSource explainJob;
        bool expanded,learnable,pinned,dragging,disposed,previewMode,showOriginal=true,hasAnchor;
        string skinId="glass",activeWord="",hoverWord="",hoverContext="";
        PairResult pairing;
        int cardVersion;
        int resizeVersion;
        bool resizing;
        Rectangle anchor;

        // 一段文字在文档中的位置，用来把悬停点还原到它所在的句组。
        internal struct GroupRange { internal TextPointer Start, End; internal int Group; internal bool Target; }

        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetStyle(IntPtr hwnd,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetStyle(IntPtr hwnd,int index,IntPtr value);

        internal Overlay(bool demo) {            window=new Window {Title=demo?"语伴 · 离线演示":"语伴 · 整句翻译",Width=430,Height=140,
                WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,
                Topmost=true,ShowInTaskbar=false,ShowActivated=false,FontFamily=new FontFamily("Microsoft YaHei UI"),UseLayoutRounding=true};
            shell=new Border {CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1)};
            var frame=new Grid();frame.Children.Add(shell);window.Content=frame;WindowMaterial.Attach(frame,16);
            // The outline is drawn LAST, above footer fill and button hover backgrounds at all four corners.
            outline=new Border {CornerRadius=new CornerRadius(15.5),BorderThickness=new Thickness(1.2),Margin=new Thickness(.6),IsHitTestVisible=false};
            var trim=new Border {Height=1.5,Margin=new Thickness(19,1,19,0),VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};
            frame.Children.Add(trim);frame.Children.Add(outline);
            var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});shell.Child=grid;
            var body=new Grid {Margin=new Thickness(5,9,0,8)};
            body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});
            grid.Children.Add(body);
            textContent=new StackPanel {Margin=new Thickness(5,3,6,2)};
            // 原文与译文是同一份结果的两种读法：句组排版由 FlowDocument 承载，
            // 这两个对象只负责保存整段文字，供复制、朗读、缓存与降级使用。
            Original=new OverlayText(new TextBlock {FontSize=12,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed});
            Translation=new OverlayText(new TextBlock {FontSize=15,FontFamily=new FontFamily("Segoe UI")});
            document.PagePadding=new Thickness(0);
            editor=new RichTextBox {IsReadOnly=true,IsReadOnlyCaretVisible=false,AutoWordSelection=true,
                BorderThickness=new Thickness(0),Padding=new Thickness(0),Margin=new Thickness(0),Background=Brushes.Transparent,
                VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                MinHeight=24,SelectionBrush=Skin.Brush("#5C8FDC"),SelectionOpacity=0.35,Focusable=true,
                FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=13,Document=document};
            System.Windows.Automation.AutomationProperties.SetName(editor,"对照译文");
            textContent.Children.Add(editor);
            // Focusing a tall editor must not scroll the entire editor into view and move the word under the pointer.
            editor.RequestBringIntoView+=delegate(object sender,RequestBringIntoViewEventArgs e) {e.Handled=true;};
            status=new TextBlock {FontSize=11,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed,Margin=new Thickness(0,7,0,0)};
            textContent.Children.Add(status);
            scroll=new ScrollViewer {Content=textContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                CanContentScroll=false,Focusable=false};scroll.Template=LeftScrollTemplate();body.Children.Add(scroll);
            pet=new PetAdornment(window,trim){Margin=new Thickness(10,0,0,0)};body.Children.Add(pet);
            fold=new OverlayButton("","");fold.Name("展开");fold.Control.Padding=new Thickness(0);fold.Control.Width=40;fold.Control.Height=32;fold.Control.VerticalAlignment=VerticalAlignment.Top;
            foldShape=new System.Windows.Shapes.Path {Width=16,Height=16,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};fold.Control.Content=foldShape;
            Grid.SetColumn(fold.Control,1);body.Children.Add(fold.Control);fold.Click+=delegate {SetExpanded(!expanded);};
            footer=new Border {BorderThickness=new Thickness(0,1,0,0)};Grid.SetRow(footer,1);grid.Children.Add(footer);
            var buttons=new DockPanel {LastChildFill=false};footer.Child=buttons;
            Speak=new OverlayButton("朗读","\uE768");Retry=new OverlayButton("","\uE72C");Retry.Name("重新翻译");
            Speak.Control.Width=88;
            copy=new OverlayButton("","\uE8C8");copy.Name("复制译文");
            Settings=new OverlayButton("","\uE713");Settings.Name("设置");close=new OverlayButton("","\uE711");close.Name("关闭");
            close.Control.Width=Settings.Control.Width=40;close.Control.Height=Settings.Control.Height=40;close.Control.Padding=Settings.Control.Padding=new Thickness(0);
            Manual=new OverlayButton("粘贴翻译","\uE77F");Manual.Control.Visibility=Visibility.Collapsed;
            DockPanel.SetDock(close.Control,Dock.Right);buttons.Children.Add(close.Control);
            DockPanel.SetDock(Settings.Control,Dock.Right);buttons.Children.Add(Settings.Control);
            foreach(var b in new[]{Speak,Retry,copy,Manual})buttons.Children.Add(b.Control);
            Speak.Enabled=Retry.Enabled=copy.Enabled=false;
            close.Click+=delegate {if(previewMode)window.Close();else Hide();if(Dismissed!=null)Dismissed();};
            copy.Click+=delegate {CopyAll();};copyFeedback.Tick+=delegate {copyFeedback.Stop();copy.Glyph="\uE8C8";copy.Name("复制译文");};
            var menu=new ContextMenu();var selectionCopy=new MenuItem {Header="复制",Command=ApplicationCommands.Copy,CommandTarget=editor};
            menu.Items.Add(selectionCopy);menu.Items.Add(new MenuItem {Header="全选",Command=ApplicationCommands.SelectAll,CommandTarget=editor});editor.ContextMenu=menu;
            menu.Opened+=delegate {hover.Stop();CloseCard();};

            card=new Border {Width=340,Padding=new Thickness(14),Background=Skin.Brush("#FCFFFFFF"),BorderBrush=Skin.Brush("#CDD9EB"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12)};
            popup=new Popup {Child=card,AllowsTransparency=true,StaysOpen=true,Focusable=false,PlacementTarget=editor,Placement=PlacementMode.MousePoint};
            var stack=new StackPanel();card.Child=stack;var header=new DockPanel();stack.Children.Add(header);
            cardClose=new OverlayButton("","\uE711");cardClose.Name("关闭词卡");cardClose.Control.Padding=new Thickness(6);DockPanel.SetDock(cardClose.Control,Dock.Right);header.Children.Add(cardClose.Control);
            pin=new OverlayButton("","\uE718");pin.Name("固定词卡");pin.Control.Padding=new Thickness(6);DockPanel.SetDock(pin.Control,Dock.Right);header.Children.Add(pin.Control);
            pinShape=new System.Windows.Shapes.Path {Data=Geometry.Parse("M 4 2 L 12 2 M 5 2 L 5 7 L 2 10 L 2 11 L 14 11 L 14 10 L 11 7 L 11 2 M 8 11 L 8 17"),Stroke=Skin.Brush("#33455E"),StrokeThickness=1.4,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=18,Height=18,RenderTransform=new RotateTransform(35,8,9)};
            pin.Control.Content=pinShape;
            pin.Click+=delegate {pinned=!pinned;UpdatePin();};cardClose.Click+=delegate {CloseCard();};
            wordTitle=new TextBlock {FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=Skin.Brush("#172B4D"),TextWrapping=TextWrapping.Wrap};header.Children.Add(wordTitle);
            meaning=new TextBlock {FontSize=13,TextWrapping=TextWrapping.Wrap,Foreground=Skin.Brush("#33455E"),Margin=new Thickness(0,9,0,0)};
            stack.Children.Add(new ScrollViewer {Content=meaning,MaxHeight=175,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            var actions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0)};stack.Children.Add(actions);
            wordAudio=new OverlayButton("发音","\uE767");contextButton=new OverlayButton("结合本句解释","\uE8F2");
            contextButton.Control.ToolTip="使用翻译模型解释当前句子中的含义";
            actions.Children.Add(wordAudio.Control);actions.Children.Add(contextButton.Control);
            wordAudio.Click+=delegate {if(SpeakWord!=null)SpeakWord(activeWord);};contextButton.Click+=async delegate {await Explain();};
            explanation=new TextBlock {FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=Skin.Brush("#526782")};
            stack.Children.Add(new ScrollViewer {Content=explanation,MaxHeight=125,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            card.MouseEnter+=delegate {leave.Stop();};card.MouseLeave+=delegate {if(!pinned)leave.Start();};
            hover.Tick+=async delegate {hover.Stop();if(!dragging&&SelectedLength()==0&&Mouse.LeftButton==MouseButtonState.Released&&hoverWord.Length>0&&!pinned)await ShowWord(hoverWord);};
            leave.Tick+=delegate {leave.Stop();if(!pinned&&!card.IsMouseOver&&!editor.IsMouseOver)CloseCard();};
            editor.MouseMove+=HoverWord;
            editor.MouseLeave+=delegate {hover.Stop();hoverWord="";if(!pinned)leave.Start();};
            editor.PreviewMouseLeftButtonDown+=delegate {dragging=true;pet.Pause(true);CloseCard();window.Activate();editor.Focus();};
            editor.PreviewMouseLeftButtonUp+=delegate {dragging=false;pet.Pause(SelectedLength()>0);window.Dispatcher.BeginInvoke(new Action(delegate {if(!disposed)HoverWord(editor,new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount));}));};editor.LostMouseCapture+=delegate {dragging=false;pet.Pause(SelectedLength()>0);};
            editor.SelectionChanged+=delegate {pet.Pause(dragging||SelectedLength()>0);if(SelectedLength()>0)CloseCard();};
            editor.PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e) {CloseCard();scroll.ScrollToVerticalOffset(scroll.VerticalOffset-e.Delta/3.0);e.Handled=true;};
            scroll.ScrollChanged+=delegate {if(!pinned)CloseCard();};
            Original.Changed=delegate {if(pairing==null)RebuildPlain();};
            Translation.Changed=delegate {if(pairing==null)RebuildPlain();};
            window.SizeChanged+=delegate {if(hasAnchor&&!disposed&&!resizing)Place();};
            window.SourceInitialized+=delegate {
                var s=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);s.CompositionTarget.BackgroundColor=Colors.Transparent;
                SetStyle(s.Handle,-20,new IntPtr(GetStyle(s.Handle,-20).ToInt64()|0x08000000|0x80));
                s.AddHook(delegate(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled) {if(msg==0x21){handled=true;return new IntPtr(editor.IsMouseOver?1:3);}return IntPtr.Zero;});
                AppIcon.ApplyTo(window);ApplySkin(skinId);
            };
        }

        // ---------- 内容：整段、单控件、逐句成组 ----------

        // 句组结果：每组原文一行、译文一行，组内紧、组间松。收起与展开共用同一份内容。
        internal void ShowPairs(PairResult result) {
            CloseCard();copyFeedback.Stop();copy.Glyph="\uE8C8";copy.Name("复制译文");
            pairing=result;status.Visibility=Visibility.Collapsed;
            BuildDocument();scroll.ScrollToTop();Resize(false);Settle();
        }
        // 富文本的真实高度要在窗口完成一次布局后才稳定；此时再对齐一次窗口高度。
        void Settle() {
            if (!window.IsVisible) return;
            window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(ResizeLater));
        }
        void ResizeLater() { if (!disposed) { Resize(false); PumpOnce(); Resize(false); } }
        // 让 WPF 完成一次布局通道，使下一次测量拿到真实高度。
        void PumpOnce() {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(0) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        // 回到整段排版：提示、错误、听写中与清空都走这条路。
        internal void ClearPairs() {
            if (pairing==null) return;
            pairing=null;RebuildPlain();
        }
        // 非对照内容（提示、错误、听写中）：仍然是同一个只读控件，排版与选择行为一致。
        void RebuildPlain() {
            CloseCard();status.Visibility=Visibility.Collapsed;
            BuildDocument();scroll.ScrollToTop();Resize(false);
        }
        void BuildDocument() {
            document.Blocks.Clear();ranges.Clear();
            if (pairing!=null&&pairing.Pairs!=null&&pairing.Pairs.Count>0) {
                bool bilingual=showOriginal;
                var shown=new List<SentencePair>();
                for (int i = 0; i < pairing.Pairs.Count; i++)
                    if (pairing.Pairs[i] != null && pairing.Pairs[i].Target != null && pairing.Pairs[i].Target.Length > 0) shown.Add(pairing.Pairs[i]);
                for (int i = 0; i < shown.Count; i++) {
                    var pair = shown[i];
                    bool last = i == shown.Count - 1;
                    if (bilingual && pair.Source != null && pair.Source.Length > 0) {
                        var top = new Paragraph {Margin=new Thickness(0,0,0,1)};
                        top.Inlines.Add(Source(pair.Source));
                        document.Blocks.Add(top);
                        Record(top, i, false);
                    }
                    // 组内紧、组间略松：只用段间距表达，不加分隔线、卡片或说明文字。
                    var bottom = new Paragraph {Margin=new Thickness(0,0,0,last?0:9)};
                    bottom.Inlines.Add(Target(pair.Target));
                    document.Blocks.Add(bottom);
                    Record(bottom, i, true);
                }
            } else {
                string text = Translation.Text ?? "";
                if (text.Length > 0) {
                    var only = new Paragraph {Margin=new Thickness(0)};
                    only.Inlines.Add(new Run(text) {FontSize = 15, FontFamily = new FontFamily("Segoe UI")});
                    document.Blocks.Add(only);
                    Record(only, 0, true);
                }
            }
        }
        // 原文行：稍小、稍淡；译文行：更突出。长句照常换行，不截断、不缩小到难读。
        Run Source(string value) { return new Run(value) { FontSize = 12, FontFamily = new FontFamily("Microsoft YaHei UI") }; }
        Run Target(string value) { return new Run(value ?? "") { FontSize = 15, FontFamily = new FontFamily("Segoe UI") }; }
        // 记录每段在文档中的位置；Range 持有 TextPosition，不依赖偏移量在后续编辑中保持不变。
        void Record(Paragraph block,int group,bool isTarget) {
            var range = new TextRange(block.ContentStart, block.ContentEnd);
            ranges.Add(new GroupRange { Start = range.Start, End = range.End, Group = group, Target = isTarget });
        }
        void RestyleDocument() {
            var s = Skin.Get(skinId);
            foreach (var record in ranges) {
                if (record.Start == null || record.End == null) continue;
                var run = record.Start.Parent as Run; if (run == null) continue;
                run.Foreground = Skin.Brush(record.Target ? s.Ink : s.Muted);
            }
        }

        // ---------- 选择、复制与查词 ----------

        // 悬停点先还原成字符位置，再找出它所在的句组；同一组两侧共享上下文。
        GroupRange RangeAt(Point point) {
            var hit = editor.GetPositionFromPoint(point, true);
            if (hit == null) return new GroupRange { Group = -1 };
            return GroupAt(hit.GetPositionAtOffset(0));
        }
        // 文档内的绝对偏移：TextPointer 没有公开 Offset，用与文档起点的距离代替。
        int OffsetOf(TextPointer position) { return position == null ? -1 : document.ContentStart.GetOffsetToPosition(position); }
        GroupRange GroupAt(TextPointer position) {
            GroupRange found = new GroupRange { Group = -1 };
            int offset = OffsetOf(position);
            if (offset < 0) return found;
            foreach (var record in ranges) {
                if (record.Start == null) continue;
                if (offset < OffsetOf(record.Start) || offset > OffsetOf(record.End)) continue;
                // 悬停落在原文行时，仍然用该组的上下文；两组相邻时不串组。
                if (found.Group < 0 || found.Group != record.Group) found = record;
            }
            return found;
        }
        // 查词和“结合本句解释”都用整组原文+译文，不从屏幕上拼凑。
        // 索引与 BuildDocument 的过滤结果一致：只包含真正对齐的组。
        string GroupText(int group) {
            if (pairing == null || pairing.Pairs == null || group < 0) return Translation.Text;
            var shown = new List<SentencePair>();
            for (int i = 0; i < pairing.Pairs.Count; i++)
                if (pairing.Pairs[i] != null && pairing.Pairs[i].Target != null && pairing.Pairs[i].Target.Length > 0) shown.Add(pairing.Pairs[i]);
            if (group >= shown.Count) return Translation.Text;
            var pair = shown[group];
            return showOriginal && pair.Source != null && pair.Source.Length > 0
                ? pair.Source + "\n" + pair.Target : pair.Target;
        }

        // 段落的默认外边距会让行距忽大忽小：统一清零，只用我们自己的组间距。
        static FlowDocument NewDocument() {
            var d = new FlowDocument { PagePadding = new Thickness(0) };
            var style = new Style(typeof(Paragraph));
            style.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0)));
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Microsoft YaHei UI")));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 13.0));
            d.Blocks.Add(new Paragraph());
            return d;
        }
        internal int SelectedLength() { return editor.Selection.IsEmpty ? 0 : Math.Abs(editor.Selection.End.GetOffsetToPosition(editor.Selection.Start)); }
        // 按文档偏移选择，供检查与键盘操作使用。
        internal void SelectRange(int start,int length) {
            var from=document.ContentStart.GetPositionAtOffset(start);
            var to=document.ContentStart.GetPositionAtOffset(start+length);
            editor.Selection.Select(from,to);editor.Focus();
        }
        static ControlTemplate LeftScrollTemplate() {
            return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollViewer'>
              <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                <ScrollContentPresenter x:Name='PART_ScrollContentPresenter' Grid.Column='1' Content='{TemplateBinding Content}' ContentTemplate='{TemplateBinding ContentTemplate}' CanContentScroll='{TemplateBinding CanContentScroll}'/>
                <ScrollBar x:Name='PART_VerticalScrollBar' Width='9' MinWidth='0' Margin='0,2,2,2' Orientation='Vertical' Foreground='{TemplateBinding Foreground}' Maximum='{TemplateBinding ScrollableHeight}' ViewportSize='{TemplateBinding ViewportHeight}' Value='{Binding VerticalOffset,RelativeSource={RelativeSource TemplatedParent},Mode=OneWay}' Visibility='{TemplateBinding ComputedVerticalScrollBarVisibility}'>
                  <ScrollBar.Template><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Border Width='3.5' HorizontalAlignment='Center' Background='#178099BE' CornerRadius='1.75'/>
                    <Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True'>
                      <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>
                      <Track.Thumb><Thumb MinHeight='22' MinWidth='0' Background='{TemplateBinding Foreground}'><Thumb.Template><ControlTemplate TargetType='Thumb'><Grid Background='Transparent'><Border x:Name='SlimThumb' Width='3.5' HorizontalAlignment='Center' Background='{TemplateBinding Background}' CornerRadius='1.75'/></Grid></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                      <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>
                    </Track></Grid></ControlTemplate></ScrollBar.Template>
                </ScrollBar>
              </Grid></ControlTemplate>");
        }
        internal bool Learnable {set {learnable=value;copy.Enabled=value;if(!value)CloseCard();}}
        internal bool ShowOriginal {
            get {return showOriginal;}
            set {if(showOriginal==value)return;showOriginal=value;if(pairing==null)Original.Control.Visibility=value&&Original.Text.Length>0?Visibility.Visible:Visibility.Collapsed;BuildDocument();Resize(false);}
        }
        internal bool Fallback {set {Manual.Control.Visibility=value?Visibility.Visible:Visibility.Collapsed;}}
        // 朗读未就绪时的原因：只在悬停朗读时说明，不加常驻小字。
        // 语音未配置不影响翻译；这里只解释为什么这一次朗读不可用。
        internal string SpeakBlocker {
            set {
                speakBlocker = value ?? "";
                Speak.Control.ToolTip = speakBlocker.Length>0 ? "朗读暂不可用：" + speakBlocker + "。翻译不受影响，可在设置里补齐" : "朗读";
            }
        }
        string speakBlocker="";
        internal bool Interacting {get {return window.IsMouseOver||card.IsMouseOver||dragging||editor.IsKeyboardFocusWithin;}}
        internal void Message(string text) {status.Text=text;status.Visibility=Visibility.Visible;Resize(false);}
        internal void SetPlayback(PlaybackState state,bool word=false) {Speak.SetPlayback(state);wordAudio.SetPlayback(word?state:PlaybackState.Idle);}
        internal void SetExpanded(bool value) {expanded=value;CloseCard();fold.Name(value?"收起":"展开");fold.Control.Background=value?Skin.Brush(Skin.Get(skinId).Hover):Brushes.Transparent;Resize(true);}
        void UpdateFoldDirection() {
            bool growsUp=true;
            if(hasAnchor){var src=HwndSource.FromHwnd(Handle);growsUp=Placement(expandedHeight).Y<anchor.Top/src.CompositionTarget.TransformToDevice.M22;}
            foldPointsUp=expanded?!growsUp:growsUp;
            foldShape.Data=Geometry.Parse(foldPointsUp?"M2,11 L8,5 L14,11":"M2,5 L8,11 L14,5");
        }
        void Resize(bool animate) {
            if(textContent==null||scroll==null)return;
            double width=skinId=="glass"?355:277;
            // 用临时容器量一次真实文本高度：直接量 editor 会被上一次写入的 Height 反馈放大。
            double editorHeight=MeasureContent(width);
            editor.Height=editorHeight;
            status.Measure(new Size(width,Double.PositiveInfinity));
            double content=editorHeight+(status.Visibility==Visibility.Visible?status.DesiredSize.Height+7:0);
            textContent.Margin=new Thickness(skinId=="glass"?5:83,3+(skinId!="glass"?Math.Max(0,(72-content)/2):0),6,2);
            if(skinId!="glass")content=Math.Max(72,content);
            // Updating the read-only editor height can leave the outer custom presenter at its old extent.
            textContent.InvalidateMeasure();scroll.InvalidateMeasure();
            // Expanded short sentences only gain breathing room; long text gains a taller viewport.
            // Measure the CURRENT animated value, so rapidly reversing direction never snaps to an old endpoint.
            double from=window.Height;
            expandedHeight=Math.Max(108,Math.Min(360,content+78));
            double to=expanded?expandedHeight:Math.Max(96,Math.Min(140,content+64));
            UpdateFoldDirection();
            int version=++resizeVersion;resizing=true;
            var point=hasAnchor?Placement(to):new Point(window.Left,window.Top);
            if(animate&&window.IsVisible&&SystemParameters.ClientAreaAnimation&&Math.Abs(to-from)>0.5) {
                double duration=Math.Max(150,Math.Min(280,150+Math.Abs(to-from)*0.45));
                var heightMotion=Motion(from,to,duration);
                heightMotion.Completed+=delegate {if(disposed||version!=resizeVersion)return;window.BeginAnimation(FrameworkElement.HeightProperty,null);window.BeginAnimation(Window.TopProperty,null);window.BeginAnimation(Window.LeftProperty,null);resizing=false;};
                window.BeginAnimation(FrameworkElement.HeightProperty,heightMotion,HandoffBehavior.SnapshotAndReplace);
                if(hasAnchor) {
                    window.BeginAnimation(Window.TopProperty,Motion(window.Top,point.Y,duration),HandoffBehavior.SnapshotAndReplace);
                    window.BeginAnimation(Window.LeftProperty,Motion(window.Left,point.X,duration),HandoffBehavior.SnapshotAndReplace);
                }
                window.Height=to;if(hasAnchor){window.Top=point.Y;window.Left=point.X;}
            } else {
                window.Height=to;if(hasAnchor){window.Top=point.Y;window.Left=point.X;}
                window.BeginAnimation(FrameworkElement.HeightProperty,null);window.BeginAnimation(Window.TopProperty,null);window.BeginAnimation(Window.LeftProperty,null);resizing=false;
            }
        }
        // RichTextBox 在窗口真正显示之前永远只量出最小高度；此时排版结果没有意义。
        // 因此先按最小高度占位，显示后再按真实高度对齐一次。
        double MeasureContent(double width) {
            if (!window.IsVisible) return 24;
            double pinned = editor.Height;
            editor.ClearValue(FrameworkElement.HeightProperty);
            editor.Width = width;
            editor.InvalidateMeasure();
            editor.Measure(new Size(width, Double.PositiveInfinity));
            double text = editor.DesiredSize.Height;
            editor.ClearValue(FrameworkElement.WidthProperty);
            editor.Height = pinned;
            return Math.Max(24, text);
        }
        static DoubleAnimation Motion(double from,double to,double duration) {return new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(duration)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};}
        // 整段按钮只拿完整译文，不含原文、不含屏幕上的排版标记。
        void CopyAll() {
            if(!learnable||Translation.Text.Length==0)return;
            try {Clipboard.SetText(Translation.Text);copy.Glyph="\uE73E";copy.Name("已复制");copyFeedback.Stop();copyFeedback.Start();}
            catch(ExternalException) {Message("复制暂不可用，请重试");}
        }
        void HoverWord(object sender,MouseEventArgs e) {
            if(!expanded||!learnable||pinned||dragging||SelectedLength()>0||Mouse.LeftButton==MouseButtonState.Pressed||editor.ContextMenu.IsOpen) {hover.Stop();return;}
            var at = e != null ? RangeAt(e.GetPosition(editor)) : new GroupRange { Group = -1 };
            // 查词只在译文行上触发：原文是中文，行内没有英文词。
            string text = at.Target ? TextAt(at) : "";
            int offset = at.Target ? OffsetIn(e, at) : 0;
            string word = WordAt(text, offset);
            if (word == hoverWord) return;
            hover.Stop();hoverWord=word;hoverContext=GroupText(at.Group);
            if(word.Length==0) {if(!pinned)CloseCard();return;}
            leave.Stop();hover.Start();
        }
        string TextAt(GroupRange record) { if(record.Start==null||record.End==null)return"";return new TextRange(record.Start,record.End).Text; }
        int OffsetIn(MouseEventArgs e,GroupRange record) {
            if(e==null||record.Start==null)return 0;
            var hit=editor.GetPositionFromPoint(e.GetPosition(editor),true);if(hit==null)return 0;
            return OffsetOf(hit.GetPositionAtOffset(0))-OffsetOf(record.Start);
        }
        internal static string WordAt(string text,int index) {
            if(index<0||index>=text.Length)return "";
            foreach(Match match in Regex.Matches(text,@"[A-Za-z]+(?:['’\-][A-Za-z]+)*")) if(index>=match.Index&&index<match.Index+match.Length)return match.Value;
            return "";
        }
        void UpdatePin() {pin.Name(pinned?"取消固定":"固定词卡");pin.Control.Background=pinned?Skin.Brush(Skin.Get(skinId).Hover):Brushes.Transparent;}
        async Task ShowWord(string word) {
            activeWord=word;int version=++cardVersion;wordTitle.Text=word;meaning.Text="查询中…";explanation.Text="";popup.IsOpen=true;
            try {var entry=await WordDictionary.Find(word);if(version!=cardVersion||disposed)return;
                wordTitle.Text=word+(entry==null||entry.Phonetic.Length==0?"":"  /"+entry.Phonetic+"/");
                meaning.Text=entry==null?"本地词典未收录":(entry.Word!=word.ToLowerInvariant()?"原形："+entry.Word+"\n":"")+entry.Meaning;
            } catch {if(version==cardVersion)meaning.Text="词典暂不可用";}
        }
        async Task Explain() {
            if(ExplainWord==null||explainJob!=null)return;int version=cardVersion;explainJob=new CancellationTokenSource();contextButton.Enabled=false;explanation.Text="正在解释…";
            try {var result=await ExplainWord(activeWord,hoverContext.Length>0?hoverContext:Translation.Text,explainJob.Token);if(version==cardVersion)explanation.Text=result;}
            catch(OperationCanceledException){}catch(Exception e){if(version==cardVersion)explanation.Text=e is InvalidOperationException?e.Message:"解释暂不可用，请重试";}
            finally {if(version==cardVersion){explainJob.Dispose();explainJob=null;contextButton.Enabled=true;}}
        }
        void CloseCard() {hover.Stop();leave.Stop();hoverWord="";hoverContext="";pinned=false;cardVersion++;popup.IsOpen=false;UpdatePin();if(explainJob!=null){explainJob.Cancel();explainJob.Dispose();explainJob=null;}contextButton.Enabled=true;}
        internal void ApplySkin(string id) {
            var s=Skin.Get(id);skinId=s.Id;shell.Background=Skin.Brush(s.Surface);shell.BorderBrush=Skin.Brush(s.Edge);
            outline.BorderBrush=Skin.Brush(s.Id=="glass"?"#A6BBD7":s.Id=="ocean"?"#82B6E8":"#D8BD57");
            foldShape.Stroke=Skin.Brush(s.Ink);pet.ApplySkin(s);
            textContent.Margin=new Thickness(s.Id=="glass"?5:83,3,6,2);
            window.Resources["Hover"]=Skin.Brush(s.Hover);card.Resources["Hover"]=Skin.Brush(s.Hover);
            footer.Background=Skin.Brush(s.Panel);footer.BorderBrush=Skin.Brush(s.Edge);Original.Control.Foreground=status.Foreground=Skin.Brush(s.Muted);
            editor.Foreground=Skin.Brush(s.Ink);editor.SelectionBrush=Skin.Brush(s.Focus);scroll.Foreground=Skin.Brush(s.Muted);
            card.Background=Skin.Brush(s.Panel);card.BorderBrush=Skin.Brush(s.FieldEdge);
            wordTitle.Foreground=meaning.Foreground=Skin.Brush(s.Ink);explanation.Foreground=Skin.Brush(s.Muted);pinShape.Stroke=Skin.Brush(s.Ink);
            foreach(var b in new[]{Speak,fold,copy,Manual,Retry,Settings,close,pin,wordAudio,contextButton,cardClose})b.Control.Foreground=Skin.Brush(s.Ink);
            fold.Control.Background=expanded?Skin.Brush(s.Hover):Brushes.Transparent;UpdatePin();
            RestyleDocument();
            Resize(false);
        }
        internal IntPtr Handle {get {return new WindowInteropHelper(window).EnsureHandle();}}
        internal bool Visible {get {return window.IsVisible;}}
        internal void InspectableDemo() {window.ShowInTaskbar=true;var h=Handle;SetStyle(h,-20,new IntPtr((GetStyle(h,-20).ToInt64()&~0x80L)|0x40000L));}
        internal void Preview(string length="") {previewMode=true;InspectableDemo();Original.Text="好的，我现在再来测试一下。";
            string sample="This scrollable panel lets us read complete sentences, listen to their pronunciation, and explore how words work in everyday conversations.";
            string a=length=="short"?"Let me try again.":length=="long"?String.Join(" ",System.Linq.Enumerable.Repeat(sample,12)):"Okay, let me try again now. Tomorrow we can take a little more time to explore the city, visit the museum, and meet our friends for dinner.";
            string b=length=="long"?"Learning a language becomes easier when we use complete sentences in everyday conversations.":"Because it might rain this evening, we should take an umbrella with us today.";
            var demo2=new PairResult {Source=Original.Text,Target=a+" "+b,Direction="English"};
            demo2.Pairs.Add(new SentencePair {Id="s1",Source=Original.Text,Target=a});
            demo2.Pairs.Add(new SentencePair {Id="s2",Source=length=="long"?"这样一句话就能撑开滚动区，也能看到长句的换行表现。":"因为晚上可能会下雨。",Target=b});
            Translation.Text=demo2.Target;Learnable=true;anchor=new Rectangle(700,650,2,20);hasAnchor=true;Place();ShowPairs(demo2);window.ShowDialog();}
        internal void BeginInvoke(Action action) {window.Dispatcher.BeginInvoke(action);}
        internal void Hide() {CloseCard();window.Hide();}
        Point Placement(double height) {var src=HwndSource.FromHwnd(Handle);var m=src.CompositionTarget.TransformToDevice;var size=new System.Drawing.Size((int)(window.Width*m.M11),(int)(height*m.M22));var placed=Changes.Place(anchor,size,Forms.Screen.FromRectangle(anchor).WorkingArea);return new Point(placed.X/m.M11,placed.Y/m.M22);}
        void Place() {if(resizing)return;var p=Placement(window.Height);window.Left=p.X;window.Top=p.Y;UpdateFoldDirection();}
        internal void Follow(Rectangle value) {if(!resizing&&(!Interacting||!window.IsVisible)){anchor=value;hasAnchor=true;Place();}if(!window.IsVisible)window.Show();Settle();}
        public void Dispose() {if(disposed)return;disposed=true;CloseCard();pet.Dispose();copyFeedback.Stop();window.Close();}
    }
}
