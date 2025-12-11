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
        public DatabaseBrowser()
        {
            InitializeComponent();
            LoadDiagrams();
        }

        private void LoadDiagrams()
        {
            using (var db = new AppDbContext())
            {
                var diagrams = db.AntennaDiagrams
                    .OrderBy(d => d.Id)
                    .ToList();

                DiagramList.ItemsSource = diagrams;
            }
        }

        private List<AntennaDiagram> GetSelectedDiagrams()
        {
            return DiagramList.SelectedItems.Cast<AntennaDiagram>().ToList();
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedDiagrams();
            if (!selected.Any())
            {
                MessageBox.Show("Please select at least one diagram to export", "No selection");
                return;
            }

            Directory.CreateDirectory(AppPaths.ExportFolder);
            string filePath = System.IO.Path.Combine(AppPaths.ExportFolder, $"AntennaDiagrams.csv");

            try
            {
                using (var db = new AppDbContext())
                using (var writer = new StreamWriter(filePath))
                {
                    List<string> headers = new List<string> { "Antenna ID" };
                    for (int angle = 0; angle < 360; angle += 10)
                    {
                        headers.Add($"Angle {angle}");
                    }
                    writer.WriteLine(string.Join(",", headers));

                    foreach (var item in selected)
                    {
                        var diagram = db.AntennaDiagrams
                            .Include(d => d.Measurements)
                            .First(d => d.Id == item.Id);

                        List<string> row = new List<string> { diagram.StationName ?? string.Empty };
                        for (int angle = 0; angle < 360; angle += 10)
                        {
                            var m = diagram.Measurements
                                .FirstOrDefault(meas => meas.Angle == angle);
                            double dbValue = m?.DbValue ?? 0.0;
                            row.Add($"{dbValue:F1}");
                        }
                        writer.WriteLine(string.Join(",", row));
                    }
                }
                MessageBox.Show($"Exported {selected.Count} diagram(s) to CSV \n{AppPaths.ExportFolder}", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export CSV: {ex.Message}", "Error");
            }
        }

        private void ExportPat_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedDiagrams();
            if (!selected.Any())
            {
                MessageBox.Show("Please select at least one diagram to export.", "No selection");
                return;
            }

            Directory.CreateDirectory(AppPaths.ExportFolder);

            try
            {
                using (var db = new AppDbContext())
                {
                    foreach (var item in selected)
                    {
                        var diagram = db.AntennaDiagrams
                            .Include(d => d.Measurements)
                            .First(d => d.Id == item.Id);

                        string name = (diagram.StationName ?? "Unknown")
                            .Replace(" ", "_")
                            .Replace(":", "_");

                        string filePath = System.IO.Path.Combine(AppPaths.ExportFolder, $"{name}.PAT");

                        using (var writer = new StreamWriter(filePath))
                        {
                            writer.WriteLine("'', 0, 2");
                            for (int angle = 0; angle < 360; angle += 10)
                            {
                                var m = diagram.Measurements.FirstOrDefault(meas => meas.Angle == angle);
                                double dbValue = m?.DbValue ?? 0.0;
                                writer.WriteLine($" {angle}, {dbValue:F1}");
                            }

                            writer.WriteLine("999");
                        }
                    }
                }

                MessageBox.Show($"Exported {selected.Count} PAT file(s) to \n{AppPaths.ExportFolder}", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export PAT files: {ex.Message}", "Error");
            }
        }
    }
}
