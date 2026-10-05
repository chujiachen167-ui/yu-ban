using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
namespace EnglishCompanion {
    internal sealed class ProviderOptionsWindow {
        readonly Window window;
        readonly TextBox endpoint=new TextBox(),model=new TextBox(),voice=new TextBox();
        internal ProviderOptionsWindow(Window owner,Configuration c,string provider,bool speech) {
            var skin=Skin.Get(c.Theme);var profile=ProviderProfiles.Profile(c,provider,speech);
            window=new Window {Title=provider+" · 模型与接口",Owner=owner,Width=590,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=owner.FontFamily,FontSize=14,Foreground=Skin.Brush(skin.Ink),Background=Skin.Brush(skin.Surface),ShowInTaskbar=false};
            var layout=new StackPanel {Margin=new Thickness(24)};
            layout.Children.Add(new TextBlock {Text=provider+" · 模型与接口",FontSize=21,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)});
            Field(layout,"接口地址",endpoint,profile.Url);Field(layout,"模型名称",model,profile.Model);
            if(speech)Field(layout,"音色名称 / ID",voice,profile.Voice);
            var status=new TextBlock {Foreground=Skin.Brush("#BA3245"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6)};layout.Children.Add(status);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            var defaults=new Button {Content="恢复预设",Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,12,0),IsEnabled=ProviderProfiles.Find(provider,speech)!=null};
            defaults.Click+=delegate {var p=ProviderProfiles.Find(provider,speech);endpoint.Text=p.Url;model.Text=p.Model;voice.Text=p.Voice;};
            var confirm=new Button {Content="确认",Padding=new Thickness(24,8,24,8),IsDefault=true};
            confirm.Click+=delegate {
                try {var updated=new ModelProfile {Url=endpoint.Text.Trim(),Model=model.Text.Trim(),Voice=voice.Text.Trim()};ProviderProfiles.ValidateProfile(provider,speech,updated);(speech?c.SpeechProfiles:c.TranslationProfiles)[provider]=updated;window.DialogResult=true;}
                catch(InvalidOperationException e){status.Text=e.Message;}
            };
            actions.Children.Add(defaults);actions.Children.Add(confirm);layout.Children.Add(actions);window.Content=layout;
        }
        static void Field(Panel parent,string label,TextBox box,string value){parent.Children.Add(new TextBlock {Text=label,Margin=new Thickness(0,0,0,6)});box.Text=value??"";box.Padding=new Thickness(10,8,10,8);box.Margin=new Thickness(0,0,0,14);AutomationProperties.SetName(box,label);parent.Children.Add(box);}
        internal void Show(){window.ShowDialog();}
    }
}
