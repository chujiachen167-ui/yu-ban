using System.Drawing;
using System.Reflection;
namespace EnglishCompanion {
    internal static class AppIcon {
        internal static readonly Icon Value=Create();
        static Icon Create() {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishCompanion.Icon.ico"))
            using(var icon=new Icon(stream,64,64)) return (Icon)icon.Clone();
        }
        internal static void ApplyTo(System.Windows.Window window) {
            var image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(Value.Handle,System.Windows.Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            image.Freeze(); window.Icon=image;
        }
    }
}
