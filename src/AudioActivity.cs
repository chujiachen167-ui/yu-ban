using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
namespace EnglishCompanion {
    internal enum PlaybackState { Idle, Preparing, Playing }
    // A playback activity indicator, not a measurement of the audio waveform.
    internal sealed class AudioActivity : FrameworkElement {
        internal static readonly DependencyProperty InkProperty=DependencyProperty.Register("Ink",typeof(Brush),typeof(AudioActivity),new FrameworkPropertyMetadata(Brushes.Black,FrameworkPropertyMetadataOptions.AffectsRender));
        readonly DispatcherTimer timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(40)};
        readonly Stopwatch clock=new Stopwatch();
        PlaybackState state;
        internal bool Running {get {return timer.IsEnabled;}}
        internal int FrameCount {get;private set;}
        internal PlaybackState State {get {return state;} set {if(state==value)return;state=value;clock.Restart();Refresh();InvalidateVisual();}}
        internal AudioActivity() {
            Width=18;Height=18;IsHitTestVisible=false;VerticalAlignment=VerticalAlignment.Center;
            SetBinding(InkProperty,new Binding("Foreground") {RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Button),1)});
            Loaded+=delegate {Refresh();};Unloaded+=delegate {timer.Stop();clock.Stop();};IsVisibleChanged+=delegate {Refresh();};
            timer.Tick+=delegate {if(!SystemParameters.ClientAreaAnimation){timer.Stop();InvalidateVisual();return;}FrameCount++;InvalidateVisual();};
        }
        void Refresh() {bool active=state!=PlaybackState.Idle&&IsLoaded&&IsVisible&&SystemParameters.ClientAreaAnimation;if(active){clock.Start();timer.Start();}else {timer.Stop();clock.Stop();}}
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);if(state==PlaybackState.Idle)return;
            var ink=(Brush)GetValue(InkProperty);double t=SystemParameters.ClientAreaAnimation?clock.Elapsed.TotalSeconds:0;
            if(state==PlaybackState.Preparing) {
                for(int i=0;i<3;i++){dc.PushOpacity(.3+.7*(.5+.5*Math.Sin(t*5-i*1.2)));dc.DrawEllipse(ink,null,new Point(3+i*6,9),1.6,1.6);dc.Pop();}
            } else {
                for(int i=0;i<4;i++) {
                    double height=4+(5+i*2)*(.35+.65*(.5+.5*Math.Sin(t*7-i*1.45)));
                    dc.DrawRoundedRectangle(ink,null,new Rect(i*5,17-height,3,height),1.5,1.5);
                }
            }
        }
    }
}
