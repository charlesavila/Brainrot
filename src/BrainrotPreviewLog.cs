using System;
using System.IO;
using System.Text;
using TaleWorlds.Library;

namespace Brainrot
{
    internal static class BrainrotPreviewLog
    {
        private static readonly object Sync = new object();
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Brainrot", "party-preview.log");

        internal static void Start()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.WriteAllText(LogPath,
                    "Brainrot Stage 6 party preview\r\nModule: " +
                    typeof(BrainrotSubModule).Assembly.Location + "\r\n", Encoding.UTF8);
            }
            catch (Exception exception)
            {
                Debug.Print("Brainrot: preview log unavailable: " + exception.Message);
            }
        }

        internal static void Write(string message)
        {
            // Diagnostic I/O must never interrupt character rendering.
            try
            {
                Debug.Print("Brainrot preview: " + message);
                lock (Sync)
                {
                    File.AppendAllText(LogPath,
                        DateTimeOffset.Now.ToString("o") + " " + message + "\r\n", Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
