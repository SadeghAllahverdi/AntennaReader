using AntennaReader.Infrastructure;
using AntennaReader.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        // attributes
        private ICollectionView? _diagramView;
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
                _diagramView = CollectionViewSource.GetDefaultView(diagrams);
                _diagramView.Filter = this.DiagramFilter;
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

        #region Helper Function (Diagram Filter)
        /// <summary>
        /// Filter function for antenna diagrams based on search text box input
        /// </summary>
        private bool DiagramFilter(object row)
        {   
            AntennaDiagram? diagram = row as AntennaDiagram;
            if (diagram == null) return false; // row did not cast correctly

            string searchText = SearchBar.Text;
            if (string.IsNullOrEmpty(searchText)) return true; // no filter 

            bool nameMatch = diagram.AntennaName !=null && diagram.AntennaName.ToLower().Contains(searchText.ToLower());
            bool ownerMatch = diagram.AntennaOwner != null && diagram.AntennaOwner.ToLower().Contains(searchText.ToLower());
            bool stateMatch = diagram.State !=null && diagram.State.ToLower().Contains(searchText.ToLower());
            bool cityMatch = diagram.City !=null && diagram.City.ToLower().Contains(searchText.ToLower());

            return nameMatch || ownerMatch ||stateMatch || cityMatch;
        }
        #endregion

        #region Helper Function (Open Export Folder)
        /// <summary>
        /// opens the export folder dialog
        /// </summary>
        private static void OpenExportFolder()
        {
            // Make sure the folder exists (safe even if it already exists)
            Directory.CreateDirectory(AppPaths.ExportFolder);

            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.ExportFolder,
                UseShellExecute = true
            });
        }
        #endregion

        #region Text Changed -> Search Bar
        /// <summary>
        /// refreshes the diagram list based on search bar input
        /// </summary>
        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            _diagramView?.Refresh();
        }
        #endregion

        #region Click -> Delete Antenna Diagram(s)
        /// <summary>
        /// deletes selected antenna diagrams from SQLite database and refreshes the diagram list
        /// </summary>
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            List<AntennaDiagram> selectedDiagrams = GetSelectedDiagrams(); // get selected diagrams
            // check if list is empty
            if (!selectedDiagrams.Any())
            {
                MessageBox.Show("Please select at least one diagram to delete", "No Selection", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            // warn user -> confirm deletion
            MessageBoxResult confirm = MessageBox.Show($"Deleting {selectedDiagrams.Count} antenna diagrams from SQLite database? \nThis can not be undone!", "confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    foreach (AntennaDiagram diagram in selectedDiagrams)
                    {
                        AntennaDiagram? toDelete = db.AntennaDiagrams.Include(d => d.Measurements).FirstOrDefault(d => d.Id == diagram.Id);
                        if (toDelete != null)
                        {
                            db.AntennaDiagrams.Remove(toDelete);
                        }
                    }
                    db.SaveChanges();
                }
                LoadDiagrams(); // refresh diagram list
                MessageBox.Show($"Deleted {selectedDiagrams.Count} diagram(s) from database.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete diagram(s): {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        #endregion

        #region Click -> Clear Selection
        ///<summery>
        /// clears selected rows
        ///</summery>
        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            DiagramList.SelectedItems.Clear();
            DiagramList.SelectedItem = null;
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
                MessageBox.Show("Please select at least one diagram to export", "No selection", MessageBoxButton.OK, MessageBoxImage.Exclamation);
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
                MessageBox.Show($"Exported {selectedDiagrams.Count} diagram(s) to CSV \n{AppPaths.ExportFolder}", "Success", MessageBoxButton.OK, MessageBoxImage.Error);
                OpenExportFolder();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export diagram(s) to CSV: {ex.Message}", "Success", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Please select at least one diagram to export.", "No selection", MessageBoxButton.OK, MessageBoxImage.Exclamation);
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
                MessageBox.Show($"Exported {selectedDiagrams.Count} PAT file(s) to \n{AppPaths.ExportFolder}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                OpenExportFolder();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export PAT files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Import To Diagram
        /// <summary>
        /// Imports the measurements of a selected antenna diagram from the database into the main diagram window.
        ///</summary>
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            // check that only one is selected
            if (DiagramList.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select exactly ONE antenna diagram to import.", "Multiple Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AntennaDiagram? selectedDiagram = DiagramList.SelectedItem as AntennaDiagram;

            if (selectedDiagram == null)
            {
                MessageBox.Show("Please select a diagram to import.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (this.Owner is MainWindow main)
            {
                main.ImportDiagramById(selectedDiagram.Id);
            }
        }
        #endregion
    }
}
