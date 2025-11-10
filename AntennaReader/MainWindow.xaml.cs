using AntennaReader;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;
namespace AntennaReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                try
                {
                    drawingCanvas.SetBackgroundImage(filePath);
                    MessageBox.Show("Image loaded!", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}", "Error");
                }
            }
        }

        private void DeleteImage_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.DeleteBackgroundImage();
            MessageBox.Show("Image deleted!", "Success");
        }

        private void SaveCSV_Click(object sender, RoutedEventArgs e)
        {
            string csvFile = "AntennaMeasurements.csv";
            int count = drawingCanvas.measurements.Count;

            if (count != 36)
            {
                MessageBox.Show($"Please ensure that all the points have been measured. (Missing: {36 - count})", "Error");
                return;
            }

            string antennaID = Microsoft.VisualBasic.Interaction.InputBox(
                "Please Enter Antenna ID and station name: (antennaid_station)",
                "Antenna ID + Station"
            );

            if (string.IsNullOrEmpty(antennaID))
            {
                return;
            }

            // prepare row data
            Dictionary<int, (double, Point)> measurements = drawingCanvas.measurements;
            List<string> row = new List<string> { antennaID };

            for (int angle = 0; angle < 360; angle += 10)
            {
                double dbValue = measurements[angle].Item1;
                row.Add(Math.Round(dbValue, 1).ToString());
            }

            // write to CSV
            bool fileExists = File.Exists(csvFile);

            using (StreamWriter writer = new StreamWriter(csvFile, append: true))
            {
                // if file is new -> write header
                if (!fileExists)
                {
                    List<string> header = new List<string> { "AntennaID" };
                    for (int angle = 0; angle < 360; angle += 10)
                    {
                        header.Add($"{angle}");
                    }
                    writer.WriteLine(string.Join(",", header));
                }
                // write row data
                writer.WriteLine(string.Join(",", row));
            }

            MessageBox.Show($"Antenna {antennaID} saved to {csvFile}!", "Success");
        }

        private void SavePAT_Click(object sender, RoutedEventArgs e)
        {
            string csvFile = "AntennaMeasurements.csv";
            string patDir = "./pat_files/";

            // Check if CSV exists
            if (!File.Exists(csvFile))
            {
                MessageBox.Show("No CSV file found. Please save measurements first!", "Error");
                return;
            }

            // Create pat_files directory if it doesn't exist
            if (!Directory.Exists(patDir))
            {
                Directory.CreateDirectory(patDir);
            }

            try
            {
                // Read all lines from CSV
                string[] lines = File.ReadAllLines(csvFile);

                if (lines.Length <= 1)
                {
                    MessageBox.Show("CSV file is empty!", "Error");
                    return;
                }

                int fileCount = 0;

                // Skip header (line 0), process data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] values = lines[i].Split(',');
                    string antennaID = values[0];

                    // Create PAT file
                    string patFilePath = System.IO.Path.Combine(patDir, $"{antennaID}.PAT");
                    using (StreamWriter writer = new StreamWriter(patFilePath))
                    {
                        writer.WriteLine("'', 0, 2");

                        // Write angle and dB value pairs
                        for (int j = 1; j < values.Length; j++)
                        {
                            int angle = (j - 1) * 10;
                            string dbValue = values[j];
                            writer.WriteLine($" {angle}, {dbValue}");
                        }

                        writer.WriteLine("999");
                    }

                    fileCount++;
                }

                MessageBox.Show($"{fileCount} PAT files created in {patDir}!", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create PAT files: {ex.Message}", "Error");
            }
        }

        private void DrawDiagram_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Draw Diagram clicked!");
        }

        private void DeleteDiagram_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.DeleteDiagram();
            MessageBox.Show("Diagram deleted!", "Success");
        }

        private void LockDiagram_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.IsLocked = !drawingCanvas.IsLocked;

            if (drawingCanvas.IsLocked)
            {
                drawingCanvas.Focus();
                MessageBox.Show("Diagram locked!", "Success");
            }
            else
            {
                MessageBox.Show("Diagram unlocked!", "Success");
            }
        }

        private void DeletePoints_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.DeleteMeasurements();
            MessageBox.Show("All points deleted!", "Success");
        }

        private void UndoPoint_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Undo Point clicked!");
        }

        private void RedoPoint_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Redo Point clicked!");
        }

        private void ResizeWindow_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Resize Window clicked!");
        }

        private void RecenterWindow_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Recenter Window clicked!");
        }
    }
}