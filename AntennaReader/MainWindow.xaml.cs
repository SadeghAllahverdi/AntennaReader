using AntennaReader.Infrastructure;
using AntennaReader.Models;
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
        #region Attributes
        private string _csvFile => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AntennaReader",
            "AntennaMeasurements.csv"
        );
        private string _patDir => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AntennaReader",
            "pat_files"
        );
        #endregion

        #region Command Bindings
        public static RoutedUICommand OpenImageCommand = new RoutedUICommand();
        public static RoutedUICommand DeleteImageCommand = new RoutedUICommand();
        
        public static RoutedUICommand SaveDBCommand = new RoutedUICommand();
        public static RoutedUICommand OpenDBCommand = new RoutedUICommand();

        public static RoutedUICommand DeleteDiagramCommand = new RoutedUICommand();
        public static RoutedUICommand LockDiagramCommand = new RoutedUICommand();
        public static RoutedUICommand DeletePointsCommand = new RoutedUICommand();

        public static RoutedUICommand UndoCommand = new RoutedUICommand();
        public static RoutedUICommand RedoCommand = new RoutedUICommand();
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();

            // connect command bindings to handler functions
            CommandBindings.Add(new CommandBinding(OpenImageCommand, OpenImage_Click));
            CommandBindings.Add(new CommandBinding(DeleteImageCommand, DeleteImage_Click));

            CommandBindings.Add(new CommandBinding(SaveDBCommand, SaveDB_Click));
            CommandBindings.Add(new CommandBinding(OpenDBCommand, OpenDB_Click));


            CommandBindings.Add(new CommandBinding(DeleteDiagramCommand, DeleteDiagram_Click));
            CommandBindings.Add(new CommandBinding(LockDiagramCommand, LockDiagram_Click));
            CommandBindings.Add(new CommandBinding(DeletePointsCommand, DeletePoints_Click));

            CommandBindings.Add(new CommandBinding(UndoCommand, Undo_Click));
            CommandBindings.Add(new CommandBinding(RedoCommand, Redo_Click));

            // create the base directory if it doesn't exist
            try
            {
                string baseDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AntennaReader"
                );
                Directory.CreateDirectory(baseDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create data directory: {ex.Message}", "Error");
            }
        }
        #endregion

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

        #region Click -> Save to Database
        /// <summery>
        /// Saves the current diagram and its measurements to the sqlite database
        /// </summery>
        private void SaveDB_Click(object sender, RoutedEventArgs e)
        {
            // check if diagram has 36 measurements
            if (drawingCanvas.measurements.Count != 36)
            {                 
                MessageBox.Show($"Please ensure that all the points have been measured. (missing : {36 - drawingCanvas.measurements.Count})", "Error");
                return;
            }

            string antennaID = Microsoft.VisualBasic.Interaction.InputBox("Enter Antenna ID / Station Name: (e.g. antennaID_station):", "Antenna ID / Station");

            if (string.IsNullOrWhiteSpace(antennaID))
            {
                return;
            }

            string state = Microsoft.VisualBasic.Interaction.InputBox("(Optional) Enter State (e.g. NRW):", "State");
            string city = Microsoft.VisualBasic.Interaction.InputBox("(Optional) Enter City (e.g. Dortmond):", "City");

            Dictionary<int, (double, Point)> measurments = drawingCanvas.measurements;

            try
            {
                using (var db = new AppDbContext())
                {
                    var diagram = new AntennaDiagram
                    {
                        StationName = antennaID,
                        State = state ?? string.Empty,
                        City = city ?? string.Empty,
                        CreateDate = DateTime.Now,
                        Measurements = new List<AntennaMeasurement>()
                    };
                    
                    foreach (var kvp in measurments)
                    {
                        int angle = kvp.Key;
                        double dbValue = kvp.Value.Item1;
                        Point p = kvp.Value.Item2;

                        var m = new AntennaMeasurement
                        {
                            Angle = angle,
                            DbValue = dbValue,
                            PosX = p.X,
                            PosY = p.Y
                        };

                        diagram.Measurements.Add(m);
                    }

                    db.AntennaDiagrams.Add(diagram);
                    db.SaveChanges();
                }
                MessageBox.Show($"Antenna {antennaID} saved to the database", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving to database: {ex.Message}", "Error");
            }
        }
        #endregion

        #region Click -> Open Database
        /// <summary>
        /// Opens the Database Browser window
        ///</summary>>
        private void OpenDB_Click(object sender, RoutedEventArgs e)
        {
            var browser = new DatabaseBrowser { Owner = this  };
            browser.Show();
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

        #region Click -> Interpolate Points
        /// <summary>
        /// handles when "Interpolate Points" is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InterpolatePoints_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas.measurements.Count == 0)
            {
                MessageBox.Show("Please add at least one point!");
                return;
            }
            drawingCanvas.InterpolateMeasurements();
            MessageBox.Show("Missing points are interpolated!", "Success");
        }
        #endregion

        #region Click -> Undo
        /// <summary>
        /// performs undo action
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas.UndoStack.Count > 0)
            {
                drawingCanvas.Undo();
            }
            else
            {
                MessageBox.Show("Nothing to undo!");
            }
        }
        #endregion

        #region Click -> Redo
        /// <summary>
        /// performs undo action
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas.RedoStack.Count > 0)
            {
                drawingCanvas.Redo();
            }
            else
            {
                MessageBox.Show("Nothing to Redo!");
            }
        }
        #endregion
    }
}