#nullable disable
using System.Windows;
using System.Windows.Controls;
using BikeKings_System.Database;

namespace BikeKings_System.Views.Admin
{
    public partial class CreateUserWindow : Window
    {
        private bool _isPasswordRevealed = false;

        public CreateUserWindow()
        {
            InitializeComponent();
        }

        private void BtnTogglePw_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordRevealed = !_isPasswordRevealed;
            if (_isPasswordRevealed)
            {
                txtPasswordRevealed.Text = txtPassword.Password;
                txtPassword.Visibility = Visibility.Collapsed;
                txtPasswordRevealed.Visibility = Visibility.Visible;
                txtPasswordRevealed.Focus();
                txtPasswordRevealed.CaretIndex = txtPasswordRevealed.Text.Length;
            }
            else
            {
                txtPassword.Password = txtPasswordRevealed.Text;
                txtPasswordRevealed.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Visible;
                txtPassword.Focus();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string fullName = txtFullName.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = _isPasswordRevealed ? txtPasswordRevealed.Text : txtPassword.Password;
            string role = ((ComboBoxItem)cbRole.SelectedItem)?.Content?.ToString() ?? "Cashier";

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please fill out all fields.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MaintenanceService.AddUser(fullName, username, password, role))
            {
                MessageBox.Show($"✅ User account '{username}' ({role}) created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Username already exists or database error occurred.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}