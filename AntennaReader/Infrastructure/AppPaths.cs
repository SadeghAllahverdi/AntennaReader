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
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AntennaReader"
        );

        // 2. database path
        public static string DBPath => System.IO.Path.Combine(
            BaseFolder, 
            "AntennaReader.db"
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
    }
}
