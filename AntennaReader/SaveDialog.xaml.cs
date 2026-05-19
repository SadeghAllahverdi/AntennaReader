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
    /// Interaction logic for SaveDialog.xaml
    /// </summary>
    public partial class SaveDialog : Window
    {
        public string antennaName { get; private set; } = string.Empty;
        public string owner { get; private set; } = string.Empty;
        public string state { get; private set; } = string.Empty;
        public string city { get; private set; } = string.Empty;

        /// <summary>
        /// empty dialog — used by "Save to Database" for brand new entries
        /// </summary>
        public SaveDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// pre-filled dialog — used by "Edit" in DatabaseBrowser to modify an existing entry
        /// </summary>
        public SaveDialog(string name, string owner, string state, string city)
        {
            InitializeComponent();
            AntennaNameBox.Text = name;
            OwnerBox.Text = owner;
            StateBox.Text = state;
            CityBox.Text = city;
            this.Title = "Edit Antenna";
            SaveButton.Content = "Update";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = AntennaNameBox.Text.Trim();
            // return if antenna name is empty
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Antenna name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.antennaName = name;
            this.owner = OwnerBox.Text.Trim();
            this.state = StateBox.Text.Trim();
            this.city = CityBox.Text.Trim();
            this.DialogResult = true;
        }
    }
}