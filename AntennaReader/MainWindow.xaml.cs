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
using Microsoft.EntityFrameworkCore;


namespace AntennaReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Command Bindings
        public static RoutedUICommand OpenImageCommand = new RoutedUICommand();
        public static RoutedUICommand DeleteImageCommand = new RoutedUICommand();
        
        public static RoutedUICommand SaveDBCommand = new RoutedUICommand();
        public static RoutedUICommand OpenDBCommand = new RoutedUICommand();
        public static RoutedUICommand ImportDBCommand = new RoutedUICommand();

        public static RoutedUICommand LockDiagramCommand = new RoutedUICommand();
        public static RoutedUICommand InterpolatePointsCommand = new RoutedUICommand();
        public static RoutedUICommand DeleteDiagramCommand = new RoutedUICommand();
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
            CommandBindings.Add(new CommandBinding(ImportDBCommand, ImportFromDB_Click));

            CommandBindings.Add(new CommandBinding(LockDiagramCommand, LockDiagram_Click));
            CommandBindings.Add(new CommandBinding(InterpolatePointsCommand, InterpolatePoints_Click));
            CommandBindings.Add(new CommandBinding(DeleteDiagramCommand, DeleteDiagram_Click));
            CommandBindings.Add(new CommandBinding(DeletePointsCommand, DeletePoints_Click));

            CommandBindings.Add(new CommandBinding(UndoCommand, Undo_Click));
            CommandBindings.Add(new CommandBinding(RedoCommand, Redo_Click));
        }
        #endregion

        #region Click -> Open Image
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

        #region Click -> Open Database
        /// <summary>
        /// Handles when Open Database Browser is clicked
        ///</summary>>
        private void OpenDB_Click(object sender, RoutedEventArgs e)
        {
            DatabaseBrowser browser = new DatabaseBrowser(); // initialize db browser window
            browser.Owner = this; // owner is main window
            browser.Show(); // show
        }
        #endregion

        #region Click -> Save to Database
        /// <summery>
        /// Saves the current diagram and its measurements to the SQLite database
        /// </summery>
        private void SaveDB_Click(object sender, RoutedEventArgs e)
        {
            // check if diagram has  all 36 measurements
            if (drawingCanvas.measurements.Count != 36)
            {                 
                MessageBox.Show($"Please ensure that all the points have been measured. (missing : {36 - drawingCanvas.measurements.Count})");
                return;
            }

            // get Antenna Name
            string antennaName = Microsoft.VisualBasic.Interaction.InputBox("Enter Antenna Name: (antennaCode_stationName):", "Antenna Code + Station Name");
            // check if antenna Name is valid
            if (string.IsNullOrWhiteSpace(antennaName))
            {
                return;
            }
            // get state and city (optional)
            string state = Microsoft.VisualBasic.Interaction.InputBox("(Optional) Enter State (e.g. NRW):", "State");
            string city = Microsoft.VisualBasic.Interaction.InputBox("(Optional) Enter City (e.g. Dortmond):", "City");

            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    // 1. check if diagram exists
                    AntennaDiagram? existingDiagram = db.AntennaDiagrams
                        .Include(d => d.Measurements)
                        .FirstOrDefault(d => d.AntennaName.ToLower() == antennaName.ToLower());
                    if (existingDiagram != null)
                    {
                        existingDiagram.State = state ?? string.Empty; // update state
                        existingDiagram.City = city ?? string.Empty;   // update city
                        existingDiagram.CreateDate = DateTime.Now;     // update date
                        db.AntennaMeasurements.RemoveRange(existingDiagram.Measurements);
                        existingDiagram.Measurements.Clear();
                        foreach (KeyValuePair<int, (double, Point)> kvp in drawingCanvas.measurements)
                        {
                            AntennaMeasurement m = new AntennaMeasurement(); // new measurement object
                            m.Angle = kvp.Key;
                            m.DbValue = kvp.Value.Item1;
                            m.PosX = kvp.Value.Item2.X;
                            m.PosY = kvp.Value.Item2.Y;
                            existingDiagram.Measurements.Add(m);
                        }
                        db.SaveChanges();
                        MessageBox.Show($"Antenna {antennaName} was overwritten in the database.");
                        return;
                    }

                        // 2. save a new diagram 
                        AntennaDiagram diagram = new AntennaDiagram(); // new diagram object
                    diagram.AntennaName = antennaName;             // store antenna name
                    diagram.State = state ?? string.Empty;         // state
                    diagram.City = city ?? string.Empty;           // city
                    diagram.CreateDate = DateTime.Now;
                    // store measurements from current drawing canvas
                    diagram.Measurements = new List<AntennaMeasurement>();
                    foreach (KeyValuePair<int, (double, Point)> kvp in drawingCanvas.measurements)
                    {
                        AntennaMeasurement m = new AntennaMeasurement(); // new measurement object
                        m.Angle = kvp.Key;
                        m.DbValue = kvp.Value.Item1;
                        m.PosX = kvp.Value.Item2.X;
                        m.PosY = kvp.Value.Item2.Y;
                        diagram.Measurements.Add(m);
                    }
                    db.AntennaDiagrams.Add(diagram); // add diagram to database
                    db.SaveChanges();
                }
                MessageBox.Show($"Antenna {antennaName} has been saved to the database.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving to database: {ex.Message}");
            }
        }
        #endregion

        #region Click -> Import From Database
        /// <summery>
        /// opens the database browser to import a diagram from the database
        /// </summery>
        private void ImportFromDB_Click(object sender, RoutedEventArgs e)
        {
            DatabaseBrowser browser = new DatabaseBrowser(); // initialize db browser window
            browser.Owner = this; 
            // check if user selected a diagram to import
            bool? ok = browser.ShowDialog();
            if (ok != true)
            {
                return;
            }

            using (AppDbContext db = new AppDbContext())
            {
                AntennaDiagram diagram = db.AntennaDiagrams
                    .Include(d => d.Measurements)
                    .First(d => d.Id == (int)browser.Tag);
                // dictionary to store (angle, db)
                Dictionary<int, double> measurements = new Dictionary<int, double>();
                foreach (AntennaMeasurement m in diagram.Measurements)
                {
                    measurements[m.Angle] = m.DbValue;
                }

                if (!drawingCanvas.SetMeasurements(measurements))
                {
                    MessageBox.Show("Error importing diagram from database. Please Make sure that you have drawn a diagram!");
                    return;
                }

                // store antenna name
                drawingCanvas.Tag = diagram.AntennaName;
                AntennaNameText.Text = $"Loaded: {diagram.AntennaName}";
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