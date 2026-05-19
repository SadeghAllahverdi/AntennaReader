using AntennaReader.Infrastructure;
using AntennaReader.Models;
using System;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Windows;

namespace AntennaReader
{
    /// <summary>
    /// Interaction logic for PreferencesWindow.xaml
    /// Browse, apply, edit, rename and delete saved diagram preferences.
    /// </summary>
    public partial class Prefrences : Window
    {
        #region Constructor
        public Prefrences()
        {
            InitializeComponent();
            LoadPreferenceListFromDatabase();
        }
        #endregion

        #region Helper -> Load Preference List From Database
        /// <summary>
        /// reads all saved preferences from the database and shows them in the list
        /// </summary>
        private void LoadPreferenceListFromDatabase()
        {
            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    PreferenceList.ItemsSource = db.GetAllDCSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messageBoxText:$"Failed to load preferences: {ex.Message}",
                    caption:"Error", 
                    button: MessageBoxButton.OK, 
                    icon: MessageBoxImage.Error);
            }
        }
        #endregion

        #region Helper -> Get Selected Preference
        /// <summary>
        /// returns the currently selected preference, or null if nothing is selected
        /// </summary>
        private DrawingCanvasSetting? GetSelectedPreference()
        {
            return PreferenceList.SelectedItem as DrawingCanvasSetting;
        }
        #endregion

        #region Click -> Apply
        /// <summary>
        /// pushes the selected preference into the main canvas
        /// </summary>
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DrawingCanvasSetting? selectedPreference = GetSelectedPreference();
            if (selectedPreference == null)
            {
                MessageBox.Show(
                    messageBoxText:"Select a preference first.", 
                    caption:"No Selection", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Exclamation);
                return;
            }

            if (this.Owner is MainWindow mainWindow)
            {
                mainWindow.LoadPreference(selectedPreference.Id);
            }
            this.Close();
        }
        #endregion

        #region Click -> Edit
        /// <summary>
        /// opens the settings window pre-loaded with the selected preference,
        /// then writes the edits back to the same preference row on save
        /// </summary>
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            DrawingCanvasSetting? selectedPreference = GetSelectedPreference();
            if (selectedPreference == null)
            {
                MessageBox.Show(
                    messageBoxText: "Select a preference first.",
                    caption: "No Selection", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Exclamation);
                return;
            }

            SettingsWindow settingsWindow = new SettingsWindow(selectedPreference.Clone());
            settingsWindow.Owner = this;

            if (this.Owner is MainWindow mainWindow)
            {
                settingsWindow.SettingChanged += s => mainWindow.LoadPreference(s.Id);
            }

            bool? settingsResult = settingsWindow.ShowDialog();
            if (settingsResult != true) return;

            // write the edited values back to the same named row
            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    db.SaveDrawingCanvasSetting(selectedPreference.Name, settingsWindow.currentSetting.Clone());
                }
                LoadPreferenceListFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save preference: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Rename
        /// <summary>
        /// renames the selected preference
        /// </summary>
        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            DrawingCanvasSetting? selectedPreference = GetSelectedPreference();
            if (selectedPreference == null)
            {
                MessageBox.Show("Select a preference first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            SettingNameInputDialog nameDialog = new SettingNameInputDialog();
            nameDialog.Owner = this;
            if (nameDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(nameDialog.EnteredName))
            {
                return;
            }

            string newName = nameDialog.EnteredName.Trim();
            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    DrawingCanvasSetting? rowToRename = db.DrawingCanvasSettings.FirstOrDefault(s => s.Id == selectedPreference.Id);
                    if (rowToRename != null)
                    {
                        rowToRename.Name = newName;
                        rowToRename.LastModified = DateTime.Now;
                        db.SaveChanges();
                    }
                }
                LoadPreferenceListFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Delete
        /// <summary>
        /// deletes the selected preference after confirmation
        /// </summary>
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            DrawingCanvasSetting? selectedPreference = GetSelectedPreference();
            if (selectedPreference == null)
            {
                MessageBox.Show("Select a preference first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"Delete preference '{selectedPreference.Name}'? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes) return;

            try
            {
                using (AppDbContext db = new AppDbContext())
                {
                    db.DeleteDrawingCanvasSetting(selectedPreference.Name);
                }
                LoadPreferenceListFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Click -> Close
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion
    }
}