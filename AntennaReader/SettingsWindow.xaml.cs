using AntennaReader.Infrastructure;
using AntennaReader.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AntennaReader
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // limits for the dB range inputs
        private const double MinAllowedLowerBound = -20.0;
        private const double MaxAllowedLowerBound = 50.0;
        private const double MinAllowedUpperBound = 5.0;
        private const double MaxAllowedUpperBound = 60.0;
        private const double MinAllowedRange = 5.0;

        // precision dropdown limits (decimal places)
        private const int MinExportPrecision = 0;
        private const int MaxExportPrecision = 3;

        // tolerance for matching doubles in the contour step dropdown
        private const double DoubleMatchTolerance = 1e-9;

        // the live editable copy — every edit goes here, and the canvas mirrors it
        public DrawingCanvasSetting currentSetting { get; private set; }
        // a snapshot taken when the window opened — used to revert if user cancels
        private DrawingCanvasSetting originalSetting;
        // true while the constructor is still populating controls
        // we use this to ignore event handlers firing during initial setup
        private bool isInitializing = true;

        // raised every time the user changes something
        // MainWindow subscribes so the canvas updates live
        public event Action<DrawingCanvasSetting>? SettingChanged;

        #region Constructor
        public SettingsWindow(DrawingCanvasSetting setting)
        {
            InitializeComponent();
            currentSetting = setting.Clone();
            originalSetting = setting.Clone();

            SyncUiFromSetting();
            isInitializing = false;
        }
        #endregion

        #region Helper -> Sync UI From Setting
        /// <summary>
        /// populates every control to match the current setting
        /// </summary>
        private void SyncUiFromSetting()
        {
            // scale mode radio buttons
            LogScaleRadio.IsChecked = currentSetting.IsLogScale;
            LinearScaleRadio.IsChecked = !currentSetting.IsLogScale;

            // dB range text boxes
            LowerBoundBox.Text = currentSetting.lowerBound.ToString(CultureInfo.InvariantCulture);
            UpperBoundBox.Text = currentSetting.upperBound.ToString(CultureInfo.InvariantCulture);
            // in log mode the lower bound is always 0 — disable the box
            LowerBoundBox.IsEnabled = !currentSetting.IsLogScale;
            DbHintText.Visibility = currentSetting.IsLogScale ? Visibility.Visible : Visibility.Collapsed;

            // contour step is only relevant in equal-distance mode
            bool isEqualDistance = !currentSetting.IsLogScale;
            ContourStepLabel.IsEnabled = isEqualDistance;
            ContourStepCombo.IsEnabled = isEqualDistance;

            FillContourStepCombo();
            FillPrecisionCombos();
        }
        #endregion

        #region Helper -> Fill Contour Step Combo
        /// <summary>
        /// fills the contour step dropdown with valid step values for the current range
        /// </summary>
        private void FillContourStepCombo()
        {
            ContourStepCombo.Items.Clear();

            List<double> validSteps = currentSetting.GetValidContourSteps();
            // make sure the currently selected step is in the list even if it's not "valid"
            if (!validSteps.Contains(currentSetting.ContourStep))
            {
                validSteps.Add(currentSetting.ContourStep);
                validSteps.Sort();
            }

            double minContour = currentSetting.IsLogScale ? 0.0 : currentSetting.lowerBound;
            double range = currentSetting.upperBound - minContour;

            foreach (double step in validSteps)
            {
                int ringCount = (int)Math.Floor(range / step);
                ContourStepCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{step} dB  →  {ringCount} circles",
                    Tag = step
                });
            }

            // select the item that matches the current step
            foreach (ComboBoxItem item in ContourStepCombo.Items)
            {
                double stepValue = (double)item.Tag;
                if (Math.Abs(stepValue - currentSetting.ContourStep) < DoubleMatchTolerance)
                {
                    ContourStepCombo.SelectedItem = item;
                    break;
                }
            }
        }
        #endregion

        #region Helper -> Fill Precision Combos
        /// <summary>
        /// fills the CSV and PAT precision dropdowns with human-friendly labels
        /// </summary>
        private void FillPrecisionCombos()
        {
            string[] precisionLabels = {
                "1 (no decimals)",
                "0.1 (1 decimal)",
                "0.01 (2 decimals)",
                "0.001 (3 decimals)"
            };

            CsvPrecisionCombo.Items.Clear();
            PatPrecisionCombo.Items.Clear();

            for (int decimals = MinExportPrecision; decimals <= MaxExportPrecision; decimals++)
            {
                CsvPrecisionCombo.Items.Add(new ComboBoxItem { Content = precisionLabels[decimals], Tag = decimals });
                PatPrecisionCombo.Items.Add(new ComboBoxItem { Content = precisionLabels[decimals], Tag = decimals });
            }
            CsvPrecisionCombo.SelectedIndex = Math.Clamp(currentSetting.CsvExportPrecision, MinExportPrecision, MaxExportPrecision);
            PatPrecisionCombo.SelectedIndex = Math.Clamp(currentSetting.PATExportPrecision, MinExportPrecision, MaxExportPrecision);
        }
        #endregion

        #region Helper -> Notify Canvas
        /// <summary>
        /// raises the SettingChanged event so the canvas can update live
        /// </summary>
        private void NotifyCanvas()
        {
            SettingChanged?.Invoke(currentSetting);
        }
        #endregion

        #region Event -> Scale Mode Changed
        private void ScaleMode_Changed(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            currentSetting.IsLogScale = LogScaleRadio.IsChecked == true;

            // log mode forces lower bound to 0
            if (currentSetting.IsLogScale)
            {
                currentSetting.lowerBound = 0.0;
                LowerBoundBox.Text = "0";
            }
            LowerBoundBox.IsEnabled = !currentSetting.IsLogScale;
            DbHintText.Visibility = currentSetting.IsLogScale ? Visibility.Visible : Visibility.Collapsed;

            // re-enable / disable the contour step controls
            bool isEqualDistance = !currentSetting.IsLogScale;
            ContourStepLabel.IsEnabled = isEqualDistance;
            ContourStepCombo.IsEnabled = isEqualDistance;

            FillContourStepCombo();
            NotifyCanvas();
        }
        #endregion

        #region Event -> Bounds Text Changed
        private void BoundsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;

            // ignore invalid numbers — user might be mid-typing
            bool lowerParsed = double.TryParse(LowerBoundBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lowerBound);
            bool upperParsed = double.TryParse(UpperBoundBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double upperBound);
            if (!lowerParsed || !upperParsed) return;

            // clamp to sensible limits
            lowerBound = Math.Clamp(lowerBound, MinAllowedLowerBound, MaxAllowedLowerBound);
            upperBound = Math.Clamp(upperBound, MinAllowedUpperBound, MaxAllowedUpperBound);
            // require a minimum gap so the diagram doesn't degenerate
            if (upperBound - lowerBound < MinAllowedRange) return;

            currentSetting.lowerBound = lowerBound;
            currentSetting.upperBound = upperBound;
            FillContourStepCombo();
            NotifyCanvas();
        }
        #endregion

        #region Event -> Contour Step Changed
        private void ContourStepCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;

            if (ContourStepCombo.SelectedItem is ComboBoxItem item && item.Tag is double step)
            {
                currentSetting.ContourStep = step;
                NotifyCanvas();
            }
        }
        #endregion

        #region Event -> Precision Changed
        private void PrecisionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;

            if (CsvPrecisionCombo.SelectedItem is ComboBoxItem csvItem && csvItem.Tag is int csvDecimals)
            {
                currentSetting.CsvExportPrecision = csvDecimals;
            }
            if (PatPrecisionCombo.SelectedItem is ComboBoxItem patItem && patItem.Tag is int patDecimals)
            {
                currentSetting.PATExportPrecision = patDecimals;
            }
            NotifyCanvas();
        }
        #endregion

        #region Click -> Save as Preference
        /// <summary>
        /// asks for a name, then saves the current setting as a named preference in the database
        /// </summary>
        private void SaveAsPreference_Click(object sender, RoutedEventArgs e)
        {
            SettingNameInputDialog nameDialog = new SettingNameInputDialog();
            nameDialog.Owner = this;
            if (nameDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(nameDialog.EnteredName))
            {
                return;
            }

            string preferenceName = nameDialog.EnteredName.Trim();
            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    DrawingCanvasSetting toSave = currentSetting.Clone();
                    toSave.Id = 0; // EF assigns a new id
                    db.SaveDrawingCanvasSetting(preferenceName, toSave);
                }
                MessageBox.Show($"Preference '{preferenceName}' saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save preference: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Done
        private void Done_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion

        #region Click -> Cancel
        /// <summary>
        /// reverts the canvas back to whatever it had before the window opened
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            currentSetting = originalSetting.Clone();
            NotifyCanvas();
            this.DialogResult = false;
        }
        #endregion
    }
}