using System;
using System.IO;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace BikeKings_System.Database
{
    public static class DatabaseInitializer
    {
        // Connection to MASTER (not BikeKingsDB) to check/create the DB
        private static string MasterConnection =>
            DatabaseHelper.GetMasterConnectionString();

        public static void Initialize()
        {
            try
            {
                if (!DatabaseExists())
                {
                    MessageBox.Show(
                        "First time setup detected.\n" +
                        "Creating database automatically. Please wait...",
                        "Bike Kings Setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    RunSetupScript();

                    MessageBox.Show(
                        "✅ Database created successfully!\n" +
                        "You can now log in.",
                        "Setup Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Setup Error:\n" + ex.Message +
                    "\n\nMake sure SQL Server Express is installed and running.",
                    "Database Setup Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static bool DatabaseExists()
        {
            using (var conn = new SqlConnection(MasterConnection))
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM sys.databases WHERE name = 'BikeKingsDB'";
                using (var cmd = new SqlCommand(sql, conn))
                    return (int)cmd.ExecuteScalar() > 0;
            }
        }

        private static void RunSetupScript()
        {
            // Read the SQL script file
            string scriptPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "setup_database.sql");

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException(
                    "setup_database.sql not found. Make sure it is in the project.");

            string script = File.ReadAllText(scriptPath);

            // Split by GO statements
            string[] batches = script.Split(
                new[] { "\r\nGO", "\nGO", "\r\ngo", "\ngo" },
                StringSplitOptions.RemoveEmptyEntries);

            using (var conn = new SqlConnection(MasterConnection))
            {
                conn.Open();
                foreach (string batch in batches)
                {
                    string trimmed = batch.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    using (var cmd = new SqlCommand(trimmed, conn))
                        cmd.ExecuteNonQuery();
                }
            }
        }
    }
}