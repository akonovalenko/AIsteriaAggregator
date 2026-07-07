using System.Windows;

namespace Aisteria
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Carry over data from the pre-rename %APPDATA%\AiAggregator folder (once).
            AppData.EnsureMigrated();
            base.OnStartup(e);
        }
    }
}
