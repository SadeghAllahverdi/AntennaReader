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
                using (var db = new AppDbContext())
                {   
                    // I added these 2 lines for debugging 
                    var conn = db.Database.GetDbConnection();
                    MessageBox.Show($"DataBase Path = {AppPaths.DBPath}\nConnection Source = {conn.DataSource}", "Database Check", MessageBoxButton.OK, MessageBoxImage.Information);

                    db.Database.EnsureCreated();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize data base: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
