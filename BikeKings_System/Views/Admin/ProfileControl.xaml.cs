using System;
using System.Windows;
using System.Windows.Controls;
using BikeKings_System.Database;
using BikeKings_System.Helpers;

namespace BikeKings_System.Views.Admin
{
    public partial class ProfileControl : UserControl
    {
        public ProfileControl()
        {
            InitializeComponent();
            LoadProfileData();
        }

        public void LoadProfileData()
        {
            if (SessionHelper.CurrentUser != null)
            {
                txtFullName.Text = SessionHelper.CurrentUser.FullName;
                txtUsername.Text = SessionHelper.CurrentUser.Username;
                txtRole.Text = SessionHelper.CurrentUser.Role;
            }

            txtCurrentPassword.Password = "";
            txtNewPassword.Password = "";
            txtConfirmPassword.Password = "";
        }

        private void BtnUpdateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (SessionHelper.CurrentUser == null) return;

            string fullName = txtFullName.Text.Trim();
            string username = txtUsername.Text.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Please enter your full name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtFullName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Please enter a username.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUsername.Focus();
                return;
            }

            if (AuthService.UpdateProfile(SessionHelper.CurrentUser.UserID, fullName, username, out string error))
            {
                SessionHelper.CurrentUser.FullName = fullName;
                SessionHelper.CurrentUser.Username = username;

                // Live-update AdminDashboardWindow sidebar username and avatar initial
                if (Window.GetWindow(this) is AdminDashboardWindow adminWin)
                {
                    adminWin.RefreshCurrentUserHeader();
                }

                AuditLogService.Log("MAINTENANCE", $"Updated user profile: {username} ({fullName})");
                MessageBox.Show("Profile details updated successfully!", "Profile Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Failed to update profile:\n{error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUpdatePassword_Click(object sender, RoutedEventArgs e)
        {
            if (SessionHelper.CurrentUser == null) return;

            string currentPw = txtCurrentPassword.Password;
            string newPw = txtNewPassword.Password;
            string confirmPw = txtConfirmPassword.Password;

            if (string.IsNullOrEmpty(currentPw))
            {
                MessageBox.Show("Please enter your current password.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCurrentPassword.Focus();
                return;
            }

            if (string.IsNullOrEmpty(newPw))
            {
                MessageBox.Show("Please enter a new password.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (newPw.Length < 4)
            {
                MessageBox.Show("New password must be at least 4 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (newPw != confirmPw)
            {
                MessageBox.Show("The new password and confirmation password do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtConfirmPassword.Focus();
                return;
            }

            if (AuthService.ChangePassword(SessionHelper.CurrentUser.UserID, currentPw, newPw))
            {
                txtCurrentPassword.Password = "";
                txtNewPassword.Password = "";
                txtConfirmPassword.Password = "";

                AuditLogService.Log("MAINTENANCE", $"Changed password for user account: {SessionHelper.CurrentUser.Username}");
                MessageBox.Show("Password updated successfully!", "Password Changed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Current password is incorrect.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtCurrentPassword.SelectAll();
                txtCurrentPassword.Focus();
            }
        }
    }
}