using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BikeKings_System.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace BikeKings_System.Views.Admin
{
    public partial class BackupRecoveryControl : UserControl
    {
        public BackupRecoveryControl()
        {
            InitializeComponent();
        }

        // ── 1. BACKUP DATABASE ───────────────────────────────────────────────
        private async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save Database Backup File",
                Filter = "SQL Server Backup (*.bak)|*.bak|All Files (*.*)|*.*",
                FileName = $"BikeKingsDB_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (sfd.ShowDialog() != true) return;

            string targetFile = sfd.FileName;

            // UI feedback
            btnBackup.IsEnabled = false;
            btnRestore.IsEnabled = false;
            pbProgress.Visibility = Visibility.Visible;
            lblStatus.Visibility = Visibility.Visible;
            lblStatus.Text = "Creating database backup, please wait...";

            var (success, error) = await Task.Run(() =>
            {
                // Stage through a safe local folder where SQL Server has verified permissions
                string safeFolder = @"C:\BikeKings_Backups";
                try
                {
                    if (!Directory.Exists(safeFolder)) Directory.CreateDirectory(safeFolder);
                }
                catch { }

                string tempBak = Path.Combine(safeFolder, $"Temp_{Guid.NewGuid():N}.bak");

                try
                {
                    using (var conn = new SqlConnection(DatabaseHelper.GetMasterConnectionString()))
                    {
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandTimeout = 120;
                        cmd.CommandText = "BACKUP DATABASE [BikeKingsDB] TO DISK = @bakPath WITH FORMAT, INIT, NAME = 'BikeKingsDB Full Backup', STATS = 10";
                        cmd.Parameters.AddWithValue("@bakPath", tempBak);
                        cmd.ExecuteNonQuery();
                    }

                    // Copy to the destination chosen by the user
                    File.Copy(tempBak, targetFile, overwrite: true);

                    // Clean up temp
                    try { File.Delete(tempBak); } catch { }

                    AuditLogService.Log("MAINTENANCE", $"Database backup created: {Path.GetFileName(targetFile)}");
                    return (true, "");
                }
                catch (Exception ex)
                {
                    try { if (File.Exists(tempBak)) File.Delete(tempBak); } catch { }
                    return (false, ex.Message);
                }
            });

            btnBackup.IsEnabled = true;
            btnRestore.IsEnabled = true;
            pbProgress.Visibility = Visibility.Collapsed;
            lblStatus.Visibility = Visibility.Collapsed;

            if (success)
            {
                var res = MessageBox.Show(
                    $"Database backup completed successfully!\n\nSaved to:\n{targetFile}\n\nWould you like to open the folder?",
                    "Backup Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    try { Process.Start("explorer.exe", $"/select,\"{targetFile}\""); } catch { }
                }
            }
            else
            {
                MessageBox.Show($"Backup failed:\n{error}", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── 2. RESTORE DATABASE ──────────────────────────────────────────────
        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "WARNING: Restoring the database will overwrite all current system data with the contents of the backup file.\n\nAre you sure you want to proceed?",
                "Confirm Database Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var ofd = new OpenFileDialog
            {
                Title = "Select Database Backup File to Restore",
                Filter = "SQL Server Backup (*.bak)|*.bak|All Files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (ofd.ShowDialog() != true) return;

            string sourceFile = ofd.FileName;

            if (!File.Exists(sourceFile))
            {
                MessageBox.Show("The selected file does not exist.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // UI feedback
            btnBackup.IsEnabled = false;
            btnRestore.IsEnabled = false;
            pbProgress.Visibility = Visibility.Visible;
            lblStatus.Visibility = Visibility.Visible;
            lblStatus.Text = "Restoring database, please wait...";

            var (success, error) = await Task.Run(() =>
            {
                string safeFolder = @"C:\BikeKings_Backups";
                try
                {
                    if (!Directory.Exists(safeFolder)) Directory.CreateDirectory(safeFolder);
                }
                catch { }

                string tempRestore = Path.Combine(safeFolder, $"RestoreTemp_{Guid.NewGuid():N}.bak");

                try
                {
                    File.Copy(sourceFile, tempRestore, overwrite: true);

                    // Disconnect active connections to allow single-user replacement
                    SqlConnection.ClearAllPools();

                    using (var conn = new SqlConnection(DatabaseHelper.GetMasterConnectionString()))
                    {
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandTimeout = 180;
                        cmd.CommandText = @"
                            USE master;
                            ALTER DATABASE [BikeKingsDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            RESTORE DATABASE [BikeKingsDB] FROM DISK = @bakPath WITH REPLACE;
                            ALTER DATABASE [BikeKingsDB] SET MULTI_USER;";
                        cmd.Parameters.AddWithValue("@bakPath", tempRestore);
                        cmd.ExecuteNonQuery();
                    }

                    try { File.Delete(tempRestore); } catch { }
                    SqlConnection.ClearAllPools();

                    AuditLogService.Log("MAINTENANCE", $"Database restored from backup: {Path.GetFileName(sourceFile)}");
                    return (true, "");
                }
                catch (Exception ex)
                {
                    // Safety check: ensure multi-user mode is restored
                    try
                    {
                        using var conn = new SqlConnection(DatabaseHelper.GetMasterConnectionString());
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER DATABASE [BikeKingsDB] SET MULTI_USER;";
                        cmd.ExecuteNonQuery();
                    }
                    catch { }

                    try { if (File.Exists(tempRestore)) File.Delete(tempRestore); } catch { }
                    return (false, ex.Message);
                }
            });

            btnBackup.IsEnabled = true;
            btnRestore.IsEnabled = true;
            pbProgress.Visibility = Visibility.Collapsed;
            lblStatus.Visibility = Visibility.Collapsed;

            if (success)
            {
                MessageBox.Show(
                    "Database restoration was successful!\n\nAll data has been updated to match the selected backup.",
                    "Restore Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Restore failed:\n{error}", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}