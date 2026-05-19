using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace AntennaReader
{
    /// <summary>
    /// Interaktionslogik für SettingNameInputDialog.xaml
    /// </summary>
    public partial class SettingNameInputDialog : Window
    {
        public string EnteredName { get; private set; } = string.Empty;
        #region Constructor
        public SettingNameInputDialog()
        {
            InitializeComponent();
            NameBox.Focus();
        }
        #endregion

        #region Function Click Save
        /// <summary>
        /// handles save click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show(
                    messageBoxText:"Name cannot be empty.", 
                    caption:"Error", 
                    button:MessageBoxButton.OK, 
                    icon:MessageBoxImage.Warning);
                return;
            }
            EnteredName = NameBox.Text.Trim();
            this.DialogResult = true;
        }
        #endregion

        #region Click Cancel
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
