using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Rectangle=System.Drawing.Rectangle;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace EnglishCompanion {
    internal sealed class OverlayText {
        internal readonly TextBlock Control; internal Action Changed; string text="";
        internal OverlayText(TextBlock value) {Control=value;}
        internal string Text {get {return text;} set {text=value??"";Control.Text=text;if(Changed!=null)Changed();}}
    }
    internal sealed class OverlayButton {
        internal readonly Button Control; readonly TextBlock label,icon;
        readonly string idleText;
        AudioActivity activity;
        internal OverlayButton(string text,string glyph) {
            idleText=text;
            var content=new StackPanel {Orientation=Orientation.Horizontal};
            icon=new TextBlock {Text=glyph,FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=14,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,text.Length==0?0:6,0)};content.Children.Add(icon);
            label=new TextBlock {Text=text,FontSize=12,VerticalAlignment=VerticalAlignment.Center}; content.Children.Add(label);
            Control=new Button {Content=content,Padding=new Thickness(11,12,11,12),BorderThickness=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand,Background=Brushes.Transparent,Focusable=false};
            Control.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border x:Name='B' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.35'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");Name(text);
        }
        internal void Name(string name) {Control.ToolTip=name;System.Windows.Automation.AutomationProperties.SetName(Control,name);}
        internal string Text {get {return label.Text;} set {label.Text=value;Name(value);}}
        internal string Glyph {set {icon.Text=value;}}
        internal bool Enabled {get {return Control.IsEnabled;} set {Control.IsEnabled=value;}}
        internal void SetPlayback(PlaybackState state) {
            if(activity==null) {activity=new AudioActivity {Margin=icon.Margin};((StackPanel)Control.Content).Children.Insert(0,activity);}
            bool idle=state==PlaybackState.Idle;
            icon.Visibility=idle?Visibility.Visible:Visibility.Collapsed;
            activity.Visibility=idle?Visibility.Collapsed:Visibility.Visible;activity.State=state;
            Text=idle?idleText:state==PlaybackState.Preparing?"准备中":"停止";
            if(!idle)Name(state==PlaybackState.Preparing?"取消朗读":"停止朗读");
        }
        internal event EventHandler Click {add {Control.Click+=value.Invoke;} remove {Control.Click-=value.Invoke;}}
    }
    internal sealed class Hotkeys : Forms.NativeWindow,IDisposable {
        internal event Action<int> Pressed;internal bool AllRegistered=true;
        internal Hotkeys(){CreateHandle(new Forms.CreateParams());for(int i=0;i<3;i++)AllRegistered&=Native.RegisterHotKey(Handle,101+i,0x4000|2|4,(uint)(Forms.Keys.F7+i));}
        protected override void WndProc(ref Forms.Message m){if(m.Msg==0x312&&Pressed!=null)Pressed(m.WParam.ToInt32());base.WndProc(ref m);}
        public void Dispose(){for(int i=101;i<=103;i++)Native.UnregisterHotKey(Handle,i);DestroyHandle();}
    }
}
