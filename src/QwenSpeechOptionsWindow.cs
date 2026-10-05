using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
namespace EnglishCompanion {
    internal sealed class QwenSpeechOptionsWindow {
        readonly Window window;
        readonly TextBox workspace=new TextBox(),model=new TextBox(),voice=new TextBox(),endpoint=new TextBox();
        readonly ComboBox kind=new ComboBox(),region=new ComboBox();
        internal QwenSpeechOptionsWindow(Window owner,Configuration c,string key) {
            var skin=Skin.Get(c.Theme);var profile=ProviderProfiles.Profile(c,"千问",true);
            window=new Window {Title="千问 · 语音配置",Owner=owner,Width=570,SizeToContent=SizeToContent.Height,MaxHeight=Math.Min(760,SystemParameters.WorkArea.Height-48),ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=owner.FontFamily,FontSize=14,Foreground=Skin.Brush(skin.Ink),Background=Skin.Brush(skin.Surface),ShowInTaskbar=false};
            var layout=new StackPanel {Margin=new Thickness(24)};
            layout.Children.Add(new TextBlock {Text="千问语音配置",FontSize=21,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)});
            kind.Items.Add("普通千问 TTS");kind.Items.Add("Qwen Audio · 工作空间");kind.SelectedIndex=QwenWorkspace.Required(profile,key)?1:0;
            Field(layout,"接口类型",kind);kind.Padding=new Thickness(10,7,10,7);AutomationProperties.SetName(kind,"千问接口类型");
            var workspaceFields=new StackPanel();layout.Children.Add(workspaceFields);
            Field(workspaceFields,"工作空间 ID",workspace);workspace.Text=QwenWorkspace.Id(profile);
            region.Items.Add("北京");region.Items.Add("新加坡");region.SelectedIndex=QwenWorkspace.Region(profile)=="ap-southeast-1"?1:0;
            Field(workspaceFields,"地域（与 Key 一致）",region);region.Padding=new Thickness(10,7,10,7);AutomationProperties.SetName(region,"工作空间地域");
            workspaceFields.Children.Add(new TextBlock {Text="控制台选择 Key 所属地域，打开右上角的业务空间信息，复制 Workspace ID。程序会自动生成接口地址。",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,0,0,10)});
            var guide=new Button {Content="打开工作空间获取指引",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,0,14)};
            guide.Click+=delegate {try {Process.Start(new ProcessStartInfo(QwenWorkspace.Guide){UseShellExecute=true});}catch {guide.Content="请在语音配置的 i 中复制指引链接";}};workspaceFields.Children.Add(guide);
            var advanced=new StackPanel();Field(advanced,"接口地址",endpoint);endpoint.Text=profile.Url;
            Field(advanced,"模型名称",model);model.Text=profile.Model;Field(advanced,"音色名称 / ID",voice);voice.Text=profile.Voice;
            var details=new Expander {Header="模型与音色",Content=advanced,Margin=new Thickness(0,0,0,12)};layout.Children.Add(details);
            Action update=delegate {
                bool audio=kind.SelectedIndex==1;workspaceFields.Visibility=audio?Visibility.Visible:Visibility.Collapsed;endpoint.IsReadOnly=audio;
                if(audio&&!(model.Text??"").StartsWith("qwen-audio-",StringComparison.OrdinalIgnoreCase)){model.Text="qwen-audio-3.1-tts-flash";voice.Text="Betty_v3.1";}
                if(!audio&&(model.Text??"").StartsWith("qwen-audio-",StringComparison.OrdinalIgnoreCase)){var defaults=ProviderProfiles.Find("千问",true);model.Text=defaults.Model;voice.Text=defaults.Voice;endpoint.Text=defaults.Url;}
                if(audio)endpoint.Text="https://"+(String.IsNullOrWhiteSpace(workspace.Text)?"{WorkspaceId}":workspace.Text.Trim())+"."+(region.SelectedIndex==1?"ap-southeast-1":"cn-beijing")+".maas.aliyuncs.com"+QwenWorkspace.Path;
            };
            kind.SelectionChanged+=delegate {update();};region.SelectionChanged+=delegate {update();};workspace.TextChanged+=delegate {update();};update();
            var status=new TextBlock {Foreground=Skin.Brush("#BA3245"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};layout.Children.Add(status);
            var confirm=new Button {Content="确认",Padding=new Thickness(24,8,24,8),HorizontalAlignment=HorizontalAlignment.Right,IsDefault=true};
            confirm.Click+=delegate {
                try {
                    if(kind.SelectedIndex==0&&(key??"").Trim().StartsWith("sk-ws-",StringComparison.Ordinal))throw new InvalidOperationException("这把 Key 属于工作空间，请选择 Qwen Audio · 工作空间。");
                    var updated=kind.SelectedIndex==1?QwenWorkspace.Create(workspace.Text,region.SelectedIndex==1?"ap-southeast-1":"cn-beijing",model.Text,voice.Text):new ModelProfile {Url=endpoint.Text.Trim(),Model=model.Text.Trim(),Voice=voice.Text.Trim()};
                    ProviderProfiles.ValidateProfile("千问",true,updated);
                    Services.ValidateSpeech(new Configuration {SpeechUrl=updated.Url,SpeechModel=updated.Model},key??"");
                    c.SpeechProfiles["千问"]=updated;window.DialogResult=true;
                }catch(InvalidOperationException e){status.Text=e.Message;}
            };layout.Children.Add(confirm);
            window.Content=new ScrollViewer {Content=layout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        }
        static void Field(Panel parent,string label,Control field) {
            parent.Children.Add(new TextBlock {Text=label,Margin=new Thickness(0,0,0,6)});field.Margin=new Thickness(0,0,0,14);AutomationProperties.SetName(field,label);
            var text=field as TextBox;if(text!=null)text.Padding=new Thickness(10,8,10,8);parent.Children.Add(field);
        }
        internal bool Show(){return window.ShowDialog()==true;}
    }
}
