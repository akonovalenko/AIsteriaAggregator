using System;
using System.IO;

namespace Aisteria
{
    /// <summary>
    /// Per-user data folder (%APPDATA%\Aisteria). On first run after the rename it copies
    /// existing data from the old %APPDATA%\AiAggregator folder so API keys, history,
    /// sessions and templates carry over.
    /// </summary>
    internal static class AppData
    {
        private static string Root => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string Dir  => Path.Combine(Root, "Aisteria");

        public static void EnsureMigrated()
        {
            try
            {
                string oldDir = Path.Combine(Root, "AiAggregator");
                if (!Directory.Exists(oldDir) || Directory.Exists(Dir)) return; // nothing to do

                Directory.CreateDirectory(Dir);
                foreach (var file in Directory.GetFiles(oldDir))
                    File.Copy(file, Path.Combine(Dir, Path.GetFileName(file)), overwrite: false);
            }
            catch { /* migration is best-effort; never block startup */ }
        }
    }
}
