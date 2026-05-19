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

            bool nameMatch = diagram.AntennaName != null && diagram.AntennaName.ToLower().Contains(searchText.ToLower());
            bool ownerMatch = diagram.AntennaOwner != null && diagram.AntennaOwner.ToLower().Contains(searchText.ToLower());
            bool stateMatch = diagram.State != null && diagram.State.ToLower().Contains(searchText.ToLower());
            bool cityMatch = diagram.City != null && diagram.City.ToLower().Contains(searchText.ToLower());

            return nameMatch || ownerMatch || stateMatch || cityMatch;
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

        #region Function (Safe File Name)
        /// <summary>
        /// changes a string to a safe file name by removing invalid chars.
        /// </summary>
        private static string SafeFileName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "Unknown";
            StringBuilder sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (Path.GetInvalidFileNameChars().Contains(c))
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Trim().Replace(" ", "_");
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
                MessageBox.Show(
                    messageBoxText:"Please select at least one diagram to delete", 
                    caption:"No Selection", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Exclamation);
                return;
            }

            // warn user -> confirm deletion
            MessageBoxResult confirm = MessageBox.Show(
                messageBoxText:$"Deleting {selectedDiagrams.Count} antenna diagrams from SQLite database? \nThis can not be undone!", 
                caption:"Confirm Deletion", 
                button:MessageBoxButton.YesNo, 
                icon:MessageBoxImage.Warning,
                defaultResult: MessageBoxResult.No);
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
                MessageBox.Show(
                    messageBoxText:$"Deleted {selectedDiagrams.Count} diagram(s) from database.", 
                    caption:"Success", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messageBoxText:$"Failed to delete diagram(s): {ex.Message}", 
                    caption:"Error", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Error);
                return;
            }
        }
        #endregion

        #region Click -> Edit Antenna Metadata
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (DiagramList.SelectedItems.Count != 1)
            {
                MessageBox.Show(
                    messageBoxText: "Please select exactly one antenna diagram to edit its metadata.",
                    caption: "Multiple Selection",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Warning);
                return;
            }

            AntennaDiagram? selectedDiagram = DiagramList.SelectedItem as AntennaDiagram;
            if (selectedDiagram == null) return;

            SaveDialog dlg = new SaveDialog(
                selectedDiagram.AntennaName,
                selectedDiagram.AntennaOwner,
                selectedDiagram.State,
                selectedDiagram.City);
            dlg.Owner = this;

            bool? result = dlg.ShowDialog();
            if (result != true) return;

            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    bool nameConflict = db.AntennaDiagrams
                        .Any(d => d.Id != selectedDiagram.Id
                            && d.AntennaName.ToLower() == dlg.antennaName.ToLower());

                    if (nameConflict)
                    {
                        MessageBox.Show(
                            messageBoxText:$"Another antenna with the name '{dlg.antennaName}' already exists. Choose a different name.",
                            caption:"Name Conflict",
                            button:MessageBoxButton.OK,
                            icon:MessageBoxImage.Warning);
                        return;
                    }

                    AntennaDiagram? rowToEdit = db.AntennaDiagrams.FirstOrDefault(d => d.Id == selectedDiagram.Id);
                    if (rowToEdit != null)
                    {
                        rowToEdit.AntennaName = dlg.antennaName;
                        rowToEdit.AntennaOwner = dlg.owner ?? string.Empty;
                        rowToEdit.State = dlg.state ?? string.Empty;
                        rowToEdit.City = dlg.city ?? string.Empty;
                        rowToEdit.CreateDate = DateTime.Now;
                        db.SaveChanges();
                    }
                }

                LoadDiagrams();
                MessageBox.Show(
                    messageBoxText:$"Antenna '{dlg.antennaName}' updated.",
                    caption:"Success", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messageBoxText:$"Failed to update diagram: {ex.Message}",
                    caption: "Error",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Error);   
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
            List<AntennaDiagram> selectedDiagrams = GetSelectedDiagrams();
            if (!selectedDiagrams.Any())
            {
                MessageBox.Show(
                    messageBoxText:"Please select at least one diagram to export", 
                    caption:"No selection", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Exclamation);
                return;
            }

            // pull active setting from the main window
            DrawingCanvasSetting activeSetting = new DrawingCanvasSetting();
            int csvPrecision = 3;
            if (this.Owner is MainWindow main)
            {
                activeSetting = main.drawingCanvas.Setting;
                csvPrecision = activeSetting.CsvExportPrecision;
            }

            string filePath = System.IO.Path.Combine(AppPaths.ExportFolder, "AntennaDiagrams.csv");

            try
            {
                using (AppDbContext db = new AppDbContext())
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // header
                    List<string> header = new List<string> { "Antenna Name" };
                    for (int angle = 0; angle < 360; angle++)
                    {
                        header.Add($"Angle {angle}");
                    }
                    writer.WriteLine(string.Join(",", header));

                    // rows
                    foreach (AntennaDiagram diagram in selectedDiagrams)
                    {
                        AntennaDiagram result = db.AntennaDiagrams
                            .Include(d => d.Measurements)
                            .First(d => d.Id == diagram.Id);

                        Dictionary<int, double> raw = result.Measurements
                            .ToDictionary(m => m.Angle, m => m.DbValue);

                        Dictionary<int, double> dense = Interpolator.Interpolate(
                            raw,
                            InterpolationMode.Monotone,
                            activeSetting
                        );

                        List<string> row = new List<string> { result.AntennaName ?? string.Empty };
                        for (int angle = 0; angle < 360; angle++)
                        {
                            double dbValue = dense.TryGetValue(angle, out double v) ? v : 0.0;
                            row.Add(dbValue.ToString($"F{csvPrecision}", System.Globalization.CultureInfo.InvariantCulture));
                        }
                        writer.WriteLine(string.Join(",", row));
                    }
                }

                MessageBox.Show(
                    messageBoxText:$"Exported {selectedDiagrams.Count} diagram(s) to CSV \n{AppPaths.ExportFolder}",
                    caption:"Success", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Information);
                OpenExportFolder();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messageBoxText: $"Failed to export diagram(s) to CSV: {ex.Message}", 
                    caption:"Error", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Export PAT
        /// <summary>
        /// exports selected antenna diagrams from the database browser window to separate PAT file(s).
        /// </summary>
        private void ExportPAT_Click(object sender, RoutedEventArgs e)
        {
            List<AntennaDiagram> selectedDiagrams = GetSelectedDiagrams();
            if (!selectedDiagrams.Any())
            {
                MessageBox.Show(
                    messageBoxText:"Please select at least one diagram to export.", 
                    caption:"No selection", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Exclamation);
                return;
            }

            // pull active setting from the main window
            DrawingCanvasSetting activeSetting = new DrawingCanvasSetting();
            int patPrecision = 3;
            if (this.Owner is MainWindow main)
            {
                activeSetting = main.drawingCanvas.Setting;
                patPrecision = activeSetting.PATExportPrecision;
            }

            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    foreach (AntennaDiagram diagram in selectedDiagrams)
                    {
                        AntennaDiagram result = db.AntennaDiagrams
                            .Include(d => d.Measurements)
                            .First(d => d.Id == diagram.Id);

                        Dictionary<int, double> raw = result.Measurements
                            .ToDictionary(m => m.Angle, m => m.DbValue);

                        Dictionary<int, double> dense = Interpolator.Interpolate(
                            raw,
                            InterpolationMode.Monotone,
                            activeSetting
                        );

                        string filename = SafeFileName(result.AntennaName);
                        string filePath = System.IO.Path.Combine(AppPaths.ExportFolder, $"{filename}.PAT");

                        using (StreamWriter writer = new StreamWriter(filePath))
                        {
                            writer.WriteLine("'', 0, 2");
                            for (int angle = 0; angle < 360; angle++)
                            {
                                double dbValue = dense.TryGetValue(angle, out double v) ? v : 0.0;
                                writer.WriteLine($" {angle}, {dbValue.ToString($"F{patPrecision}", System.Globalization.CultureInfo.InvariantCulture)}");
                            }
                            writer.WriteLine("999");
                        }
                    }
                }

                MessageBox.Show(
                    messageBoxText:$"Exported {selectedDiagrams.Count} PAT file(s) to \n{AppPaths.ExportFolder}", 
                    caption:"Success", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Information);
                OpenExportFolder();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messageBoxText:$"Failed to export PAT files: {ex.Message}", 
                    caption:"Error", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Error);
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
                MessageBox.Show(
                    messageBoxText:"Please select exactly one antenna diagram to import.", 
                    caption:"Multiple Selection", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Warning);
                return;
            }

            AntennaDiagram? selectedDiagram = DiagramList.SelectedItem as AntennaDiagram;

            if (selectedDiagram == null)
            {
                MessageBox.Show(
                    messageBoxText:"Please select a diagram to import.", 
                    caption:"No Selection", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Information);
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