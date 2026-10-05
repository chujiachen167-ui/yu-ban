using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
namespace EnglishCompanion {
    internal sealed class Skin {
        internal string Id, Name, Accent, Ink, Muted, Surface, Edge, Panel;
        internal string ButtonInk { get { return Id=="baby"?"#453B25":"#FFFFFF"; } }
        internal string Field { get { return Id=="baby"?"#FFFEF8":Id=="glass"?"#00FFFFFF":"#FBFDFF"; } }
        internal Brush SettingsPanel { get { return Id=="glass"?Brushes.Transparent:Brush(Panel); } }
        internal string FieldEdge { get { return Id=="baby"?"#9D8C59":Id=="glass"?"#294566":"#748CA8"; } }
        internal string Hover { get { return Id=="baby"?"#FFF0B5":Id=="ocean"?"#DFEDFF":"#E5EFFF"; } }
        internal string Focus { get { return Id=="baby"?"#8A6412":Accent; } }
        internal string Saved { get { return "#00AD55"; } }
        internal string Pending { get { return Id=="baby"?"#805609":Id=="glass"?"#412608":"#875414"; } }
        internal static Skin Get(string id) {
            if (id == "ocean") return new Skin { Id=id, Name="大肥鱼", Accent="#2866BE", Ink="#182E54", Muted="#536A89", Surface="#EEF7FF", Edge="#B6D8FA", Panel="#FAFCFF" };
            if (id == "baby") return new Skin { Id=id, Name="小奶蛙", Accent="#FFDC52", Ink="#453B25", Muted="#6F644B", Surface="#FFF9DF", Edge="#E9D58A", Panel="#FFFEF5" };
            return new Skin { Id="glass", Name="官方默认", Accent="#245ECB", Ink="#172B4D", Muted="#152C45", Surface="#F5F9FF", Edge="#C3D5EB", Panel="#F8FBFF" };
        }
        internal static Brush Brush(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
        internal static ImageSource Artwork(string id) {
            if (id == "glass") return null;
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion." + id + ".png")) {
                if (stream == null) return null;
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.StreamSource=stream; image.EndInit(); image.Freeze(); return image;
            }
        }
    }
    internal static class WindowMaterial {
        // Per-pixel alpha: one rounded silhouette, no second native frame or acrylic layer.
        internal static void Attach(FrameworkElement root,double radius) {
            Action clip=delegate { root.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,root.ActualWidth),Math.Max(0,root.ActualHeight)),radius,radius); };
            root.SizeChanged+=delegate {clip();}; root.Loaded+=delegate {clip();};
        }
    }
}
