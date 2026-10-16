#nullable disable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BikeKings_System.Database;
using BikeKings_System.Helpers;
using BikeKings_System.Models;
using BikeKings_System.Views.Admin;
using BikeKings_System.Views.Cashier;
using BikeKings_System.Views.StoreClerk;

namespace BikeKings_System.Views
{
    public partial class LoginWindow : Window
    {
        private bool _isPasswordRevealed = false;

        public LoginWindow()
        {
            InitializeComponent();
        }

        // ── Username Placeholder Focus Handling ──
        private void TxtUsername_GotFocus(object sender, RoutedEventArgs e)
            => phUsername.Visibility = Visibility.Collapsed;

        private void TxtUsername_LostFocus(object sender, RoutedEventArgs e)
            => phUsername.Visibility = string.IsNullOrEmpty(txtUsername.Text)
               ? Visibility.Visible : Visibility.Collapsed;

        private void TxtUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_isPasswordRevealed) txtPasswordRevealed.Focus();
                else txtPassword.Focus();
            }
        }

        // ── Password Placeholder Focus Handling ──
        private void TxtPassword_GotFocus(object sender, RoutedEventArgs e)
            => phPassword.Visibility = Visibility.Collapsed;

        private void TxtPassword_LostFocus(object sender, RoutedEventArgs e)
            => phPassword.Visibility = string.IsNullOrEmpty(txtPassword.Password) && string.IsNullOrEmpty(txtPasswordRevealed.Text)
               ? Visibility.Visible : Visibility.Collapsed;

        private void TxtPasswordRevealed_GotFocus(object sender, RoutedEventArgs e)
            => phPassword.Visibility = Visibility.Collapsed;

        private void TxtPasswordRevealed_LostFocus(object sender, RoutedEventArgs e)
            => phPassword.Visibility = string.IsNullOrEmpty(txtPassword.Password) && string.IsNullOrEmpty(txtPasswordRevealed.Text)
               ? Visibility.Visible : Visibility.Collapsed;

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isPasswordRevealed)
            {
                txtPasswordRevealed.Text = txtPassword.Password;
                phPassword.Visibility = string.IsNullOrEmpty(txtPassword.Password) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void TxtPasswordRevealed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPasswordRevealed)
            {
                txtPassword.Password = txtPasswordRevealed.Text;
                phPassword.Visibility = string.IsNullOrEmpty(txtPasswordRevealed.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ── Eye Toggle Button Click Logic ──
        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
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

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnLogin_Click(sender, e);
        }

        // ── Login Button Logic ──
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = _isPasswordRevealed ? txtPasswordRevealed.Text : txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter your username and password.");
                return;
            }

            btnLogin.IsEnabled = false;

            UserModel user = AuthService.Login(username, password);

            btnLogin.IsEnabled = true;

            if (user == null)
            {
                ShowError("Invalid username or password.");
                txtPassword.Clear();
                txtPasswordRevealed.Clear();
                phPassword.Visibility = Visibility.Visible;
                if (_isPasswordRevealed) txtPasswordRevealed.Focus();
                else txtPassword.Focus();
                return;
            }

            AuditLogService.Log("LOGIN", $"User {user.Username} logged in.", user.UserID, user.Username, user.Role);

            SessionHelper.CurrentUser = user;
            OpenDashboard(user.Role);
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            pnlError.Visibility = Visibility.Visible;
        }

        private void OpenDashboard(string role)
        {
            try
            {
                Window dashboard;

                string normalizedRole = (role ?? "").Trim().ToLower().Replace(" ", "").Replace("_", "");

                if (normalizedRole == "admin" || normalizedRole == "administrator")
                {
                    dashboard = new AdminDashboardWindow();
                }
                else if (normalizedRole == "cashier")
                {
                    dashboard = new CashierDashboardWindow();
                }
                else if (normalizedRole == "storeclerk" || normalizedRole == "clerk" || normalizedRole == "inventorymanager" || normalizedRole == "inventory")
                {
                    dashboard = new StoreClerkDashboardWindow();
                }
                else
                {
                    ShowError($"Unknown role '{role}'. Contact administrator.");
                    return;
                }

                dashboard.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening dashboard for role '{role}':\n\n{ex.Message}\n\nInner Exception:\n{ex.InnerException?.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Dashboard Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ShowError($"Failed to launch dashboard: {ex.Message}");
            }
        }
    }
}