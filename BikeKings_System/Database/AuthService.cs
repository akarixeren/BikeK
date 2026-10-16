using System;
using System.Windows;
using Microsoft.Data.SqlClient;
using BikeKings_System.Models;

namespace BikeKings_System.Database
{
    public static class AuthService
    {
        public static UserModel? Login(string username, string password)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    // Strict Case-Sensitivity on BOTH Username and Password
                    string sql = @"SELECT UserID, FullName, Username, Role, Password
                                   FROM Users
                                   WHERE Username = @u COLLATE Latin1_General_CS_AS
                                     AND Password = @p COLLATE Latin1_General_CS_AS
                                     AND IsActive = 1";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", username.Trim());
                        cmd.Parameters.AddWithValue("@p", password);

                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                string dbUsername = r["Username"]?.ToString() ?? "";
                                string dbPassword = r["Password"]?.ToString() ?? "";

                                // Strict C# exact case verification (Reject if casing differs)
                                if (!string.Equals(dbUsername, username.Trim(), StringComparison.Ordinal) ||
                                    !string.Equals(dbPassword, password, StringComparison.Ordinal))
                                {
                                    return null;
                                }

                                return new UserModel
                                {
                                    UserID = Convert.ToInt32(r["UserID"]),
                                    FullName = r["FullName"]?.ToString() ?? "",
                                    Username = dbUsername,
                                    Role = r["Role"]?.ToString() ?? ""
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Database Error:\n" + ex.Message,
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return null;
        }

        public static bool ChangePassword(int userId, string currentPassword, string newPassword)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    // Verify current password with strict case sensitivity
                    string verifySql = "SELECT Password FROM Users WHERE UserID = @id AND IsActive = 1";
                    using (var cmdVerify = new SqlCommand(verifySql, conn))
                    {
                        cmdVerify.Parameters.AddWithValue("@id", userId);
                        var obj = cmdVerify.ExecuteScalar();
                        if (obj == null || !string.Equals(obj.ToString(), currentPassword, StringComparison.Ordinal))
                        {
                            return false;
                        }
                    }

                    // Update to new password
                    string updateSql = "UPDATE Users SET Password = @newPw WHERE UserID = @id";
                    using (var cmdUpdate = new SqlCommand(updateSql, conn))
                    {
                        cmdUpdate.Parameters.AddWithValue("@newPw", newPassword);
                        cmdUpdate.Parameters.AddWithValue("@id", userId);
                        return cmdUpdate.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool UpdateProfile(int userId, string fullName, string username, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(fullName))
            {
                error = "Full Name cannot be empty.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                error = "Username cannot be empty.";
                return false;
            }

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();

                // Check for duplicate username among other active accounts
                string checkSql = "SELECT COUNT(1) FROM Users WHERE Username = @u COLLATE Latin1_General_CI_AS AND UserID <> @id AND IsActive = 1";
                using (var cmdCheck = new SqlCommand(checkSql, conn))
                {
                    cmdCheck.Parameters.AddWithValue("@u", username.Trim());
                    cmdCheck.Parameters.AddWithValue("@id", userId);
                    int count = Convert.ToInt32(cmdCheck.ExecuteScalar());
                    if (count > 0)
                    {
                        error = "The username is already taken by another account.";
                        return false;
                    }
                }

                string sql = "UPDATE Users SET FullName = @fn, Username = @u WHERE UserID = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@fn", fullName.Trim());
                    cmd.Parameters.AddWithValue("@u", username.Trim());
                    cmd.Parameters.AddWithValue("@id", userId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}