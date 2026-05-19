using AntennaReader.Infrastructure;
using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.EntityFrameworkCore;


namespace AntennaReader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                AppPaths.EnsureFolderExists();
                using (AppDbContext db = new AppDbContext())
                {
                    db.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messageBoxText:$"Failed to initialize database: {ex.Message}\n\nDatabase location: {AppPaths.DBPath}",
                    caption:"Database Error",
                    button:MessageBoxButton.OK,
                    icon:MessageBoxImage.Error);
            }
        }
    }
}