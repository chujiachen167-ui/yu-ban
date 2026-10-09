using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace EnglishCompanion {
    // Material stays behind settings controls and never owns input events.
    internal sealed class GlassMist {
        readonly Canvas canvas;
        readonly Window window;
        readonly Rectangle surface,outer,inner;
        readonly Stopwatch clock=new Stopwatch();
        readonly LiquidSurfaceEffect effect;
        bool enabled,subscribed,disposed;
        double lastFrame=-1;
        internal bool Hardware {get{return effect!=null;}}
        internal bool Running {get{return subscribed;}}
        internal int FrameCount {get;private set;}
        internal GlassMist(Window window,Canvas canvas) {
            this.window=window;this.canvas=canvas;
            // 液面是设计上的半透明材质，但透明度必须保证底下的文字读不清 —— 否则
            // 窗口背后有内容时，界面文字会和背景文字叠在一起。
            surface=new Rectangle {IsHitTestVisible=false,Fill=new LinearGradientBrush(Color.FromArgb(238,220,228,239),Color.FromArgb(246,250,252,255),55)};
            if(RenderCapability.IsPixelShaderVersionSupported(3,0)) {
                try {effect=new LiquidSurfaceEffect();surface.Fill=Brushes.White;surface.Effect=effect;}
                catch(InvalidOperationException) { }
            }
            canvas.Children.Add(surface);
            var rim=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,1)};
            rim.GradientStops.Add(new GradientStop(Colors.White,0));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(130,196,210,235),.40));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(230,255,255,255),.73));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(150,201,214,232),1));rim.Freeze();
            outer=new Rectangle {RadiusX=23,RadiusY=23,Stroke=rim,StrokeThickness=2,IsHitTestVisible=false};
            Canvas.SetLeft(outer,1);Canvas.SetTop(outer,1);canvas.Children.Add(outer);
            inner=new Rectangle {RadiusX=21,RadiusY=21,Stroke=Skin.Brush("#8CFFFFFF"),StrokeThickness=1,IsHitTestVisible=false};
            Canvas.SetLeft(inner,4);Canvas.SetTop(inner,4);canvas.Children.Add(inner);
            canvas.SizeChanged+=Resize;
            window.IsVisibleChanged+=VisibilityChanged;window.StateChanged+=StateChanged;window.Closed+=Closed;
            SystemParameters.StaticPropertyChanged+=SystemChanged;
        }
        void Resize(object sender,SizeChangedEventArgs e) {
            double w=Math.Max(1,canvas.ActualWidth),h=Math.Max(1,canvas.ActualHeight);
            surface.Width=w;surface.Height=h;outer.Width=Math.Max(0,w-2);outer.Height=Math.Max(0,h-2);inner.Width=Math.Max(0,w-8);inner.Height=Math.Max(0,h-8);
            if(effect!=null) {var source=PresentationSource.FromVisual(window);var dpi=source==null?Matrix.Identity:source.CompositionTarget.TransformToDevice;effect.Size=new Point(w*dpi.M11,h*dpi.M22);}
        }
        void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e) {Refresh();}
        void StateChanged(object sender,EventArgs e) {Refresh();}
        void SystemChanged(object sender,PropertyChangedEventArgs e) {if(e.PropertyName=="ClientAreaAnimation")window.Dispatcher.BeginInvoke(new Action(Refresh));}
        void Closed(object sender,EventArgs e) {
            disposed=true;Refresh();SystemParameters.StaticPropertyChanged-=SystemChanged;
            canvas.SizeChanged-=Resize;window.IsVisibleChanged-=VisibilityChanged;window.StateChanged-=StateChanged;window.Closed-=Closed;
        }
        internal void SetEnabled(bool value) {enabled=value;canvas.Visibility=value?Visibility.Visible:Visibility.Collapsed;Refresh();}
        void Refresh() {
            bool active=!disposed&&enabled&&Hardware&&window.IsVisible&&window.WindowState!=WindowState.Minimized&&SystemParameters.ClientAreaAnimation;
            if(active==subscribed)return;
            subscribed=active;
            if(active){clock.Start();lastFrame=-1;CompositionTarget.Rendering+=Render;}
            else {CompositionTarget.Rendering-=Render;clock.Stop();}
        }
        void Render(object sender,EventArgs e) {
            double seconds=clock.Elapsed.TotalSeconds;
            if(lastFrame>=0&&seconds-lastFrame<1.0/30)return;
            lastFrame=seconds;effect.Time=seconds;FrameCount++;
        }
    }
}
