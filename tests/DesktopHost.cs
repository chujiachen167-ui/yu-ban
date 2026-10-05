using System;
using System.IO;
namespace EnglishCompanion {
    // Optional interactive-desktop launcher. No APIs are called; fixed cached
    // speech is required for --playback-check and that check refuses a cache miss.
    internal static class DesktopTestHost {
        [STAThread] static int Main() {
            string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","artifacts");Directory.CreateDirectory(directory);
            using(var output=new StreamWriter(Path.Combine(directory,"desktop-check.txt"),false,System.Text.Encoding.UTF8)) {
                Console.SetOut(output);Console.SetError(output);
                int code=Tests.Main(new[]{"--desktop-check","--panel-check","--skin-check","--pet-check","--liquid-check","--playback-check"});
                output.WriteLine("DesktopExitCode="+code);return code;
            }
        }
    }
}
