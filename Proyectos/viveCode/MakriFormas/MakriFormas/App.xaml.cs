using System.Threading.Tasks;
using System.Windows;
using MakriFormas.Services;

namespace MakriFormas
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public InventoryWindow? InventoryWindowInstance { get; private set; }
        public NewProformaWindow? ProformaWindowInstance { get; private set; }
        public ProformaHistoryWindow? ProformaHistoryWindowInstance { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DatabaseService.Initialize();

            // Iniciar Ollama en segundo plano — no bloquea la UI
            _ = Task.Run(() => OllamaLauncher.EnsureRunningAsync());
        }

        public void ShowOrActivateInventory(Window? owner = null)
        {
            _ = owner;

            if (InventoryWindowInstance is { IsLoaded: true })
            {
                ActivateWindow(InventoryWindowInstance);
                return;
            }

            InventoryWindowInstance = new InventoryWindow();
            if (Application.Current.MainWindow != null && Application.Current.MainWindow != InventoryWindowInstance)
            {
                InventoryWindowInstance.Owner = Application.Current.MainWindow;
            }

            InventoryWindowInstance.Closed += (_, _) => InventoryWindowInstance = null;
            InventoryWindowInstance.Show();
            ActivateWindow(InventoryWindowInstance);
        }

        public void ShowOrActivateProforma(Window? owner = null)
        {
            _ = owner;

            if (ProformaWindowInstance is { IsLoaded: true })
            {
                ActivateWindow(ProformaWindowInstance);
                return;
            }

            ProformaWindowInstance = new NewProformaWindow();
            if (Application.Current.MainWindow != null && Application.Current.MainWindow != ProformaWindowInstance)
            {
                ProformaWindowInstance.Owner = Application.Current.MainWindow;
            }

            ProformaWindowInstance.Closed += (_, _) => ProformaWindowInstance = null;
            ProformaWindowInstance.Show();
            ActivateWindow(ProformaWindowInstance);
        }

        public void ShowOrActivateProformaHistory(Window? owner = null)
        {
            _ = owner;

            if (ProformaHistoryWindowInstance is { IsLoaded: true })
            {
                ActivateWindow(ProformaHistoryWindowInstance);
                return;
            }

            ProformaHistoryWindowInstance = new ProformaHistoryWindow();
            if (Application.Current.MainWindow != null && Application.Current.MainWindow != ProformaHistoryWindowInstance)
            {
                ProformaHistoryWindowInstance.Owner = Application.Current.MainWindow;
            }

            ProformaHistoryWindowInstance.Closed += (_, _) => ProformaHistoryWindowInstance = null;
            ProformaHistoryWindowInstance.Show();
            ActivateWindow(ProformaHistoryWindowInstance);
        }

        private static void ActivateWindow(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            window.Focus();
        }
    }

}
