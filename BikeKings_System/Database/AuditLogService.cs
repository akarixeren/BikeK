using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using BikeKings_System.Helpers;

namespace BikeKings_System.Database
{
    public class AuditLogItem
    {
        public int LogID { get; set; }
        public int? UserID { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string ActionType { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime Timestamp { get; set; }

        public string FormattedTimestamp => Timestamp.ToString("MMM dd, yyyy, hh:mm tt");
        public string RoleBadgeText => Role.ToUpper();

        public string RoleBgColor => Role.ToUpper() switch
        {
            "ADMIN" or "ADMINISTRATOR" => "#18233C",
            "CASHIER" => "#153026",
            "STORECLERK" or "STORE CLERK" or "CLERK" => "#261D32",
            _ => "#261D32"
        };

        public string RoleTextColor => Role.ToUpper() switch
        {
            "ADMIN" or "ADMINISTRATOR" => "#3B82F6",
            "CASHIER" => "#10B981",
            "STORECLERK" or "STORE CLERK" or "CLERK" => "#A78BFA",
            _ => "#A78BFA"
        };

        public string ActionBgColor => ActionType.ToUpper() switch
        {
            "LOGIN" => "#332A15",
            "LOGOUT" => "#3B2118",
            "MAINTENANCE" or "CREATE" => "#132D23",
            "INVENTORY" or "UPDATE" => "#122B38",
            "DELETE" or "DEACTIVATE" => "#3E161C",
            "STOCK_IN" or "STOCK-IN" => "#132D23",
            "STOCK_OUT" or "STOCK-OUT" => "#332A15",
            "SUPPLIERS" => "#2E1B38",
            "PURCHASE_ORDER" => "#1B2A38",
            _ => "#1D1926"
        };

        public string ActionTextColor => ActionType.ToUpper() switch
        {
            "LOGIN" => "#EAB308",
            "LOGOUT" => "#F97316",
            "MAINTENANCE" or "CREATE" => "#10B981",
            "INVENTORY" or "UPDATE" => "#06B6D4",
            "DELETE" or "DEACTIVATE" => "#EF4444",
            "STOCK_IN" or "STOCK-IN" => "#22C55E",
            "STOCK_OUT" or "STOCK-OUT" => "#F59E0B",
            "SUPPLIERS" => "#C084FC",
            "PURCHASE_ORDER" => "#38BDF8",
            _ => "#D1CCE0"
        };
    }

    public static class AuditLogService
    {
        public static void Log(string actionType, string details, int? userId = null, string? username = null, string? role = null)
        {
            try
            {
                int? uid = userId ?? SessionHelper.CurrentUser?.UserID;
                string uname = username ?? SessionHelper.CurrentUser?.Username ?? "admin";
                string urole = role ?? SessionHelper.CurrentUser?.Role ?? "Administrator";

                using var con = DatabaseHelper.GetConnection();
                con.Open();
                string sql = @"
                    INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp)
                    VALUES (@uid, @uname, @urole, @action, @details, GETDATE())";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@uid", uid.HasValue ? (object)uid.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@uname", uname);
                cmd.Parameters.AddWithValue("@urole", urole);
                cmd.Parameters.AddWithValue("@action", actionType);
                cmd.Parameters.AddWithValue("@details", details);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static List<AuditLogItem> GetLogs(
            string search = "",
            string actionFilter = "All Actions",
            string roleFilter = "All Roles",
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var list = new List<AuditLogItem>();
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();

                string sql = @"
                    SELECT LogID, UserID, Username, Role, ActionType, Details, Timestamp
                    FROM AuditLogs
                    WHERE (@search = '' OR Username LIKE '%' + @search + '%' OR Details LIKE '%' + @search + '%' OR ActionType LIKE '%' + @search + '%')
                      AND (@action = 'All Actions' OR @action = 'All Logs' OR ActionType = @action)
                      AND (@role = 'All Roles' OR Role = @role)
                      AND (@from IS NULL OR Timestamp >= @from)
                      AND (@to IS NULL OR Timestamp <= @to)
                    ORDER BY Timestamp DESC";

                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@search", search.Trim());
                cmd.Parameters.AddWithValue("@action", actionFilter);
                cmd.Parameters.AddWithValue("@role", roleFilter);
                cmd.Parameters.AddWithValue("@from", fromDate.HasValue ? (object)fromDate.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@to", toDate.HasValue ? (object)toDate.Value : DBNull.Value);

                using var dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new AuditLogItem
                    {
                        LogID = Convert.ToInt32(dr["LogID"]),
                        UserID = dr["UserID"] != DBNull.Value ? Convert.ToInt32(dr["UserID"]) : null,
                        Username = dr["Username"].ToString() ?? "",
                        Role = dr["Role"].ToString() ?? "",
                        ActionType = dr["ActionType"].ToString() ?? "",
                        Details = dr["Details"].ToString() ?? "",
                        Timestamp = Convert.ToDateTime(dr["Timestamp"])
                    });
                }
            }
            catch { }
            return list;
        }
    }
}