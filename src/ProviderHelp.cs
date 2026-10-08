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
        internal static void Attach(Button trigger, Func<string> provider, bool speech,Action options=null,Func<ModelProfile> profile=null,Func<string> key=null) {
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
                // 入口放在最上面：先看到能点的按钮，再看说明链接。
                if(name!="系统语音"&&options!=null) {
                    var configure=new Button {Content=name=="千问"&&speech?"配置千问语音":"模型与接口",Padding=new Thickness(12,8,12,8),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,12),FontSize=14};
                    configure.Click+=delegate { popup.IsOpen=false;options();};
                    body.Children.Add(configure);
                }
                string content=name=="系统语音"?"使用 Windows 已安装的音色，不需要 API Key。":name=="DeepSeek"?"默认翻译服务商。登录 DeepSeek 开放平台，在 API Keys 页面创建密钥，复制到这一行；在平台查看余额或充值。":name=="千问"?(speech?"默认语音服务商。登录千问 AI 平台创建 API Key；在账单页查看余额或充值。Qwen Audio 使用工作空间，普通 Qwen3-TTS 使用通用接口。":"登录千问 AI 平台创建 API Key。文字与语音额度可能分别计算，请核对模型和地域。"):entry!=null?"登录 "+name+" 官方平台，在 API Keys / 接口密钥页面创建 Key，复制到这一行。余额、免费额度及模型权限以平台账户为准。"+(name=="豆包 / 火山方舟"?"先在方舟开通模型；模型与接口中可填写模型 ID 或接入点 ID。":"")+(name=="ElevenLabs"?"请使用账户可访问的音色 ID，所选音频格式也需符合套餐权限。":""):"继续使用已有服务商的配置。请到服务商官方网站创建 API Key。";
                body.Children.Add(new TextBlock { Text=content, TextWrapping=TextWrapping.Wrap, FontSize=13, LineHeight=21, Foreground=Skin.Brush("#516771"), Margin=new Thickness(0,0,0,10) });
                if(name=="千问"&&speech) {
                    var current=profile==null?null:profile();bool required=QwenWorkspace.Required(current,key==null?"":key());
                    string setup=current!=null&&QwenWorkspace.Ready(current)?"✓ 工作空间已配置，换程序位置不用重新填写。":required?"这份语音配置需要工作空间 ID；点击下面“配置千问语音”填写即可。":"普通 Qwen3-TTS 通常无需填写工作空间 ID；选择 Qwen Audio 或使用 sk-ws- 开头的 Key 时需要。";
                    body.Children.Add(new TextBlock {Text=setup,TextWrapping=TextWrapping.Wrap,FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Skin.Brush("#243B46"),Margin=new Thickness(0,0,0,10)});
                    body.Children.Add(new TextBlock {Text="工作空间 ID 怎么找\n1. 打开百炼控制台，选择 Key 所属地域。\n2. 打开右上角的业务空间信息，复制 Workspace ID；管理员也可在业务空间管理的 ID 列复制。\n3. 在语伴填写 ID，接口地址由程序生成。\n\nID 不是 API Key，也不是 APP ID。赠送额度对应的模型名称和音色，可在“模型与音色”调整。",TextWrapping=TextWrapping.Wrap,FontSize=13,LineHeight=21,Foreground=Skin.Brush("#516771"),Margin=new Thickness(0,0,0,10)});
                    Link(body,"打开百炼控制台",QwenWorkspace.Console);Link(body,"工作空间 ID 获取指引",QwenWorkspace.Guide);Link(body,"Qwen Audio 接口说明",QwenWorkspace.AudioGuide);
                }
                if(entry!=null){Link(body,"官方平台 / 获取密钥",entry.Portal);Link(body,"接口文档",entry.Docs);}
                if(name=="千问")Link(body,"余额 / 充值",QwenBilling);
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
