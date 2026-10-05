using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace EnglishCompanion {
    // Two atlas poses plus gentle transform motion. Decoration cannot intercept selection or buttons.
    internal sealed class PetAdornment : Canvas,IDisposable {
        static BitmapSource atlas;
        readonly Window owner;
        readonly Image open=new Image {Width=66,Height=66},shut=new Image {Width=66,Height=66};
        readonly Grid character=new Grid {Width=66,Height=66,RenderTransformOrigin=new Point(.5,.8)};
        readonly ScaleTransform breath=new ScaleTransform();
        readonly TranslateTransform floaty=new TranslateTransform();
        readonly RotateTransform sway=new RotateTransform();
        readonly Shape[] particles=new Shape[3];
        readonly Border trim;
        readonly Stopwatch clock=new Stopwatch();
        readonly GradientStop glint=new GradientStop();
        bool enabled,subscribed,disposed,paused;
        double lastFrame=-1;
        internal int FrameCount {get;private set;}
        internal int BlinkCount {get;private set;}
        internal bool Running {get{return subscribed;}}
        bool wasBlink;
        internal PetAdornment(Window owner,Border trim) {
            this.owner=owner;this.trim=trim;Width=68;Height=72;IsHitTestVisible=false;
            HorizontalAlignment=HorizontalAlignment.Left;VerticalAlignment=VerticalAlignment.Top;
            var transforms=new TransformGroup();transforms.Children.Add(breath);transforms.Children.Add(sway);transforms.Children.Add(floaty);character.RenderTransform=transforms;
            character.Children.Add(open);character.Children.Add(shut);Children.Add(character);shut.Opacity=0;
            Canvas.SetTop(character,3);
            for(int i=0;i<particles.Length;i++) {particles[i]=new Ellipse {Width=3+i,Height=3+i,StrokeThickness=1,Opacity=.5};Children.Add(particles[i]);}
            owner.IsVisibleChanged+=VisibilityChanged;owner.StateChanged+=StateChanged;owner.Closed+=Closed;
            SystemParameters.StaticPropertyChanged+=SystemChanged;
        }
        static ImageSource Frame(int x,int y) {
            if(atlas==null)using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion.PetAtlas.png")) {
                var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=stream;b.EndInit();b.Freeze();atlas=b;
            }
            int w=atlas.PixelWidth/2,h=atlas.PixelHeight/2;var crop=new CroppedBitmap(atlas,new Int32Rect(x*w,y*h,w,h));crop.Freeze();return crop;
        }
        internal void ApplySkin(Skin s) {
            enabled=s.Id!="glass";Visibility=enabled?Visibility.Visible:Visibility.Collapsed;trim.Visibility=Visibility;
            if(enabled) {
                open.Source=Frame(0,s.Id=="baby"?1:0);shut.Source=Frame(1,s.Id=="baby"?1:0);
                var color=(Color)ColorConverter.ConvertFromString(s.Id=="baby"?"#B4BC52":"#77B9ED");
                glint.Color=color;
                trim.Background=new LinearGradientBrush(new GradientStopCollection {new GradientStop(Color.FromArgb(0,color.R,color.G,color.B),0),glint,new GradientStop(Color.FromArgb(0,color.R,color.G,color.B),1)},new Point(0,0),new Point(1,0));
                foreach(var p in particles){p.Stroke=new SolidColorBrush(color);p.Fill=s.Id=="baby"?new SolidColorBrush(color):Brushes.Transparent;}
            }
            ResetPose();Refresh();
        }
        internal void Pause(bool value) {paused=value;Refresh();}
        void ResetPose() {breath.ScaleX=breath.ScaleY=1;sway.Angle=0;floaty.Y=0;open.Opacity=1;shut.Opacity=0;glint.Offset=.5;trim.Opacity=.6;wasBlink=false;}
        void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e){Refresh();}
        void StateChanged(object sender,EventArgs e){Refresh();}
        void SystemChanged(object sender,PropertyChangedEventArgs e){if(e.PropertyName=="ClientAreaAnimation")owner.Dispatcher.BeginInvoke(new Action(Refresh));}
        void Closed(object sender,EventArgs e){Dispose();}
        void Refresh() {
            bool active=!disposed&&enabled&&!paused&&owner.IsVisible&&owner.WindowState!=WindowState.Minimized&&SystemParameters.ClientAreaAnimation;
            if(active==subscribed)return;subscribed=active;
            if(active){clock.Start();lastFrame=-1;CompositionTarget.Rendering+=Render;}
            else {CompositionTarget.Rendering-=Render;clock.Stop();ResetPose();}
        }
        void Render(object sender,EventArgs e) {
            double t=clock.Elapsed.TotalSeconds;if(lastFrame>=0&&t-lastFrame<1.0/30)return;lastFrame=t;FrameCount++;
            double wave=Math.Sin(t*1.8);breath.ScaleX=1+.012*wave;breath.ScaleY=1-.016*wave;floaty.Y=-1.5*wave;sway.Angle=.8*Math.Sin(t*.9);
            double blinkPhase=t%4.3;
            double blink=blinkPhase<.08?1-blinkPhase/.08:blinkPhase>4.14?Math.Min(1,(blinkPhase-4.14)/.06):0;
            shut.Opacity=blink;open.Opacity=1-blink;
            if(blink>.5&&!wasBlink)BlinkCount++;wasBlink=blink>.5;
            for(int i=0;i<particles.Length;i++){double phase=(t*.15+i*.31)%1;Canvas.SetLeft(particles[i],i==1?60:4+i*3+Math.Sin(t+i)*2);Canvas.SetTop(particles[i],57-phase*45);particles[i].Opacity=.48*Math.Sin(phase*Math.PI);}
            glint.Offset=.5+.28*Math.Sin(t*.65);trim.Opacity=.55+.18*Math.Sin(t*.9);
        }
        public void Dispose(){if(disposed)return;disposed=true;Refresh();SystemParameters.StaticPropertyChanged-=SystemChanged;owner.IsVisibleChanged-=VisibilityChanged;owner.StateChanged-=StateChanged;owner.Closed-=Closed;}
    }
}
