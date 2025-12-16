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
        // attributes
        // 1. base folder path
        public static string BaseFolder => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AntennaReader"
        );

        // 2. database path
        public static string DBPath => System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "antenna.db"
        );
        public static string SeedDBPath => System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Data",
            "antenna.db"
        );
        // 3. image folder
        public static string ImageFolder => System.IO.Path.Combine(
            BaseFolder,
            "Images"
        );
        // 4. export folder
        public static string ExportFolder => System.IO.Path.Combine(
            BaseFolder,
            "Exports"
        );

        public static void EnsureFolderExists ()
        {
            // if no base folder -> create one
            if ( !Directory.Exists( BaseFolder ) )
            {
                Directory.CreateDirectory( BaseFolder );
            }
            // if no Image folder -> create one
            if ( !Directory.Exists( ImageFolder ) )
            {
                Directory.CreateDirectory( ImageFolder );
            }
            // of no Export folder -> create one
            if ( !Directory.Exists( ExportFolder ) )
            {
                Directory.CreateDirectory( ExportFolder );
            }
        }

        public static void EnsureDBExists ()
        {
            EnsureFolderExists();
            if (File.Exists(DBPath))
            {
                return;
            }

            if (!File.Exists(SeedDBPath))
            { 
                throw new FileNotFoundException("Seed database not found.", SeedDBPath);
            }

            File.Copy(SeedDBPath, DBPath);
        }   
    }
}
