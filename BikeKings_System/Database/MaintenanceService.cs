using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace BikeKings_System.Database
{
    public class CategoryModel
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = "";
        public string Description { get; set; } = "";
        public override string ToString() => CategoryName;
    }

    public class BrandModel
    {
        public int BrandID { get; set; }
        public string BrandName { get; set; } = "";
        public string Description { get; set; } = "";
        public override string ToString() => BrandName;
    }

    public class MechanicModel
    {
        public int MechanicID { get; set; }
        public string FullName { get; set; } = "";
        public string MechanicName => FullName;
        public string ContactNumber { get; set; } = "";
        public string Specialization { get; set; } = "";
        public decimal CommissionRate { get; set; } = 80m;
        public string FormattedCommission => $"{CommissionRate:0}% Split";
    }

    public static class MaintenanceService
    {
        // ── CATEGORIES ──
        public static List<string> GetCategories()
        {
            var list = new List<string>();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("SELECT CategoryName FROM Categories WHERE IsActive = 1 ORDER BY CategoryName", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(r["CategoryName"].ToString()!);
            }
            catch { }
            return list;
        }

        public static List<CategoryModel> GetCategoriesList()
        {
            var list = new List<CategoryModel>();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("SELECT CategoryID, CategoryName FROM Categories WHERE IsActive = 1 ORDER BY CategoryName", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new CategoryModel
                    {
                        CategoryID = Convert.ToInt32(r["CategoryID"]),
                        CategoryName = r["CategoryName"].ToString()!
                    });
                }
            }
            catch { }
            return list;
        }

        public static bool AddCategory(string name)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("INSERT INTO Categories (CategoryName, IsActive) VALUES (@name, 1)", conn);
                cmd.Parameters.AddWithValue("@name", name.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool UpdateCategory(int id, string name)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Categories SET CategoryName = @name WHERE CategoryID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@name", name.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool DeleteCategory(int id)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Categories SET IsActive = 0 WHERE CategoryID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ── BRANDS ──
        public static List<string> GetBrands()
        {
            var list = new List<string>();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("SELECT BrandName FROM Brands WHERE IsActive = 1 ORDER BY BrandName", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read()) list.Add(r["BrandName"].ToString()!);
            }
            catch { }
            return list;
        }

        public static List<BrandModel> GetBrandsList()
        {
            var list = new List<BrandModel>();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("SELECT BrandID, BrandName FROM Brands WHERE IsActive = 1 ORDER BY BrandName", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new BrandModel
                    {
                        BrandID = Convert.ToInt32(r["BrandID"]),
                        BrandName = r["BrandName"].ToString()!
                    });
                }
            }
            catch { }
            return list;
        }

        public static bool AddBrand(string name)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("INSERT INTO Brands (BrandName, IsActive) VALUES (@name, 1)", conn);
                cmd.Parameters.AddWithValue("@name", name.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool UpdateBrand(int id, string name)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Brands SET BrandName = @name WHERE BrandID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@name", name.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool DeleteBrand(int id)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Brands SET IsActive = 0 WHERE BrandID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ── SERVICES & LABOR FEES ──
        public static DataTable GetServices()
        {
            var dt = new DataTable();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var da = new SqlDataAdapter("SELECT ServiceID, ServiceName, LaborFee FROM Services WHERE IsActive = 1 ORDER BY ServiceName", conn);
                da.Fill(dt);
            }
            catch { }
            return dt;
        }

        public static bool AddService(string name, decimal fee)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("INSERT INTO Services (ServiceName, LaborFee, IsActive) VALUES (@name, @fee, 1)", conn);
                cmd.Parameters.AddWithValue("@name", name.Trim());
                cmd.Parameters.AddWithValue("@fee", fee);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool UpdateService(int id, string name, decimal fee)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Services SET ServiceName = @name, LaborFee = @fee WHERE ServiceID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@name", name.Trim());
                cmd.Parameters.AddWithValue("@fee", fee);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool DeleteService(int id)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Services SET IsActive = 0 WHERE ServiceID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ── MECHANICS (STANDALONE TECHNICIANS) ──
        public static List<MechanicModel> GetMechanicsList()
        {
            var list = new List<MechanicModel>();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                string sql = @"SELECT MechanicID, FullName, ISNULL(ContactNumber,'') AS ContactNumber, 
                                      ISNULL(Specialization,'General Mechanic') AS Specialization,
                                      ISNULL(CommissionRate, 80.00) AS CommissionRate 
                               FROM Mechanics WHERE IsActive = 1 ORDER BY FullName";
                using var cmd = new SqlCommand(sql, conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new MechanicModel
                    {
                        MechanicID = Convert.ToInt32(r["MechanicID"]),
                        FullName = r["FullName"].ToString()!,
                        ContactNumber = r["ContactNumber"].ToString()!,
                        Specialization = r["Specialization"].ToString()!,
                        CommissionRate = Convert.ToDecimal(r["CommissionRate"])
                    });
                }
            }
            catch { }
            return list;
        }

        public static bool AddMechanic(string fullName, decimal commissionRate = 80m, string contact = "", string spec = "General Mechanic")
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                string sql = "INSERT INTO Mechanics (FullName, ContactNumber, Specialization, CommissionRate, IsActive) VALUES (@fn, @cn, @sp, @cr, 1)";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@fn", fullName.Trim());
                cmd.Parameters.AddWithValue("@cn", string.IsNullOrWhiteSpace(contact) ? DBNull.Value : (object)contact.Trim());
                cmd.Parameters.AddWithValue("@sp", string.IsNullOrWhiteSpace(spec) ? "General Repair" : (object)spec.Trim());
                cmd.Parameters.AddWithValue("@cr", commissionRate);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool UpdateMechanic(int id, string fullName, decimal commissionRate = 80m, string contact = "", string spec = "General Mechanic")
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                string sql = "UPDATE Mechanics SET FullName = @fn, ContactNumber = @cn, Specialization = @sp, CommissionRate = @cr WHERE MechanicID = @id";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@fn", fullName.Trim());
                cmd.Parameters.AddWithValue("@cn", string.IsNullOrWhiteSpace(contact) ? DBNull.Value : (object)contact.Trim());
                cmd.Parameters.AddWithValue("@sp", string.IsNullOrWhiteSpace(spec) ? "General Repair" : (object)spec.Trim());
                cmd.Parameters.AddWithValue("@cr", commissionRate);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool DeleteMechanic(int id)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Mechanics SET IsActive = 0 WHERE MechanicID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        // ── STAFF USERS (SYSTEM LOGIN ACCOUNTS) ──
        public static DataTable GetStaffUsers()
        {
            var dt = new DataTable();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var da = new SqlDataAdapter("SELECT UserID, FullName, Username, Role FROM Users WHERE IsActive = 1 ORDER BY UserID", conn);
                da.Fill(dt);
            }
            catch { }
            return dt;
        }

        public static bool AddUser(string fullName, string username, string password, string role)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("INSERT INTO Users (FullName, Username, Password, Role, IsActive) VALUES (@fn, @u, @p, @r, 1)", conn);
                cmd.Parameters.AddWithValue("@fn", fullName.Trim());
                cmd.Parameters.AddWithValue("@u", username.Trim());
                cmd.Parameters.AddWithValue("@p", password);
                cmd.Parameters.AddWithValue("@r", role);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public static bool DeactivateUser(int userId)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Users SET IsActive = 0 WHERE UserID = @id", conn);
                cmd.Parameters.AddWithValue("@id", userId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }
    }
}