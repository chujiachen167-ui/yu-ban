using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Rectangle=System.Drawing.Rectangle;
using Forms=System.Windows.Forms;

namespace EnglishCompanion {
    internal sealed class Overlay : IDisposable {
        readonly Window window;
        readonly Border shell,footer,outline;
        readonly PetAdornment pet;
        readonly System.Windows.Shapes.Path foldShape;
        double expandedHeight=180;
        bool foldPointsUp;
        readonly ScrollViewer scroll;
        readonly StackPanel textContent;
        readonly TextBox editor;
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
        string skinId="glass",activeWord="",hoverWord="";
        int cardVersion;
        int resizeVersion;
        bool resizing;
        Rectangle anchor;

        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetStyle(IntPtr hwnd,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetStyle(IntPtr hwnd,int index,IntPtr value);

        internal Overlay(bool demo) {
            window=new Window {Title=demo?"语伴 · 离线演示":"语伴 · 整句翻译",Width=430,Height=140,
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
            Original=new OverlayText(new TextBlock {FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,7)});
            textContent.Children.Add(Original.Control);
            Translation=new OverlayText(new TextBlock {FontSize=17,FontFamily=new FontFamily("Segoe UI"),TextWrapping=TextWrapping.Wrap});
            editor=new TextBox {IsReadOnly=true,IsReadOnlyCaretVisible=false,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
                BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Brushes.Transparent,FontFamily=new FontFamily("Segoe UI"),
                FontSize=17,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                MinHeight=24,SelectionBrush=Skin.Brush("#5C8FDC"),SelectionOpacity=0.35};
            System.Windows.Automation.AutomationProperties.SetName(editor,"译文");textContent.Children.Add(editor);
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
            hover.Tick+=async delegate {hover.Stop();if(!dragging&&editor.SelectionLength==0&&Mouse.LeftButton==MouseButtonState.Released&&hoverWord.Length>0&&!pinned)await ShowWord(hoverWord);};
            leave.Tick+=delegate {leave.Stop();if(!pinned&&!card.IsMouseOver&&!editor.IsMouseOver)CloseCard();};
            editor.MouseMove+=HoverWord;
            editor.MouseLeave+=delegate {hover.Stop();hoverWord="";if(!pinned)leave.Start();};
            editor.PreviewMouseLeftButtonDown+=delegate {dragging=true;pet.Pause(true);CloseCard();window.Activate();editor.Focus();};
            editor.PreviewMouseLeftButtonUp+=delegate {dragging=false;pet.Pause(editor.SelectionLength>0);window.Dispatcher.BeginInvoke(new Action(delegate {if(!disposed)HoverWord(editor,new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount));}));};editor.LostMouseCapture+=delegate {dragging=false;pet.Pause(editor.SelectionLength>0);};
            editor.SelectionChanged+=delegate {pet.Pause(dragging||editor.SelectionLength>0);if(editor.SelectionLength>0)CloseCard();};
            editor.PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e) {CloseCard();scroll.ScrollToVerticalOffset(scroll.VerticalOffset-e.Delta/3.0);e.Handled=true;};
            scroll.ScrollChanged+=delegate {if(!pinned)CloseCard();};
            Original.Changed=delegate {Original.Control.Visibility=showOriginal&&Original.Text.Length>0?Visibility.Visible:Visibility.Collapsed;Resize(false);};
            Translation.Changed=delegate {CloseCard();copyFeedback.Stop();copy.Glyph="\uE8C8";copy.Name("复制译文");editor.Text=Translation.Text;status.Visibility=Visibility.Collapsed;scroll.ScrollToTop();Resize(false);};
            window.SizeChanged+=delegate {if(hasAnchor&&!disposed&&!resizing)Place();};
            window.SourceInitialized+=delegate {
                var s=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);s.CompositionTarget.BackgroundColor=Colors.Transparent;
                SetStyle(s.Handle,-20,new IntPtr(GetStyle(s.Handle,-20).ToInt64()|0x08000000|0x80));
                s.AddHook(delegate(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled) {if(msg==0x21){handled=true;return new IntPtr(editor.IsMouseOver?1:3);}return IntPtr.Zero;});
                AppIcon.ApplyTo(window);ApplySkin(skinId);
            };
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
        internal bool ShowOriginal {get {return showOriginal;} set {showOriginal=value;Original.Control.Visibility=value&&Original.Text.Length>0?Visibility.Visible:Visibility.Collapsed;Resize(false);}}
        internal bool Fallback {set {Manual.Control.Visibility=value?Visibility.Visible:Visibility.Collapsed;}}
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
            Original.Control.Measure(new Size(width,Double.PositiveInfinity));
            Translation.Control.Measure(new Size(width,Double.PositiveInfinity));
            status.Measure(new Size(width,Double.PositiveInfinity));
            // Give the read-only editor its complete text height. The outer viewport alone scrolls.
            editor.Height=Math.Max(24,Translation.Control.DesiredSize.Height+10);
            double content=editor.Height+(showOriginal&&Original.Text.Length>0?Original.Control.DesiredSize.Height+7:0)+(status.Visibility==Visibility.Visible?status.DesiredSize.Height+7:0);
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
        static DoubleAnimation Motion(double from,double to,double duration) {return new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(duration)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};}
        void CopyAll() {
            if(!learnable||Translation.Text.Length==0)return;
            try {Clipboard.SetText(Translation.Text);copy.Glyph="\uE73E";copy.Name("已复制");copyFeedback.Stop();copyFeedback.Start();}
            catch(ExternalException) {Message("复制暂不可用，请重试");}
        }
        void HoverWord(object sender,MouseEventArgs e) {
            if(!expanded||!learnable||pinned||dragging||editor.SelectionLength>0||Mouse.LeftButton==MouseButtonState.Pressed||editor.ContextMenu.IsOpen) {hover.Stop();return;}
            int at=editor.GetCharacterIndexFromPoint(e.GetPosition(editor),false);
            string word=WordAt(editor.Text,at);
            if(word==hoverWord)return;
            hover.Stop();hoverWord=word;
            if(word.Length==0) {if(!pinned)CloseCard();return;}
            leave.Stop();hover.Start();
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
            try {var result=await ExplainWord(activeWord,Translation.Text,explainJob.Token);if(version==cardVersion)explanation.Text=result;}
            catch(OperationCanceledException){}catch(Exception e){if(version==cardVersion)explanation.Text=e is InvalidOperationException?e.Message:"解释暂不可用，请重试";}
            finally {if(version==cardVersion){explainJob.Dispose();explainJob=null;contextButton.Enabled=true;}}
        }
        void CloseCard() {hover.Stop();leave.Stop();hoverWord="";pinned=false;cardVersion++;popup.IsOpen=false;UpdatePin();if(explainJob!=null){explainJob.Cancel();explainJob.Dispose();explainJob=null;}contextButton.Enabled=true;}
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
            Resize(false);
        }
        internal IntPtr Handle {get {return new WindowInteropHelper(window).EnsureHandle();}}
        internal bool Visible {get {return window.IsVisible;}}
        internal void InspectableDemo() {window.ShowInTaskbar=true;var h=Handle;SetStyle(h,-20,new IntPtr((GetStyle(h,-20).ToInt64()&~0x80L)|0x40000L));}
        internal void Preview(string length="") {previewMode=true;InspectableDemo();Original.Text="好的，我现在再来测试一下。";
            string sample="This scrollable panel lets us read complete sentences, listen to their pronunciation, and explore how words work in everyday conversations.";
            Translation.Text=length=="short"?"Let me try again.":length=="long"?String.Join(" ",System.Linq.Enumerable.Repeat(sample,12)):"Okay, let me try again now. Tomorrow we can take a little more time to explore the city, visit the museum, and meet our friends for dinner. Learning a language becomes easier when we use complete sentences in everyday conversations.";
            Learnable=true;anchor=new Rectangle(700,650,2,20);hasAnchor=true;Place();window.ShowDialog();}
        internal void BeginInvoke(Action action) {window.Dispatcher.BeginInvoke(action);}
        internal void Hide() {CloseCard();window.Hide();}
        Point Placement(double height) {var src=HwndSource.FromHwnd(Handle);var m=src.CompositionTarget.TransformToDevice;var size=new System.Drawing.Size((int)(window.Width*m.M11),(int)(height*m.M22));var placed=Changes.Place(anchor,size,Forms.Screen.FromRectangle(anchor).WorkingArea);return new Point(placed.X/m.M11,placed.Y/m.M22);}
        void Place() {if(resizing)return;var p=Placement(window.Height);window.Left=p.X;window.Top=p.Y;UpdateFoldDirection();}
        internal void Follow(Rectangle value) {if(!resizing&&(!Interacting||!window.IsVisible)){anchor=value;hasAnchor=true;Place();}if(!window.IsVisible)window.Show();}
        public void Dispose() {if(disposed)return;disposed=true;CloseCard();pet.Dispose();copyFeedback.Stop();window.Close();}
    }
}
