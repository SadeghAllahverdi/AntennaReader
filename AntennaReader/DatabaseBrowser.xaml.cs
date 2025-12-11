using AntennaReader.Infrastructure;
using AntennaReader.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace AntennaReader
{
    /// <summary>
    /// Interaction logic for DatabaseBrowser.xaml
    /// </summary>
    public partial class DatabaseBrowser : Window
    {
        #region Constructor
        /// <summary>
        /// Initializes DatabaseBrowser window. Loads antenna diagrams from the SQLite database.
        /// </summary>
        public DatabaseBrowser()
        {
            InitializeComponent();
            LoadDiagrams();
        }
        #endregion

        #region Helper Function (Load Diagrams)
        /// <summary>
        /// Loads antenna diagrams from the SQLite database -> binds to DiagramList
        /// </summary>
        private void LoadDiagrams()
        {
            using (AppDbContext db = new AppDbContext())
            {
                List<AntennaDiagram> diagrams = db.AntennaDiagrams.OrderBy(d => d.Id).ToList(); // list all diagrams
                DiagramList.ItemsSource = diagrams;
            }
        }
        #endregion

        #region Helper Function (Get Selected Diagrams)
        /// <summary>
        /// returns a list of the selected antenna diagrams
        /// </summary>>
        private List<AntennaDiagram> GetSelectedDiagrams()
        {
            return DiagramList.SelectedItems.Cast<AntennaDiagram>().ToList();
        }
        #endregion

        #region Click -> Export CSV
        /// <summary>
        /// exports selected antenna diagrams from the database browser window to CSV file format.
        /// </summary>
        private void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            List<AntennaDiagram> selectedDiagrams = GetSelectedDiagrams(); // get selected diagrams
            // check if list is empty
            if (!selectedDiagrams.Any())
            {
                MessageBox.Show("Please select at least one diagram to export");
                return;
            }
            // set file path
            string filePath = System.IO.Path.Combine(AppPaths.ExportFolder, $"AntennaDiagrams.csv");

            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        //1. header
                        List<string> header = new List<string>();
                        header.Add("Antenna Name");
                        for (int angle = 0; angle < 360; angle += 10)
                        {
                            header.Add($"Angle {angle}");
                        }
                        writer.WriteLine(string.Join(",", header));
                        //2. data rows
                        foreach (AntennaDiagram diagram in selectedDiagrams)
                        {
                            // query database for diagram and its measurements
                            AntennaDiagram result = db.AntennaDiagrams.Include(d => d.Measurements).First(d => d.Id == diagram.Id);
                            // measurements 
                            List<string> row = new List<string> { result.AntennaName ?? string.Empty };
                            for (int angle = 0; angle < 360; angle += 10)
                            {
                                AntennaMeasurement? measurment = result.Measurements.FirstOrDefault(m => m.Angle == angle);
                                double dbValue = measurment?.DbValue ?? 0.0;
                                row.Add($"{dbValue:F1}");
                            }
                            writer.WriteLine(string.Join(",", row));
                        }
                    }
                }
                MessageBox.Show($"Exported {selectedDiagrams.Count} diagram(s) to CSV \n{AppPaths.ExportFolder}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export diagram(s) to CSV: {ex.Message}");
            }
        }
        #endregion

        #region Click -> Export PAT
        /// <summary>
        /// exports selected antenna diagrams from the database browser window to seperate PAT file(s).
        /// </summary>
        private void ExportPAT_Click(object sender, RoutedEventArgs e)
        {
            List<AntennaDiagram> selectedDiagrams = GetSelectedDiagrams();
            // check if list is empty
            if (!selectedDiagrams.Any())
            {
                MessageBox.Show("Please select at least one diagram to export.", "No selection");
                return;
            }

            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    foreach (AntennaDiagram diagram in selectedDiagrams)
                    {
                        AntennaDiagram result = db.AntennaDiagrams.Include(d => d.Measurements).First(d => d.Id == diagram.Id);
                        string filename = (result.AntennaName ?? "Unknown").Replace(" ", "_");// safe filename -> replace space
                        string filePath = System.IO.Path.Combine(AppPaths.ExportFolder, $"{filename}.PAT");
                        // write PAT file
                        using (StreamWriter writer = new StreamWriter(filePath))
                        {
                            writer.WriteLine("'', 0, 2"); // -> I assume these are fixed values (PAT files start with these)
                            for (int angle = 0; angle < 360; angle += 10) // write measurements
                            {
                                AntennaMeasurement? measurment = result.Measurements.FirstOrDefault(m => m.Angle == angle);
                                double dbValue = measurment?.DbValue ?? 0.0;
                                writer.WriteLine($" {angle}, {dbValue:F1}");
                            }
                            writer.WriteLine("999"); // ending for PAT file
                        }
                    }
                }
                MessageBox.Show($"Exported {selectedDiagrams.Count} PAT file(s) to \n{AppPaths.ExportFolder}", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export PAT files: {ex.Message}", "Error");
            }
        }
        #endregion
    }
}
