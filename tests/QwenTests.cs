using System;
using System.Windows;
using System.Windows.Controls;
namespace EnglishCompanion {
    internal static partial class Tests {
        static void QwenWorkspaceChecks() {
            var profile=QwenWorkspace.Create("ws-contract-test","cn-beijing","qwen-audio-3.1-tts-flash","Betty_v3.1");
            Equal("https://ws-contract-test.cn-beijing.maas.aliyuncs.com/api/v1/services/audio/tts/SpeechSynthesizer",profile.Url,"workspace ID generates exact TTS endpoint");
            Equal("ws-contract-test",QwenWorkspace.Id(profile),"saved workspace ID can be recovered without retyping");
            Equal(true,QwenWorkspace.Ready(profile),"complete workspace is ready");
            Equal(true,QwenWorkspace.Required(ProviderProfiles.Find("千问",true).Default(),"  sk-ws-contract-test  "),"workspace key selects workspace setup");
            Equal(false,QwenWorkspace.Required(ProviderProfiles.Find("千问",true).Default(),"regular-test-key"),"ordinary Qwen default does not require workspace");
            var singapore=QwenWorkspace.Create("ws-contract-test","ap-southeast-1",profile.Model,profile.Voice);
            Equal("ap-southeast-1",QwenWorkspace.Region(singapore),"saved workspace retains region");
            foreach(string id in new[]{"","sk-ws-secret.with.dots","ws-test.evil.example","https://evil.example/","ws-test/path","ws-test?secret=value"}) {
                bool rejected=false;try {QwenWorkspace.Create(id,"cn-beijing",profile.Model,profile.Voice);}catch(InvalidOperationException){rejected=true;}
                Equal(true,rejected,"invalid ID cannot change endpoint authority");
            }
            profile.Url="https://ws-test.cn-beijing.maas.aliyuncs.com/wrong-route";Equal(false,QwenWorkspace.Ready(profile),"wrong speech route is incomplete");
        }
        static void QwenSetupUiChecks() {
            using(var settings=new SettingsWindow(new Configuration(),true)) {
                var owner=Field<Window>(settings,"window");owner.Show();PumpLayout();
                var c=new Configuration();ProviderProfiles.SeedKeys(c);
                var setup=new QwenSpeechOptionsWindow(owner,c,"sk-ws-contract-test");var dialog=Field<Window>(setup,"window");
                owner.Dispatcher.BeginInvoke(new Action(delegate {
                    Equal(1,Field<ComboBox>(setup,"kind").SelectedIndex,"workspace key defaults to Audio setup");
                    Field<TextBox>(setup,"workspace").Text="ws-contract-test";
                    Equal(true,Field<TextBox>(setup,"endpoint").Text.Contains("ws-contract-test.cn-beijing.maas.aliyuncs.com"),"ID fills endpoint in real UI");
                    FindButton((FrameworkElement)dialog.Content,"确认").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                }));
                Equal(true,setup.Show(),"setup confirms without asking for full endpoint");
                Equal("ws-contract-test",QwenWorkspace.Id(c.SpeechProfiles["千问"]),"setup saves chosen workspace into draft");
                Equal("qwen-audio-3.1-tts-flash",c.SpeechProfiles["千问"].Model,"workspace preset selects Audio schema");
                var cancel=new QwenSpeechOptionsWindow(owner,c,"sk-ws-contract-test");
                owner.Dispatcher.BeginInvoke(new Action(delegate {Field<TextBox>(cancel,"workspace").Text="ws-other-test";Field<Window>(cancel,"window").Close();}));
                Equal(false,cancel.Show(),"closing cancels workspace edits");Equal("ws-contract-test",QwenWorkspace.Id(c.SpeechProfiles["千问"]),"cancel preserves previous workspace");
            }
            using(var settings=new SettingsWindow(new Configuration(),true)) {
                var owner=Field<Window>(settings,"window");owner.Show();PumpLayout();
                Field<KeyEntry>(settings,"translationKey").Value="translation-contract-test";
                Field<KeyEntry>(settings,"speechKey").Value="sk-ws-contract-test";
                var confirm=(Button)owner.FindName("Confirm");
                owner.Dispatcher.BeginInvoke(new Action(delegate {
                    owner.Dispatcher.BeginInvoke(new Action(delegate {Equal(1,owner.OwnedWindows.Count,"incomplete workspace opens setup during save");owner.OwnedWindows[0].Close();}));
                    confirm.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                }));PumpLayout();
                Equal(null,settings.Result,"canceling incomplete setup does not save invalid profile");
                Equal("sk-ws-contract-test",Field<KeyEntry>(settings,"speechKey").Value,"cancel retains already entered API key");
                owner.Dispatcher.BeginInvoke(new Action(delegate {
                    owner.Dispatcher.BeginInvoke(new Action(delegate {
                        var setupWindow=owner.OwnedWindows[0];
                        ((TextBox)FindNamed((FrameworkElement)setupWindow.Content,"工作空间 ID")).Text="ws-contract-test";
                        FindButton((FrameworkElement)setupWindow.Content,"确认").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }));
                    confirm.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                }));PumpLayout();
                Equal(true,settings.Result!=null&&QwenWorkspace.Ready(settings.Result.SpeechProfiles["千问"]),"complete setup finishes original save without a second outer confirmation");
                Equal("sk-ws-contract-test",Configuration.Open(settings.Result.SpeechSecret),"completed save retains workspace credential");
            }
        }
        static void SavedSettingsUiChecks() {
            byte[] before=System.IO.File.ReadAllBytes(Configuration.FilePath);var c=Configuration.Load();
            using(var settings=new SettingsWindow(c,true)) {
                var owner=Field<Window>(settings,"window");owner.Show();PumpLayout();
                Equal(true,Field<KeyEntry>(settings,"translationKey").Value.Length>0,"saved translation key appears in real input control");
                Equal(true,Field<KeyEntry>(settings,"speechKey").Value.Length>0,"saved speech key appears in real input control");
                Equal(true,QwenWorkspace.Ready(ProviderProfiles.Profile(c,"千问",true)),"saved Qwen workspace is complete");
                Services.ValidateSpeech(c,Configuration.Open(c.SpeechSecret));
            }
            Equal(true,System.Linq.Enumerable.SequenceEqual(before,System.IO.File.ReadAllBytes(Configuration.FilePath)),"inspection does not modify saved settings");
        }
        static Button FindButton(FrameworkElement root,string text) {
            var button=root as Button;if(button!=null&&Object.Equals(button.Content,text))return button;
            foreach(object child in LogicalTreeHelper.GetChildren(root)){var element=child as FrameworkElement;if(element!=null){var found=FindButton(element,text);if(found!=null)return found;}}
            return null;
        }
        static FrameworkElement FindNamed(FrameworkElement root,string name) {
            if(System.Windows.Automation.AutomationProperties.GetName(root)==name)return root;
            foreach(object child in LogicalTreeHelper.GetChildren(root)){var element=child as FrameworkElement;if(element!=null){var found=FindNamed(element,name);if(found!=null)return found;}}
            return null;
        }
    }
}
