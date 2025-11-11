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


namespace AntennaReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _csvFile = "AntennaMeasurements.csv";
        private bool _csvFileExists = false;
        private string _patDir = "./pat_files/"; 
        private bool _patDirExists = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        #region Button Click -> Open Image
        /// <summary>
        /// handles when "Open Image" is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            // open file explorer
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            // user chose a file
            if (openFileDialog.ShowDialog() == true)
            {
                // get file path
                string filePath = openFileDialog.FileName;
                try
                {
                    // set background image
                    drawingCanvas.SetBackgroundImage(filePath);
                    MessageBox.Show("Image loaded!");
                }
                catch (Exception ex)
                {
                    // show error
                    MessageBox.Show($"Error loading image: {ex.Message}");
                }
            }
        }
        #endregion

        #region Click -> Delete Image
        /// <summary>
        /// handles when "Delete Image" is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteImage_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.DeleteBackgroundImage();
            MessageBox.Show("Image deleted!");
        }
        #endregion

        #region Click -> Save Measurements
        /// <summary>
        /// handles when "Save in CSV DB" is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveCSV_Click(object sender, RoutedEventArgs e)
        { 
            // if not all points are measured
            if (drawingCanvas.measurements.Count != 36)
            {
                MessageBox.Show($"Please ensure that all the points have been measured. (Missing: {36 - drawingCanvas.measurements.Count})");
                return;
            }
            // get antenna ID from user
            string antennaID = Microsoft.VisualBasic.Interaction.InputBox(
                "Please Enter Antenna ID and station name: (antennaid_station)",
                "Antenna ID + Station"
            );
            // if user entered empty ID or cancelled
            if (string.IsNullOrEmpty(antennaID))
            {
                return;
            }
            // prepare row data
            Dictionary<int, (double, Point)> measurements = drawingCanvas.measurements; // store current measurements
            List<string> row = new List<string> { antennaID }; // first column value -> antenna ID
            // add dB values for each angle
            for (int angle = 0; angle < 360; angle += 10)
            {
                double dbValue = measurements[angle].Item1;
                row.Add(Math.Round(dbValue, 1).ToString());
            }
            // does file already exist?
            using (StreamWriter writer = new StreamWriter(this._csvFile, append: true)) // make file
            {
                // if file already existed -> skip header
                if (!this._csvFileExists)
                {
                    List<string> header = new List<string> { "AntennaID" }; // antenna ID column
                    for (int angle = 0; angle < 360; angle += 10) // one column per angle
                    {
                        header.Add($"{angle}");
                    }
                    writer.WriteLine(string.Join(",", header));
                }
                // write row data
                writer.WriteLine(string.Join(",", row));
                this._csvFileExists = true;
            }
            // show success
            MessageBox.Show($"Antenna {antennaID} saved to {this._csvFile}!");
        }
        #endregion

        #region Click -> Save PAT Files
        /// <summary>
        /// handles when "Save in PAT DIR" is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SavePAT_Click(object sender, RoutedEventArgs e)
        {
            // check if csv exists
            if (!this._csvFileExists)
            {
                MessageBox.Show("No CSV file found. Please save measurements first!");
                return;
            }
            // create pat_files directory if it doesn't exist
            if (!this._patDirExists)
            {
                Directory.CreateDirectory(this._patDir);
                this._patDirExists = true;
            }
            try
            {
                // Read all lines from CSV
                string[] lines = File.ReadAllLines(this._csvFile);
                // check if file is empty
                if (lines.Length <= 1) // only header 
                {
                    MessageBox.Show("CSV file is empty!");
                    return;
                }
     
                int fileCount = 0;
                // process each line (skip header)
                for (int i = 1; i < lines.Length; i++)
                {
                    // prepare data
                    string[] values = lines[i].Split(',');
                    string antennaID = values[0];
                    // create .PAT file
                    string patFilePath = System.IO.Path.Combine(this._patDir, $"{antennaID}.PAT");
                    using (StreamWriter writer = new StreamWriter(patFilePath))
                    {
                        writer.WriteLine("'', 0, 2"); // fixed header line
                        for (int j = 1; j < values.Length; j++) // insert values
                        {
                            int angle = (j - 1) * 10;
                            string dbValue = values[j];
                            writer.WriteLine($" {angle}, {dbValue}");
                        }
                        writer.WriteLine("999"); // fixed footer line
                    }

                    fileCount++; // increment file number
                }
                // show success
                MessageBox.Show($"{fileCount} PAT files created in {this._patDir}!", "Success");
            }
            catch (Exception ex)
            {
                // show error
                MessageBox.Show($"Failed to create PAT files: {ex.Message}", "Error");
            }
        }
        #endregion

        #region Click -> Delete Diagram
        /// <summary>
        /// handles when "Delete Diagram" is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteDiagram_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.DeleteDiagram();
            MessageBox.Show("Diagram deleted!", "Success");
        }
        #endregion

        #region Click -> Lock Diagram
        /// <summary>
        /// handles when "Lock Diagram" is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LockDiagram_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.IsLocked = !drawingCanvas.IsLocked; // change lock state

            if (drawingCanvas.IsLocked)
            {
                drawingCanvas.Focus();
                MessageBox.Show("Diagram locked!");
            }
            else
            {
                MessageBox.Show("Diagram unlocked!");
            }
        }
        #endregion

        #region Click -> Delete Points
        /// <summary>
        /// handles when "Delete Points" is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeletePoints_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.DeleteMeasurements();
            MessageBox.Show("All points deleted!", "Success");
        }
        #endregion
    }
}