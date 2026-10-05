using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace EnglishCompanion {
    internal sealed class LiquidSurfaceEffect : ShaderEffect {
        internal static readonly DependencyProperty TimeProperty=DependencyProperty.Register("Time",typeof(double),typeof(LiquidSurfaceEffect),new UIPropertyMetadata(0.0,PixelShaderConstantCallback(0)));
        internal static readonly DependencyProperty SizeProperty=DependencyProperty.Register("Size",typeof(Point),typeof(LiquidSurfaceEffect),new UIPropertyMetadata(new Point(820,590),PixelShaderConstantCallback(1)));
        internal double Time {get{return (double)GetValue(TimeProperty);}set{SetValue(TimeProperty,value);}}
        internal Point Size {get{return (Point)GetValue(SizeProperty);}set{SetValue(SizeProperty,value);}}
        internal LiquidSurfaceEffect() {
            var shader=new PixelShader();
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion.LiquidSurface.ps"))shader.SetStreamSource(stream);
            shader.Freeze();PixelShader=shader;UpdateShaderValue(TimeProperty);UpdateShaderValue(SizeProperty);
        }
    }
}
