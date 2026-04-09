using System.Windows;
using MakriFormas.Services;

namespace MakriFormas
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// Single-window architecture: MainWindow is the only shell.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DatabaseService.Initialize();
            AiSettingsService.EnsureDefaults();
        }

        /// <summary>Returns the running MainWindow instance (if any).</summary>
        public static MainWindow? GetMainWindow()
            => Current?.MainWindow as MainWindow;
    }
}
