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
        public SettingNameInputDialog()
        {
            InitializeComponent();
            NameBox.Focus();
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EnteredName = NameBox.Text.Trim();
            this.DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

    }
}
