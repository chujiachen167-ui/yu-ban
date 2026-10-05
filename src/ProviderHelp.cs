using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace EnglishCompanion {
    internal static class ProviderHelp {
        internal const string DeepSeekKey="https://platform.deepseek.com/api_keys";
        internal const string QwenKey="https://platform.qianwenai.com/home/api-keys";
        internal const string QwenBilling="https://platform.qianwenai.com/home/billing/overview";
        internal static void Attach(Button trigger, Func<string> provider, bool speech,Action options=null) {
            var popup=new Popup { PlacementTarget=trigger, Placement=PlacementMode.Bottom, AllowsTransparency=true, StaysOpen=true };
            var body=new StackPanel { Margin=new Thickness(18), Width=340 };
            var border=new Border { Background=Skin.Brush("#FFFDFE"), BorderBrush=Skin.Brush("#D5E0E5"), BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(16), Margin=new Thickness(2,6,2,8), Child=new ScrollViewer {Content=body,MaxHeight=440,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled} };
            popup.Child=border;
            var delay=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(350) };
            delay.Tick+=delegate { delay.Stop(); if(!trigger.IsMouseOver && !border.IsMouseOver) popup.IsOpen=false; };
            Action open=delegate {
                delay.Stop(); body.Children.Clear(); string name=provider();
                body.Children.Add(new TextBlock { Text=name=="系统语音"?"使用系统语音":name+" · 获取 API Key", FontSize=17, FontWeight=FontWeights.SemiBold, Foreground=Skin.Brush("#243B46"), Margin=new Thickness(0,0,0,9) });
                var entry=ProviderProfiles.Find(name,speech);
                string content=name=="系统语音"?"使用 Windows 已安装的音色，不需要 API Key。":name=="DeepSeek"?"默认翻译服务商。登录 DeepSeek 开放平台，在 API Keys 页面创建密钥，复制到这一行；在平台查看余额或充值。":name=="千问"?(speech?"默认语音服务商。登录千问 AI 平台创建 API Key；在账单页查看余额或充值。百炼普通 Key 使用默认接口，工作空间 Key 需在“模型与接口”填写专属地址、模型及音色。":"登录千问 AI 平台创建 API Key。文字与语音额度可能分别计算，请核对模型和地域。"):entry!=null?"登录 "+name+" 官方平台，在 API Keys / 接口密钥页面创建 Key，复制到这一行。余额、免费额度及模型权限以平台账户为准。"+(name=="豆包 / 火山方舟"?"先在方舟开通模型；模型与接口中可填写模型 ID 或接入点 ID。":"")+(name=="ElevenLabs"?"请使用账户可访问的音色 ID，所选音频格式也需符合套餐权限。":""):"继续使用已有服务商的配置。请到服务商官方网站创建 API Key。";
                body.Children.Add(new TextBlock { Text=content, TextWrapping=TextWrapping.Wrap, FontSize=13, LineHeight=21, Foreground=Skin.Brush("#516771"), Margin=new Thickness(0,0,0,10) });
                if(entry!=null){Link(body,"官方平台 / 获取密钥",entry.Portal);Link(body,"接口文档",entry.Docs);}
                if(name=="千问")Link(body,"余额 / 充值",QwenBilling);
                if(name!="系统语音"&&options!=null){var configure=new Button {Content="模型与接口",Padding=new Thickness(10,7,10,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};configure.Click+=delegate {popup.IsOpen=false;options();};body.Children.Add(configure);}
                body.Children.Add(new TextBlock { Text="API Key 是服务访问凭据，请保留在自己的设备上。", TextWrapping=TextWrapping.Wrap, FontSize=11, Foreground=Skin.Brush("#7B8990"), Margin=new Thickness(0,9,0,0) });
                popup.IsOpen=true;
            };
            trigger.MouseEnter+=delegate { open(); }; trigger.Click+=delegate { open(); }; trigger.MouseLeave+=delegate { delay.Start(); };
            border.MouseEnter+=delegate { delay.Stop(); }; border.MouseLeave+=delegate { delay.Start(); };
            trigger.Unloaded+=delegate { delay.Stop(); popup.IsOpen=false; };
        }
        static void Link(Panel parent,string label,string url) {
            var row=new StackPanel { Margin=new Thickness(0,4,0,5) };
            row.Children.Add(new TextBox { Text=url, IsReadOnly=true, BorderThickness=new Thickness(0), Background=Brushes.Transparent, Foreground=Skin.Brush("#246F92"), FontSize=12, Padding=new Thickness(0,4,0,4) });
            var actions=new StackPanel { Orientation=Orientation.Horizontal };
            var visit=new Button { Content=label, Padding=new Thickness(10,6,10,6), Margin=new Thickness(0,0,8,0) };
            var copy=new Button { Content="复制链接", Padding=new Thickness(10,6,10,6) };
            visit.Click+=delegate { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute=true }); } catch { visit.Content="暂时打不开，请复制链接"; } };
            copy.Click+=delegate { try { Clipboard.SetText(url); copy.Content="已复制"; } catch { copy.Content="请选中链接复制"; } };
            actions.Children.Add(visit); actions.Children.Add(copy); row.Children.Add(actions); parent.Children.Add(row);
        }
    }
}
