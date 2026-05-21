using AntennaReader;
using AntennaReader.Infrastructure;
using AntennaReader.Models;
using AntennaReader.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // NEW: Memory to track which algorithm the user ran last so the live-tuning knows what to recalculate
        private Func<DrawingCanvas, bool, Dictionary<int, double>>? _lastUsedAlgorithm = null;
        #endregion

        #region Command Bindings
        public static RoutedUICommand OpenImageCommand = new RoutedUICommand();
        public static RoutedUICommand DeleteBackgroundImageFromCanvasCommand = new RoutedUICommand();

        public static RoutedUICommand SaveDBCommand = new RoutedUICommand();
        public static RoutedUICommand OpenDBCommand = new RoutedUICommand();

        public static RoutedUICommand LockDiagramCommand = new RoutedUICommand();

        public static RoutedUICommand AutoExtractCurveSPCommand = new RoutedUICommand();
        public static RoutedUICommand AutoExtractCurveSPDebugCommand = new RoutedUICommand();
        public static RoutedUICommand AutoExtractCurveFourierCommand = new RoutedUICommand();
        public static RoutedUICommand AutoExtractCurveFourierDebugCommand = new RoutedUICommand();

        public static RoutedUICommand ToggleScaleModeCommand = new RoutedUICommand();
        public static RoutedUICommand DeleteDiagramCommand = new RoutedUICommand();
        public static RoutedUICommand DeletePointsCommand = new RoutedUICommand();

        public static RoutedUICommand UndoCommand = new RoutedUICommand();
        public static RoutedUICommand RedoCommand = new RoutedUICommand();

        public static RoutedUICommand OpenSettingsCommand = new RoutedUICommand();
        public static RoutedUICommand OpenPreferencesCommand = new RoutedUICommand();
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();

            CommandBindings.Add(new CommandBinding(OpenImageCommand, OpenImage_Click));
            CommandBindings.Add(new CommandBinding(DeleteBackgroundImageFromCanvasCommand, DeleteBackgroundImageFromCanvas_Click));

            CommandBindings.Add(new CommandBinding(SaveDBCommand, SaveDB_Click));
            CommandBindings.Add(new CommandBinding(OpenDBCommand, OpenDB_Click));

            CommandBindings.Add(new CommandBinding(LockDiagramCommand, LockDiagram_Click));

            CommandBindings.Add(new CommandBinding(AutoExtractCurveSPCommand, AutoExtractCurveSP_Click));
            CommandBindings.Add(new CommandBinding(AutoExtractCurveSPDebugCommand, AutoExtractCurveSPDebug_Click));
            CommandBindings.Add(new CommandBinding(AutoExtractCurveFourierCommand, AutoExtractCurveFourier_Click));
            CommandBindings.Add(new CommandBinding(AutoExtractCurveFourierDebugCommand, AutoExtractCurveFourierDebug_Click));

            CommandBindings.Add(new CommandBinding(ToggleScaleModeCommand, ToggleScaleMode_Click));
            CommandBindings.Add(new CommandBinding(DeleteDiagramCommand, DeleteDiagram_Click));
            CommandBindings.Add(new CommandBinding(DeletePointsCommand, DeletePoints_Click));

            CommandBindings.Add(new CommandBinding(UndoCommand, Undo_Click));
            CommandBindings.Add(new CommandBinding(RedoCommand, Redo_Click));

            CommandBindings.Add(new CommandBinding(OpenSettingsCommand, OpenSettings_Click));
            CommandBindings.Add(new CommandBinding(OpenPreferencesCommand, OpenPreferences_Click));
        }
        #endregion

        #region Click -> Open Image Folder
        /// <summary>
        /// opens the image folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppPaths.ImageFolder);
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                InitialDirectory = AppPaths.ImageFolder,
                Title = "Select an Image to Load as Background of the canvas"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                try
                {
                    drawingCanvas.SetBackgroundImage(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        messageBoxText: $"Failed to load image: {ex.Message}",
                        caption: "Error",
                        button: MessageBoxButton.OK,
                        icon: MessageBoxImage.Error
                        );
                }
            }
        }
        #endregion

        #region Click -> Delete Image From Canvas
        /// <summary>
        /// if there is a background image, it deletes it from the canvas.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteBackgroundImageFromCanvas_Click(object sender, RoutedEventArgs e)
        {
            // if no BG image, return
            if (!drawingCanvas.HasBackgroundImage)
            {
                return;
            }
            // otherwise
            MessageBoxResult result = MessageBox.Show(
                messageBoxText: "Are you sure you want to remove the background image from the canvas? This action cannot be undone.",
                caption: "Confirm Deletion",
                button: MessageBoxButton.YesNo,
                icon: MessageBoxImage.Question,
                defaultResult: MessageBoxResult.No);
            if (result == MessageBoxResult.Yes)
            {
                drawingCanvas.DeleteBackgroundImage();
            }
        }
        #endregion

        #region Click -> Open Image Folders
        /// <summary>
        /// Opens the application's Image folder in Windows Explorer.
        /// </summary>
        private void OpenImageFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.ImageFolder);
                System.Diagnostics.Process.Start("explorer.exe", AppPaths.ImageFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open image folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Open Debug Folder
        /// <summary>
        /// Opens the application's OpenCV Debug folder in Windows Explorer.
        /// </summary>
        private void OpenDebugFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DebugFolder);
                System.Diagnostics.Process.Start("explorer.exe", AppPaths.DebugFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open debug folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Open Export Folder
        /// <summary>
        /// Opens the application's Extract/Export data folder in Windows Explorer.
        /// </summary>
        private void OpenExportFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.ExportFolder);
                System.Diagnostics.Process.Start("explorer.exe", AppPaths.ExportFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open export folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Open Database
        /// <summary>
        /// opens the database browser window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenDB_Click(object sender, RoutedEventArgs e)
        {
            DatabaseBrowser dbBrowser = new DatabaseBrowser();
            dbBrowser.Owner = this;
            dbBrowser.Show();
        }
        #endregion

        #region Click -> Save to Database
        /// <summary>
        /// opens dialog window to save the current diagram to the database.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveDB_Click(object sender, RoutedEventArgs e)
        {
            // check if user has meaured at least 2 points
            if (drawingCanvas.measurements.Count < 2)
            {
                MessageBox.Show(
                    messageBoxText: "Please measure at least 2 points before saving.",
                    caption: "Incomplete Measurement",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Exclamation);
                return;
            }

            SaveDialog dlg = new SaveDialog();
            bool? result = dlg.ShowDialog();
            if (result != true) return;

            string antennaName = dlg.antennaName;
            string owner = dlg.owner;
            string state = dlg.state;
            string city = dlg.city;

            try
            {
                // raw db values that user has measured (angle, db)
                Dictionary<int, double> rawDb = drawingCanvas.measurements
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Item1);

                // interpolated values based on raw db and current interpolation mode
                Dictionary<int, double> interpolatedValues =
                    Interpolator.Interpolate(rawDb, drawingCanvas.InterpolationMode, drawingCanvas.Setting);

                using (AppDbContext db = new AppDbContext())
                {
                    // check if diagram already exists
                    AntennaDiagram? oldDiagram = db.AntennaDiagrams
                        .Include(d => d.Measurements)
                        .Include(d => d.InterpolatedMeasurements)
                        .FirstOrDefault(d => d.AntennaName.ToLower() == antennaName.ToLower());
                    // overwite if it exists
                    if (oldDiagram != null)
                    {
                        oldDiagram.AntennaOwner = owner ?? string.Empty;
                        oldDiagram.State = state ?? string.Empty;
                        oldDiagram.City = city ?? string.Empty;
                        oldDiagram.CreateDate = DateTime.Now;

                        db.AntennaMeasurements.RemoveRange(oldDiagram.Measurements);
                        db.AntennaInterpolatedMeasurements.RemoveRange(oldDiagram.InterpolatedMeasurements);
                        oldDiagram.Measurements.Clear();
                        oldDiagram.InterpolatedMeasurements.Clear();

                        foreach (KeyValuePair<int, (double, Point)> kvp in drawingCanvas.measurements)
                        {
                            oldDiagram.Measurements.Add(new AntennaMeasurement
                            {
                                Angle = kvp.Key,
                                DbValue = kvp.Value.Item1
                            });
                        }

                        foreach (KeyValuePair<int, double> kvp in interpolatedValues)
                        {
                            oldDiagram.InterpolatedMeasurements.Add(new AntennaInterpolatedMeasurement
                            {
                                Angle = kvp.Key,
                                DbValue = kvp.Value
                            });
                        }

                        db.SaveChanges();

                        MessageBox.Show($"Antenna {antennaName} was overwritten in the database.",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    // create new diagram if it doesn't exist
                    AntennaDiagram newDiagram = new AntennaDiagram
                    {
                        AntennaName = antennaName,
                        AntennaOwner = owner ?? string.Empty,
                        State = state ?? string.Empty,
                        City = city ?? string.Empty,
                        CreateDate = DateTime.Now
                    };

                    foreach (KeyValuePair<int, (double, Point)> kvp in drawingCanvas.measurements)
                    {
                        newDiagram.Measurements.Add(new AntennaMeasurement
                        {
                            Angle = kvp.Key,
                            DbValue = kvp.Value.Item1
                        });
                    }

                    foreach (KeyValuePair<int, double> kvp in interpolatedValues)
                    {
                        newDiagram.InterpolatedMeasurements.Add(new AntennaInterpolatedMeasurement
                        {
                            Angle = kvp.Key,
                            DbValue = kvp.Value
                        });
                    }

                    db.AntennaDiagrams.Add(newDiagram);
                    db.SaveChanges();
                }

                MessageBox.Show($"Antenna {antennaName} has been saved to the database.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving to database: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Helper Function -> Import From Database
        /// <summary>
        /// imports measurements from a given diagram into the drawing canvas
        /// </summary>
        public void ImportDiagramById(int id)
        {
            using (AppDbContext db = new AppDbContext())
            {   // check if diagram exists
                AntennaDiagram? diagram = db.AntennaDiagrams
                    .Include(d => d.Measurements)
                    .Include(d => d.InterpolatedMeasurements)
                    .FirstOrDefault(d => d.Id == id);
                // something has gon terribly wrong if this shows up :(
                if (diagram == null)
                {
                    MessageBox.Show(
                        messageBoxText: "Something went wrong. the chosen diagram is not found in database!",
                        caption: "Error",
                        button: MessageBoxButton.OK,
                        icon: MessageBoxImage.Error);
                    return;
                }

                Dictionary<int, double> measurements = new();
                foreach (AntennaMeasurement m in diagram.Measurements)
                {
                    measurements[m.Angle] = m.DbValue;
                }
                // and its even worse with this one!
                if (!drawingCanvas.SetMeasurements(measurements))
                {
                    MessageBox.Show(
                        messageBoxText: "Error importing measurements from database.",
                        caption: "Error",
                        button: MessageBoxButton.OK,
                        icon: MessageBoxImage.Error);
                    return;
                }

                AntennaNameText.Text = $"loaded: {diagram.AntennaName}";
            }
        }
        #endregion

        #region Click -> Delete Diagram
        /// <summary>
        /// calls DeleteDiagram()
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteDiagram_Click(object sender, RoutedEventArgs e)
        {
            if (!drawingCanvas.HasDiagram)
            {
                return;
            }
            drawingCanvas.DeleteDiagram();
            LockStatusText.Foreground = Brushes.Green;
            LockStatusText.Text = "Unlocked";

            // Clear algorithm memory if diagram is wiped
            _lastUsedAlgorithm = null;
        }
        #endregion

        #region Click -> Lock Diagram
        private void LockDiagram_Click(object sender, RoutedEventArgs e)
        {
            if (!drawingCanvas.HasDiagram)
            {
                return;
            }
            drawingCanvas.IsLocked = !drawingCanvas.IsLocked;
            drawingCanvas.Focus();
            // update lock button header ...
            LockButton.Header = drawingCanvas.IsLocked ? "Unlock (Ctrl + L)" : "Lock (Ctrl + L)";
            LockStatusText.Foreground = drawingCanvas.IsLocked ? Brushes.DarkRed : Brushes.Green;
            LockStatusText.Text = drawingCanvas.IsLocked ? "Locked" : "Unlocked";
        }
        #endregion

        #region Click -> Auto Extract Curves (SP & Fourier)
        private void AutoExtractCurveSP_Click(object sender, RoutedEventArgs e)
        {
            PerformCurveExtraction(DiagramDetectionServiceDP.ExtractCurve, false);
        }

        private void AutoExtractCurveSPDebug_Click(object sender, RoutedEventArgs e)
        {
            PerformCurveExtraction(DiagramDetectionServiceDP.ExtractCurve, true);
        }
        private void AutoExtractCurveFourier_Click(object sender, RoutedEventArgs e)
        {
            PerformCurveExtraction(DiagramDetectionServiceFA.ExtractCurve, false);
        }

        private void AutoExtractCurveFourierDebug_Click(object sender, RoutedEventArgs e)
        {
            PerformCurveExtraction(DiagramDetectionServiceFA.ExtractCurve, true);
        }
        #endregion

        #region Helper Function -> Perform Curve Extraction (SP or Fourier)
        /// <summary>
        /// Calls the Service to automatically extract the antenna curve
        /// from the locked bounding box using the IP+MBTA algorithm.
        /// </summary>
        private void PerformCurveExtraction(Func<DrawingCanvas, bool, Dictionary<int, double>> extractionAlgorithm, bool isDebug)
        {
            // check if diagram has bg image
            if (!drawingCanvas.HasBackgroundImage)
            {
                MessageBox.Show(
                    messageBoxText: "Please load a background image first.",
                    caption: "Missing Image",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Warning);
                return;
            }

            // check if diagram is drawn and is locked
            if (!drawingCanvas.HasDiagram || !drawingCanvas.IsLocked)
            {
                MessageBox.Show(
                    messageBoxText: "Please draw a bounding rectangle around the diagram and Lock it (Ctrl+L) before extracting.",
                    caption: "Diagram Not Locked",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Information);
                return;
            }

            // call service to extract 
            try
            {
                Dictionary<int, double> extractedData = extractionAlgorithm(drawingCanvas, isDebug);

                if (extractedData != null && extractedData.Count > 0)
                {
                    drawingCanvas.SetMeasurements(extractedData);

                    // NEW: Remember this algorithm so the Settings live-tuning knows what to run!
                    _lastUsedAlgorithm = extractionAlgorithm;

                    if (isDebug)
                    {
                        MessageBox.Show("Debug images successfully dumped to your debug folder!",
                                        "Debug Mode", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show(
                        messageBoxText: "Could not extract a valid curve. Try adjusting the bounding box.",
                        caption: "Extraction Failed",
                        button: MessageBoxButton.OK,
                        icon: MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messageBoxText: $"Error extracting curve: {ex.Message}",
                    caption: "Extraction Error",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Delete Points
        private void DeletePoints_Click(object sender, RoutedEventArgs e)
        {
            if (!drawingCanvas.HasDiagram)
            {
                return;
            }
            drawingCanvas.DeleteMeasurements();
            _lastUsedAlgorithm = null; // Clear memory if user trashes the points
        }
        #endregion

        #region Click -> Interpolation Mode
        /// <summary>
        /// sets the interpolation mode that drawing canvas applies to the measured points
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InterpMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item &&
                Enum.TryParse<InterpolationMode>(item.Tag?.ToString(), out var mode))
            {
                drawingCanvas.InterpolationMode = mode;
            }
        }
        #endregion

        #region Click -> Toggle Scale Mode (between log and equal-distance)
        /// <summary>
        /// quick shortcut to flip the canvas between log and equal-distance scale modes
        /// </summary>
        private void ToggleScaleMode_Click(object sender, RoutedEventArgs e)
        {
            // clone the current setting
            DrawingCanvasSetting newSetting = drawingCanvas.Setting.Clone();

            // apply new scale mode to it
            newSetting.IsLogScale = !newSetting.IsLogScale;
            // force lower bound of logarithmic scal to 0 db.
            if (newSetting.IsLogScale) newSetting.lowerBound = 0.0;
            drawingCanvas.ApplySetting(newSetting);
            drawingCanvas.Focus();
        }
        #endregion

        #region Click -> Open Settings
        /// <summary>
        /// opens drawing canvas setting window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            // save the current setting so we can revert
            DrawingCanvasSetting settingBeforeOpen = drawingCanvas.Setting.Clone();
            // create settings window
            SettingsWindow settingsWindow = new SettingsWindow(drawingCanvas.Setting);
            settingsWindow.Owner = this;
            // set up listen to trigger and apply updates
            settingsWindow.SettingChanged += (setting) => SettingsWindow_LiveTuningUpdate(settingsWindow, setting);
            bool? result = settingsWindow.ShowDialog();

            // If user clicked cancel, revert everything
            if (result != true)
            {
                RevertCanvasSettings(settingBeforeOpen);
            }
            drawingCanvas.Focus();
        }
        #endregion

        #region Helper Function -> Live Tuning Update from Settings Window
        private void SettingsWindow_LiveTuningUpdate(SettingsWindow window, DrawingCanvasSetting newSetting)
        {
            // update drawing canvas visuals first
            drawingCanvas.ApplySetting(newSetting.Clone());

            // Only run the fourier and DP algorithms if it's completely safe
            bool isReadyForMath = drawingCanvas.HasBackgroundImage && drawingCanvas.HasDiagram && drawingCanvas.IsLocked;

            if (isReadyForMath && _lastUsedAlgorithm != null)
            {
                try
                {
                    Dictionary<int, double> extractedData = _lastUsedAlgorithm(drawingCanvas, false);
                    if (extractedData != null && extractedData.Count > 0)
                    {
                        drawingCanvas.SetMeasurements(extractedData);
                        window.HideInlineError(); // Math worked, hide the warning!
                    }
                }
                catch
                {
                    window.ShowInlineError(); // Math failed, turn on the orange warning!
                }
            }
        }
        #endregion

        #region Helper Function -> Revert Canvas Settings
        private void RevertCanvasSettings(DrawingCanvasSetting originalSetting)
        {
            // Put the old rings back
            drawingCanvas.ApplySetting(originalSetting);

            // Put the old curve back if we can
            bool isReadyForMath = drawingCanvas.HasBackgroundImage && drawingCanvas.HasDiagram && drawingCanvas.IsLocked;
            if (isReadyForMath && _lastUsedAlgorithm != null)
            {
                try
                {
                    drawingCanvas.SetMeasurements(_lastUsedAlgorithm(drawingCanvas, false));
                }
                catch { }
            }
        }
        #endregion

        #region Click -> Open Preferences
        /// <summary>
        /// opens prefrences window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenPreferences_Click(object sender, RoutedEventArgs e)
        {
            Prefrences preferencesWindow = new Prefrences();
            preferencesWindow.Owner = this;
            preferencesWindow.ShowDialog();
        }
        #endregion

        #region Helper Function -> Load Preference
        /// <summary>
        /// loads a preference from the database by id and applies it to the canvas
        /// </summary>
        public void LoadPreference(int id)
        {
            using (AppDbContext db = new AppDbContext())
            {
                DrawingCanvasSetting? preference = db.DrawingCanvasSettings.FirstOrDefault(p => p.Id == id);

                if (preference == null)
                {
                    MessageBox.Show(
                        messageBoxText: "Something went wrong. The chosen preference was not found in the database!",
                        caption: "Error",
                        button: MessageBoxButton.OK,
                        icon: MessageBoxImage.Error);
                    return;
                }

                drawingCanvas.ApplySetting(preference);
                drawingCanvas.Focus();
            }
        }
        #endregion

        #region Click -> Undo
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas.UndoStack.Count > 0)
            {
                drawingCanvas.Undo();
            }
            else
            {
                MessageBox.Show("Nothing to undo!", "Empty Undo Stack", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region Click -> Redo
        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas.RedoStack.Count > 0)
            {
                drawingCanvas.Redo();
            }
            else
            {
                MessageBox.Show("Nothing to Redo!", "Empty Redo Stack", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion
    }
}