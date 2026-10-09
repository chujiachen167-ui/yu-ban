using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using System.Threading;
using System.Threading.Tasks;
namespace EnglishCompanion {
    internal sealed class SpeechOptionsWindow {
        readonly Window window;
        readonly Configuration config;
        readonly ComboBox style=new ComboBox(), voice=new ComboBox();
        readonly TextBlock status=new TextBlock();
        readonly Button listen=new Button(),apply=new Button();
        readonly AudioActivity activity=new AudioActivity {Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,6,0)};
        readonly TextBlock listenLabel=new TextBlock {Text="试听",VerticalAlignment=VerticalAlignment.Center};
        readonly VoiceProcess player=new VoiceProcess();
        CancellationTokenSource job;
        bool closed;
        internal SpeechOptionsWindow(Window owner,Configuration source) {
            config=Probe.Json.Deserialize<Configuration>(Probe.Json.Serialize(source));
            var skin=Skin.Get(source.Theme);
            // 与主设置页保持同一套外观：圆角、细边框、同一底色，而不是系统默认直角窗口。
            window=new Window {Title="语伴 · 朗读偏好",Owner=owner,Width=540,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,
                WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=owner.FontFamily,FontSize=14,Foreground=Skin.Brush(skin.Ink),
                WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false};
            window.Resources=owner.Resources;
            var shell=new Border {CornerRadius=new CornerRadius(20),BorderThickness=new Thickness(1),
                Background=Skin.Brush(skin.Id=="glass"?"#FAFCFEFF":skin.Surface),BorderBrush=Skin.Brush(skin.Edge)};
            var layout=new Grid {Margin=new Thickness(26)};
            foreach(double h in new[]{44.0,96,100,72,52})layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(h)});
            shell.Child=layout;
            // 无边框窗口要自己负责拖动：整块空白区域按住即可移动。
            shell.MouseLeftButtonDown+=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e) { if(e.OriginalSource==sender) window.DragMove(); };
            layout.Background=Brushes.Transparent;
            // 无边框窗口必须自带关闭入口，否则只能用 Alt+F4。
            var heading=new Grid();
            var title=new TextBlock {Text="朗读偏好",FontSize=23,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};
            var shut=new Button {Content="×",Width=34,Height=34,FontSize=21,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Skin.Brush(skin.Ink),Cursor=System.Windows.Input.Cursors.Hand};
            System.Windows.Automation.AutomationProperties.SetName(shut,"关闭朗读偏好");
            shut.Click+=delegate { window.Close(); };
            heading.Children.Add(title);heading.Children.Add(shut);
            layout.Children.Add(heading);
            var controls=new Grid();controls.ColumnDefinitions.Add(new ColumnDefinition());controls.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(16)});controls.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(controls,1);layout.Children.Add(controls);
            var styles=new StackPanel();styles.Children.Add(new TextBlock {Text="读法",Margin=new Thickness(0,0,0,6)});styles.Children.Add(style);controls.Children.Add(styles);
            var voices=new StackPanel();voices.Children.Add(new TextBlock {Text="英语音色",Margin=new Thickness(0,0,0,6)});voices.Children.Add(voice);Grid.SetColumn(voices,2);controls.Children.Add(voices);
            // 高度与字号统一走 Settings.xaml 的共享样式，这里不再单独覆盖：
            // 之前写死 40，和样式里的 52 打架，文字被挤得上下不居中。
            AutomationProperties.SetName(style,"朗读方式");AutomationProperties.SetName(voice,"英语音色");
            foreach(string name in SpeechProfiles.StyleNames)style.Items.Add(name);
            // 音色来自当前服务商与模型的真实目录；换一家平台，选项跟着变。
            var catalog=SpeechProfiles.Catalog(config);
            foreach(var v in catalog) {
                var item=new ComboBoxItem {Content=v.Note.Length>0 ? v.Name + "　" + v.Note : v.Name, Tag=v.Id, ToolTip=v.Note};
                voice.Items.Add(item);
            }
            var saved=config.EnglishVoice??"";
            voice.SelectedIndex=0;
            for(int i=0;i<catalog.Count;i++) if(catalog[i].Id==saved&&saved.Length>0){voice.SelectedIndex=i;break;}
            style.SelectedIndex=Math.Max(0,Array.IndexOf(SpeechProfiles.StyleIds,config.SpeechStyle));
            // 不支持的能力直接置灰并写明原因，不让用户调了才发现没反应。
            var capability=SpeechProfiles.CapabilityOf(config);
            style.IsEnabled=capability.Styles;
            style.ToolTip=capability.Styles?"切换读法":"当前模型不接受读法指令";
            voice.IsEnabled=capability.Voices;
            if(!capability.Styles&&style.IsEnabled==false)style.Background=Skin.Brush(skin.Field);
            var sample=new TextBlock {Text=SpeechProfiles.Sample,FontFamily=new System.Windows.Media.FontFamily("Segoe UI"),FontSize=16,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};Grid.SetRow(sample,2);layout.Children.Add(sample);
            status.FontSize=12;status.Foreground=Skin.Brush(skin.Muted);status.TextWrapping=TextWrapping.Wrap;status.VerticalAlignment=VerticalAlignment.Center;Grid.SetRow(status,3);layout.Children.Add(status);
            var actions=new Grid();actions.ColumnDefinitions.Add(new ColumnDefinition());actions.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(actions,4);layout.Children.Add(actions);
            var listenContent=new StackPanel {Orientation=Orientation.Horizontal};listenContent.Children.Add(activity);listenContent.Children.Add(listenLabel);listen.Content=listenContent;listen.Width=104;listen.Height=44;listen.FontSize=15;listen.HorizontalAlignment=HorizontalAlignment.Left;listen.BorderThickness=new Thickness(1);listen.Background=Skin.Brush(skin.Hover);actions.Children.Add(listen);
            apply.Content="采用";apply.Width=104;apply.Height=44;apply.FontSize=15;apply.HorizontalAlignment=HorizontalAlignment.Right;apply.Background=Skin.Brush(skin.Accent);apply.Foreground=Skin.Brush(skin.ButtonInk);Grid.SetColumn(apply,1);actions.Children.Add(apply);
            style.SelectionChanged+=delegate {Stop();Refresh();};voice.SelectionChanged+=delegate {Stop();Refresh();};
            listen.Click+=async delegate {await Listen();};
            apply.Click+=delegate {ReadSelection();source.SpeechStyle=config.SpeechStyle;source.EnglishVoice=config.EnglishVoice;window.DialogResult=true;};
            window.Closed+=delegate {closed=true;Stop();};window.Content=shell;Refresh();
        }
        void ReadSelection() {
            config.SpeechStyle=SpeechProfiles.StyleIds[Math.Max(0,style.SelectedIndex)];
            var item=voice.SelectedItem as ComboBoxItem;
            config.EnglishVoice=(item!=null&&item.Tag!=null)?item.Tag as string:"";
        }
        void Refresh() {ReadSelection();bool supported=SpeechProfiles.Supported(config);listen.IsEnabled=apply.IsEnabled=supported;
            var cap=SpeechProfiles.CapabilityOf(config);
            // OpenAI 要求向最终用户披露语音由 AI 生成；放在这里，不占用主设置页。
            string disclosure=ProviderProfiles.SpeechProvider(config)=="OpenAI"?"　朗读语音由 AI 生成。":"";
            if(supported) {
                string cache=System.IO.File.Exists(SpeechProfiles.SamplePath(config))?"已有试听，可直接播放":"首次试听使用语音额度；再次播放不重复生成";
                string line=cache+disclosure;
                // 不支持的能力在下面补一句原因与出路，不占用主设置页，也不堆成长说明。
                if(!cap.Styles)line+="\n"+"读法在当前模型不可用："+cap.Reason;
                else if(cap.ChildVoices)line+="\n含童声（小女孩／小男孩／少年／少女）。";
                status.Text=line;
            } else {
                status.Text=cap.Reason.Length>0?cap.Reason:"当前平台的英语朗读暂无可选音色与朗读方式";
            }
        }
        Task Listen() {
            var previous=SynchronizationContext.Current;
            try {SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(window.Dispatcher));return ListenCore();}
            finally {SynchronizationContext.SetSynchronizationContext(previous);}
        }
        async Task ListenCore() {
            if(job!=null){Stop();Refresh();return;}
            ReadSelection();var current=new CancellationTokenSource();job=current;SetPlayback(PlaybackState.Preparing);apply.IsEnabled=false;status.Text="正在准备试听…";
            try {
                var bytes=await SpeechProfiles.Preview(config,current.Token);current.Token.ThrowIfCancellationRequested();
                if(closed||job!=current)return;status.Text="正在播放";SetPlayback(PlaybackState.Playing);
                await player.Play(new VoiceRequest {Audio=Convert.ToBase64String(bytes)},current.Token);
                if(!closed&&job==current)status.Text="试听结束";
            } catch(OperationCanceledException) { }
            catch(Exception e) {if(!closed&&job==current)status.Text=e is InvalidOperationException?e.Message:"试听暂不可用，请重试";}
            finally {if(job==current){job=null;SetPlayback(PlaybackState.Idle);apply.IsEnabled=SpeechProfiles.Supported(config);}current.Dispose();}
        }
        void SetPlayback(PlaybackState state) {activity.State=state;activity.Visibility=state==PlaybackState.Idle?Visibility.Collapsed:Visibility.Visible;listenLabel.Text=state==PlaybackState.Idle?"试听":state==PlaybackState.Preparing?"准备中":"停止";AutomationProperties.SetName(listen,state==PlaybackState.Idle?"试听":state==PlaybackState.Preparing?"取消试听":"停止试听");}
        void Stop() {var current=job;job=null;if(current!=null)current.Cancel();player.Dispose();SetPlayback(PlaybackState.Idle);}
        internal void Show() {window.ShowDialog();}
    }
}
