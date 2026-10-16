using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace BikeKings_System.Database
{
    public class SupplierRecord
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = "";
        public string ContactPerson { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";
        public bool IsActive { get; set; } = true;

        // ── Consistent Philippine Phone Formatter ──
        public string PhoneFormatted
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Phone) || Phone.Trim() == "-") return "No phone recorded";
                string digits = Regex.Replace(Phone, @"\D", "");
                if (digits.Length == 11 && digits.StartsWith("09"))
                    return $"{digits.Substring(0, 4)}-{digits.Substring(4, 3)}-{digits.Substring(7, 4)}";
                if (digits.Length == 10 && digits.StartsWith("0"))
                    return $"({digits.Substring(0, 2)}) {digits.Substring(2, 4)}-{digits.Substring(6, 4)}";
                return Phone;
            }
        }

        public string StatusText => IsActive ? "ACTIVE" : "INACTIVE";
        public string StatusBgColor => IsActive ? "#132D23" : "#3E161C";
        public string StatusTextColor => IsActive ? "#22C55E" : "#EF4444";
        public string ToggleActionText => IsActive ? "Deactivate" : "Activate";
    }

    public static class SupplierService
    {
        public static List<SupplierRecord> GetSuppliers(string search = "")
        {
            var list = new List<SupplierRecord>();
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                string sql = @"
                    SELECT SupplierID, SupplierName, ContactPerson, Phone, Email, Address, IsActive
                    FROM Suppliers
                    WHERE (@search = '' 
                       OR SupplierName LIKE '%' + @search + '%' 
                       OR ContactPerson LIKE '%' + @search + '%' 
                       OR Address LIKE '%' + @search + '%')
                    ORDER BY SupplierName ASC";

                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@search", search.Trim());

                using var dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new SupplierRecord
                    {
                        SupplierID = Convert.ToInt32(dr["SupplierID"]),
                        SupplierName = dr["SupplierName"].ToString() ?? "",
                        ContactPerson = dr["ContactPerson"]?.ToString() ?? "-",
                        Phone = dr["Phone"]?.ToString() ?? "-",
                        Email = dr["Email"]?.ToString() ?? "-",
                        Address = dr["Address"]?.ToString() ?? "-",
                        IsActive = Convert.ToBoolean(dr["IsActive"])
                    });
                }
            }
            catch { }
            return list;
        }

        public static bool SaveSupplier(SupplierRecord s, out string error)
        {
            error = "";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();

                string sql;
                if (s.SupplierID == 0)
                {
                    sql = @"
                        INSERT INTO Suppliers (SupplierName, ContactPerson, Phone, Email, Address, IsActive)
                        VALUES (@name, @contact, @phone, @email, @address, 1)";
                }
                else
                {
                    sql = @"
                        UPDATE Suppliers 
                        SET SupplierName = @name, ContactPerson = @contact, Phone = @phone, 
                            Email = @email, Address = @address
                        WHERE SupplierID = @id";
                }

                // 🛡️ Prevent Duplicate Supplier Name
                string checkSql = s.SupplierID == 0
                    ? "SELECT COUNT(*) FROM Suppliers WHERE SupplierName = @name AND IsActive = 1"
                    : "SELECT COUNT(*) FROM Suppliers WHERE SupplierName = @name AND SupplierID <> @id AND IsActive = 1";
                using (var checkCmd = new SqlCommand(checkSql, con))
                {
                    checkCmd.Parameters.AddWithValue("@name", s.SupplierName.Trim());
                    if (s.SupplierID != 0) checkCmd.Parameters.AddWithValue("@id", s.SupplierID);
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        error = $"A supplier with the name '{s.SupplierName.Trim()}' already exists.";
                        return false;
                    }
                }

                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@name", s.SupplierName.Trim());
                cmd.Parameters.AddWithValue("@contact", string.IsNullOrWhiteSpace(s.ContactPerson) ? (object)DBNull.Value : s.ContactPerson.Trim());
                cmd.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(s.Phone) ? (object)DBNull.Value : s.Phone.Trim());
                cmd.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(s.Email) ? (object)DBNull.Value : s.Email.Trim());
                cmd.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(s.Address) ? (object)DBNull.Value : s.Address.Trim());
                if (s.SupplierID != 0) cmd.Parameters.AddWithValue("@id", s.SupplierID);

                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool ToggleStatus(int supplierId, bool currentStatus)
        {
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                string sql = "UPDATE Suppliers SET IsActive = @newStatus WHERE SupplierID = @id";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@newStatus", !currentStatus);
                cmd.Parameters.AddWithValue("@id", supplierId);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}