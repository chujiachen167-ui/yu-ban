using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
namespace EnglishCompanion {
    internal sealed class SettingsWindow : IDisposable {
        internal Configuration Result;
        readonly Window window;
        readonly GlassMist mist;
        readonly Configuration draft;
        readonly ComboBox translation, speech, skins;
        // 学哪种语言：决定翻译方向与朗读语言。放在最上面，因为其它设置都跟着它走。
        readonly ComboBox learning;
        static readonly string[] LanguageIds = { "English", "Chinese" };
        static readonly string[] LanguageNames = { "英语", "中文" };
        internal readonly TextBlock languageHint;
        readonly KeyEntry translationKey, speechKey;
        readonly TextBlock status;
        readonly System.Collections.Generic.Dictionary<string,string> savedTranslation, savedSpeech;
        string currentTranslation, currentSpeech;
        bool accepted;
        readonly bool preview;
        internal bool HasSpeechKey { get { return speechKey.Value.Length>0; } }
        internal SettingsWindow(Configuration c, bool preview=false) {
            this.preview=preview;
            draft=Probe.Json.Deserialize<Configuration>(Probe.Json.Serialize(c)); ProviderProfiles.SeedKeys(draft);
            savedTranslation=new System.Collections.Generic.Dictionary<string,string>(draft.TranslationKeys); savedSpeech=new System.Collections.Generic.Dictionary<string,string>(draft.SpeechKeys);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion.Settings.xaml")) window=(Window)XamlReader.Load(stream);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion.Wordmark.path"))
            using(var reader=new System.IO.StreamReader(stream)) Find<System.Windows.Shapes.Path>("BrandWordmark").Data=Geometry.Parse(reader.ReadToEnd());
            WindowMaterial.Attach(Find<Border>("Shell"),24);
            mist=new GlassMist(window,Find<Canvas>("Mist"));
            translation=Find<ComboBox>("TranslationProvider"); speech=Find<ComboBox>("SpeechProvider"); skins=Find<ComboBox>("SkinPicker"); status=Find<TextBlock>("Status");
            learning=Find<ComboBox>("LearningLanguage");languageHint=Find<TextBlock>("LanguageHint");
            // 语言选择必须显式呈现：用户先说自己学什么，其余设置才有着落。
            foreach(string name in LanguageNames)learning.Items.Add(name);
            learning.SelectedItem=draft.Language=="Chinese"?"中文":"英语";
            learning.SelectionChanged+=delegate { draft.Language=LearningId(); Refresh(); };
            learning.MinHeight=40;
            translationKey=new KeyEntry("翻译 API Key"); speechKey=new KeyEntry("语音 API Key"); Find<Grid>("TranslationKeyHost").Children.Add(translationKey); Find<Grid>("SpeechKeyHost").Children.Add(speechKey);
            window.Loaded+=delegate { RefreshCardMaterials(); };
            Find<Border>("TranslationCard").SizeChanged+=delegate { RefreshCardMaterials(); };
            Find<Border>("SpeechCard").SizeChanged+=delegate { RefreshCardMaterials(); };
            currentTranslation=ProviderProfiles.TranslationProvider(draft); currentSpeech=ProviderProfiles.SpeechProvider(draft);
            foreach(var provider in ProviderProfiles.Translation)translation.Items.Add(provider.Name);if(currentTranslation=="原有配置")translation.Items.Add(currentTranslation);
            foreach(var provider in ProviderProfiles.Speech)speech.Items.Add(provider.Name);if(currentSpeech=="原有配置")speech.Items.Add(currentSpeech);speech.Items.Add("系统语音");
            translation.SelectedItem=currentTranslation; speech.SelectedItem=currentSpeech;
            translationKey.Value=ProviderProfiles.Saved(draft.TranslationKeys,currentTranslation); speechKey.Value=ProviderProfiles.Saved(draft.SpeechKeys,currentSpeech);
            translation.SelectionChanged+=delegate { draft.TranslationKeys[currentTranslation]=Configuration.Seal(translationKey.Value); currentTranslation=(string)translation.SelectedItem; translationKey.Value=ProviderProfiles.Saved(draft.TranslationKeys,currentTranslation); translationKey.HideSecret(); Refresh(); };
            speech.SelectionChanged+=delegate { if(currentSpeech!="系统语音") draft.SpeechKeys[currentSpeech]=Configuration.Seal(speechKey.Value); currentSpeech=(string)speech.SelectedItem; speechKey.Value=ProviderProfiles.Saved(draft.SpeechKeys,currentSpeech); speechKey.HideSecret(); Refresh(); };
            translationKey.Changed+=Refresh; speechKey.Changed+=Refresh;
            skins.Items.Add("官方默认"); skins.Items.Add("大肥鱼"); skins.Items.Add("小奶蛙"); skins.SelectedItem=Skin.Get(draft.Theme).Name;
            skins.SelectionChanged+=delegate { draft.Theme=skins.SelectedIndex==1?"ocean":skins.SelectedIndex==2?"baby":"glass"; ApplySkin(); };
            Find<Button>("Confirm").Click+=delegate { Save(); }; Find<Button>("Close").Click+=delegate { window.Close(); }; Find<Button>("Minimize").Click+=delegate { window.WindowState=WindowState.Minimized; };
            Find<RadioButton>("Bilingual").IsChecked=draft.ShowOriginal;
            Find<RadioButton>("TranslationOnly").IsChecked=!draft.ShowOriginal;
            Find<RadioButton>("Bilingual").Checked+=delegate {draft.ShowOriginal=true;};
            Find<RadioButton>("TranslationOnly").Checked+=delegate {draft.ShowOriginal=false;};
            Find<Grid>("TitleBar").MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { if(e.OriginalSource==sender) window.DragMove(); };
            ProviderHelp.Attach(Find<Button>("TranslationInfo"),delegate {return currentTranslation;},false,delegate {new ProviderOptionsWindow(window,draft,currentTranslation,false).Show();});
            ProviderHelp.Attach(Find<Button>("SpeechInfo"),delegate {return currentSpeech;},true,delegate {if(currentSpeech=="千问")new QwenSpeechOptionsWindow(window,draft,speechKey.Value).Show();else new ProviderOptionsWindow(window,draft,currentSpeech,true).Show();Refresh();},delegate {return currentSpeech=="系统语音"?null:ProviderProfiles.Profile(draft,currentSpeech,true);},delegate {return speechKey.Value;});
            Find<Button>("SpeechOptions").Click+=delegate {
                var voiceDraft=Probe.Json.Deserialize<Configuration>(Probe.Json.Serialize(draft));
                ProviderProfiles.Prepare(voiceDraft,currentSpeech,true);
                voiceDraft.SpeechSecret=Configuration.Seal(speechKey.Value.Trim());voiceDraft.ReuseKey=false;
                new SpeechOptionsWindow(window,voiceDraft).Show();
                draft.SpeechStyle=voiceDraft.SpeechStyle;draft.EnglishVoice=voiceDraft.EnglishVoice;
            };
            window.SourceInitialized+=delegate { AppIcon.ApplyTo(window); Find<Image>("BrandIcon").Source=window.Icon; ApplySkin(); };
            window.ContentRendered+=delegate { translationKey.FocusInput(); };
            window.Deactivated+=delegate { translationKey.HideSecret(); speechKey.HideSecret(); };
            ApplySkin(); Refresh();
        }
        T Find<T>(string name) where T:class { return (T)window.FindName(name); }
        string LearningId() { return learning!=null&&learning.SelectedIndex==1?"Chinese":"English"; }
        // 说清这个选择会带来什么差别，而不是让用户自己猜。
        void RefreshLanguageHint() {
            if(languageHint==null)return;
            bool chinese=LearningId()=="Chinese";
            var cap=SpeechProfiles.CapabilityOf(draft);
            string speech=chinese
                ? (cap.Voices?"朗读会用中文音色。":"当前语音服务商未提供中文音色，可在「模型与接口」调整。")
                : (cap.Voices?"朗读会用英语音色。":"当前语音服务商未提供英语音色，可在「模型与接口」调整。");
            languageHint.Text=(chinese?"你写中文或英文，我译成中文。":"你写中文或英文，我译成英文。")+"\n"+speech;
        }
        void ApplySkin() {
            var skin=Skin.Get(draft.Theme); draft.Theme=skin.Id;
            window.Resources["Ink"]=Skin.Brush(skin.Ink); window.Resources["Muted"]=Skin.Brush(skin.Muted); window.Resources["Accent"]=Skin.Brush(skin.Accent); window.Resources["Panel"]=Skin.Brush(skin.Panel); window.Resources["Edge"]=Skin.Brush(skin.Edge);
            window.Resources["ButtonInk"]=Skin.Brush(skin.ButtonInk);window.Resources["SettingsPanel"]=skin.SettingsPanel;
            window.Resources["MenuSurface"]=Skin.Brush(skin.Id=="glass"?"#E7F0FA":skin.Field);
            window.Resources["Field"]=Skin.Brush(skin.Field);window.Resources["FieldEdge"]=Skin.Brush(skin.FieldEdge);
            window.Resources["Hover"]=Skin.Brush(skin.Hover);window.Resources["Focus"]=Skin.Brush(skin.Focus);
            translationKey.ApplySkin(skin);speechKey.ApplySkin(skin);
            bool glass=skin.Id=="glass", baby=skin.Id=="baby";
            Find<Border>("Shell").Background=Skin.Brush(glass?"#40FFFFFF":skin.Surface);
            Find<Border>("BrandPlate").Visibility=glass?Visibility.Visible:Visibility.Collapsed;
            Find<Border>("BrandPlate").Background=Brushes.Transparent;
            Find<Border>("BrandPlate").Padding=new Thickness(0);
            Find<Image>("BrandIcon").Visibility=Visibility.Collapsed;
            mist.SetEnabled(glass);
            Find<Image>("Artwork").Source=Skin.Artwork(skin.Id);
            Find<Border>("BrandPlate").Margin=new Thickness(52,55,0,0);
            Find<StackPanel>("Brand").Orientation=baby?Orientation.Horizontal:Orientation.Vertical;
            Find<TextBlock>("Tagline").Visibility=skin.Id=="ocean"?Visibility.Collapsed:Visibility.Visible;
            Find<TextBlock>("Tagline").VerticalAlignment=VerticalAlignment.Center;
            Find<TextBlock>("Tagline").Margin=baby?new Thickness(18,0,0,0):new Thickness(0,10,0,0);
            Find<TextBlock>("Tagline").FontSize=baby?12:14;
            Find<TextBlock>("Tagline").Foreground=Skin.Brush(skin.Ink);
            Find<Image>("BrandIcon").Width=Find<Image>("BrandIcon").Height=glass?42:32;
            Find<Image>("BrandIcon").Margin=new Thickness(0,0,10,0);
            RefreshCardMaterials();
            Refresh();
        }
        void RefreshCardMaterials() {
            var skin=Skin.Get(draft.Theme);
            ApplyCardMaterial("TranslationCard","TranslationProvider","TranslationKeyHost",skin);
            ApplyCardMaterial("SpeechCard","SpeechProvider","SpeechKeyHost",skin);
        }
        void ApplyCardMaterial(string cardName,string providerName,string keyName,Skin skin) {
            var card=Find<Border>(cardName);
            if(skin.Id!="glass") { card.Background=Skin.Brush(skin.Panel);return; }
            if(card.ActualWidth<=0||card.ActualHeight<=0)return;
            // The milky outer card has real holes. A transparent child alone would
            // still show its parent's tint instead of the continuous liquid below.
            Geometry area=new RectangleGeometry(new Rect(0,0,card.ActualWidth,card.ActualHeight),20,20);
            foreach(string name in new[]{providerName,keyName}) {
                var field=Find<FrameworkElement>(name);
                if(field.ActualWidth<=0||field.ActualHeight<=0)return;
                var point=field.TranslatePoint(new Point(0,0),card);
                area=new CombinedGeometry(GeometryCombineMode.Exclude,area,new RectangleGeometry(new Rect(point.X,point.Y,field.ActualWidth,field.ActualHeight),13,13));
            }
            var milk=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#DAEAF2FC"),(Color)ColorConverter.ConvertFromString("#CDDCEAF8"),90);
            var brush=new DrawingBrush(new GeometryDrawing(milk,null,area)) {
                ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,card.ActualWidth,card.ActualHeight),Stretch=Stretch.Fill
            };
            brush.Freeze();card.Background=brush;
        }
        void SetFeedback(string target,string value,string saved,bool local) {
            var label=Find<TextBlock>(target); bool filled=!String.IsNullOrWhiteSpace(value);
            label.Text=local?"无需 API Key":!filled?"尚未填写":value.Trim()==saved?"✓ 已保存 · 本机加密":"● 已填入 · 待确认";
            var skin=Skin.Get(draft.Theme);
            label.Foreground=Skin.Brush(local||!filled?skin.Muted:value.Trim()==saved?skin.Saved:skin.Pending);
            bool isSaved=!local&&filled&&value.Trim()==saved;
            label.FontWeight=isSaved?FontWeights.SemiBold:FontWeights.Normal;
            label.Effect=isSaved?new System.Windows.Media.Effects.DropShadowEffect {Color=Colors.White,ShadowDepth=0,BlurRadius=3,Opacity=1}:null;
        }
        void Refresh() {
            speechKey.SetEnabled(currentSpeech!="系统语音");
            SetFeedback("TranslationState",translationKey.Value,ProviderProfiles.Saved(savedTranslation,currentTranslation),false);
            SetFeedback("SpeechState",speechKey.Value,ProviderProfiles.Saved(savedSpeech,currentSpeech),currentSpeech=="系统语音");
            if(learning!=null)draft.Language=LearningId();
            RefreshLanguageHint();
        }
        void Save() {
            try {
                // 翻译与语音分开判断：语音没配好不该挡住翻译，用户也不必重填任何东西。
                var speechBlocker="";
                if(currentSpeech=="千问"&&!String.IsNullOrWhiteSpace(speechKey.Value)) {
                    var profile=ProviderProfiles.Profile(draft,currentSpeech,true);
                    if(QwenWorkspace.Required(profile,speechKey.Value)&&!QwenWorkspace.Ready(profile)&&!new QwenSpeechOptionsWindow(window,draft,speechKey.Value).Show()) {
                        status.Text="语音配置尚未完成，已填写内容仍保留；翻译照常可以保存。";status.Visibility=Visibility.Visible;
                        ProviderProfiles.Apply(draft,currentTranslation,translationKey.Value,currentSpeech,speechKey.Value);
                        if(!preview)draft.Save();Result=draft;accepted=true;window.Close();return;
                    }
                }
                speechBlocker=ProviderProfiles.Apply(draft,currentTranslation,translationKey.Value,currentSpeech,speechKey.Value);
                if(!preview)draft.Save();Result=draft;accepted=true;
                if(speechBlocker.Length>0) {
                    // 翻译已经能用，语音的问题单独说明，不要求用户重填或重新保存。
                    status.Text=translationKey.Value.Trim().Length>0
                        ? "翻译已保存。朗读还需：" + speechBlocker + "（点设置旁的 i 可补齐，翻译不受影响）"
                        : "已保存。翻译还需填写翻译 API Key；" + speechBlocker;
                    status.Visibility=Visibility.Visible;window.Focus();
                } else window.Close();
            }
            catch(InvalidOperationException e) {status.Text=e.Message;status.Visibility=Visibility.Visible;}
            catch { status.Text="保存失败，请重试；当前填写内容仍保留。"; status.Visibility=Visibility.Visible; }
        }
        internal void BringForward() { window.Dispatcher.BeginInvoke(new Action(delegate { if(window.WindowState==WindowState.Minimized) window.WindowState=WindowState.Normal; window.Activate(); })); }
        internal System.Windows.Forms.DialogResult ShowDialog() { window.ShowDialog(); return accepted?System.Windows.Forms.DialogResult.OK:System.Windows.Forms.DialogResult.Cancel; }
        public void Dispose() { translationKey.Clear(); speechKey.Clear(); window.Close(); }
    }
}
