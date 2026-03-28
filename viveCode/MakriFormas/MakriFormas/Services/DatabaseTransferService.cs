using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace MakriFormas.Services
{
    public static class DatabaseTransferService
    {
        public static bool ExportDatabase(Window owner)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "SQLite Database (*.db)|*.db|SQLite Database (*.sqlite)|*.sqlite",
                    DefaultExt = ".db",
                    FileName = $"makriformas_backup_{DateTime.Now:yyyyMMdd_HHmm}.db"
                };

                if (dialog.ShowDialog(owner) != true)
                {
                    return false;
                }

                DatabaseService.ExportDatabase(dialog.FileName);
                MessageBox.Show(owner, "Base de datos exportada con éxito.", "Exportar BD", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"No se pudo exportar la base de datos: {ex.Message}", "Exportar BD", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static bool ImportDatabase(Window owner)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "SQLite Database (*.db;*.sqlite)|*.db;*.sqlite|All Files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dialog.ShowDialog(owner) != true)
                {
                    return false;
                }

                var confirm = MessageBox.Show(
                    owner,
                    "Importar una base de datos reemplazará la base actual. ¿Deseas continuar?",
                    "Importar BD",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    return false;
                }

                DatabaseService.ImportDatabase(dialog.FileName);

                var importedName = Path.GetFileName(dialog.FileName);
                MessageBox.Show(owner, $"Base de datos importada desde {importedName}.", "Importar BD", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"No se pudo importar la base de datos: {ex.Message}", "Importar BD", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}