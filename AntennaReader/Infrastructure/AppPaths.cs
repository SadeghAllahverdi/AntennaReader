using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntennaReader.Infrastructure
{
    /// <summary>
    /// Organizes and defines all file/folder paths for the application
    /// </summary>
    public static class AppPaths
    {
        public static string BaseFolder => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AntennaReader"
        );
        public static string DBPath => System.IO.Path.Combine(
            BaseFolder, 
            "antenna.db"
        );
        public static string SeedDBPath => System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Data",
            "antenna.db"
        );
        public static string ImageFolder => System.IO.Path.Combine(
            BaseFolder,
            "Images"
        );
        public static string ExportFolder => System.IO.Path.Combine(
            BaseFolder,
            "Exports"
        );

        public static void EnsureFolderExists ()
        {
            if ( !Directory.Exists( BaseFolder ) )
            {
                Directory.CreateDirectory( BaseFolder );
            }
            if ( !Directory.Exists( ImageFolder ) )
            {
                Directory.CreateDirectory( ImageFolder );
            }
            if ( !Directory.Exists( ExportFolder ) )
            {
                Directory.CreateDirectory( ExportFolder );
            }
        }  
    }
}
