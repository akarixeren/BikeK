using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using BikeKings_System.Helpers;

namespace BikeKings_System.Database
{
    public class ReceiptLineItem
    {
        public int ItemNo { get; set; } = 1;
        public string Description { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string FormattedUnitPrice => $"₱{UnitPrice:N2}";
        public string FormattedAmount => $"₱{Subtotal:N2}";
        public string FormattedQtyPrice => Quantity > 1 ? $"{Quantity} x ₱{UnitPrice:N2}" : "";
    }

    public class ReceiptFullDetails
    {
        public string InvoiceNo { get; set; } = "";
        public string TransactionType { get; set; } = "Sale";
        public DateTime TransactionDate { get; set; }
        public string CustomerName { get; set; } = "Walk-in Customer";
        public string ContactNumber { get; set; } = "N/A";
        public string MotorcycleUnit { get; set; } = "N/A";
        public string PlateNumber { get; set; } = "N/A";
        public string CashierName { get; set; } = "Admin";
        public string MechanicName { get; set; } = "N/A";
        public string PaymentMethod { get; set; } = "Cash";
        public string PaymentRef { get; set; } = "";
        public string Status { get; set; } = "Completed";
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<ReceiptLineItem> Items { get; set; } = new List<ReceiptLineItem>();

        public string FormattedDate => TransactionDate.ToString("MMM dd, yyyy, hh:mm tt");
        public string FormattedSubtotal => $"₱{Subtotal:N2}";
        public string FormattedDiscount => $"-₱{Discount:N2}";
        public string FormattedTotal => $"₱{TotalAmount:N2}";
    }

    public class TransactionItem
    {
        public int TransactionID { get; set; }
        public string InvoiceNo { get; set; } = "";
        public string TransactionType { get; set; } = "Sale";
        public DateTime TransactionDate { get; set; }
        public string CustomerName { get; set; } = "Walk-in Customer";
        public string CashierName { get; set; } = "Admin";
        public string MechanicName { get; set; } = "N/A";
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string Status { get; set; } = "Completed";

        public string FormattedDate => TransactionDate.ToString("yyyy-MM-dd hh:mm tt");
        public string FormattedTotal => $"₱{TotalAmount:N2}";
        public string TypeBadge => TransactionType == "JobOrder" ? "🔧 Job Order" : "🛒 Retail Sale";

        public string StatusBgColor => Status.Equals("Voided", StringComparison.OrdinalIgnoreCase) ? "#3E161C" : "#132D23";
        public string StatusTextColor => Status.Equals("Voided", StringComparison.OrdinalIgnoreCase) ? "#EF4444" : "#22C55E";
        public bool IsCompleted => Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);
    }

    public static class SalesService
    {
        public static List<TransactionItem> GetTransactions(string search = "", string typeFilter = "All Types", string statusFilter = "All Statuses", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var list = new List<TransactionItem>();

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    string sql = @"
                        SELECT * FROM (
                            SELECT 
                                s.SaleID AS TransID,
                                s.InvoiceNo,
                                'Sale' AS TransType,
                                s.SaleDate AS TransDate,
                                ISNULL(s.CustomerName, 'Walk-in Customer') AS CustomerName,
                                ISNULL(u.FullName, 'Admin') AS CashierName,
                                'N/A' AS MechanicName,
                                s.SubTotal,
                                s.Discount,
                                s.TotalAmount,
                                ISNULL(s.PaymentMethod, 'Cash') AS PaymentMethod,
                                ISNULL(s.Status, 'Completed') AS Status
                            FROM Sales s
                            LEFT JOIN Users u ON s.UserID = u.UserID

                            UNION ALL

                            SELECT 
                                j.JobOrderID AS TransID,
                                j.JobOrderNo AS InvoiceNo,
                                'JobOrder' AS TransType,
                                j.CreatedAt AS TransDate,
                                ISNULL(j.CustomerName, 'Walk-in Customer') AS CustomerName,
                                ISNULL(u.FullName, 'Admin') AS CashierName,
                                ISNULL(m.FullName, 'Unassigned') AS MechanicName,
                                (j.TotalLabor + j.TotalParts) AS Subtotal,
                                0.00 AS Discount,
                                j.TotalAmount,
                                ISNULL(j.PaymentMethod, 'Cash') AS PaymentMethod,
                                ISNULL(j.Status, 'Completed') AS Status
                            FROM JobOrders j
                            LEFT JOIN Users u ON j.CashierID = u.UserID
                            LEFT JOIN Mechanics m ON j.MechanicID = m.MechanicID
                        ) AS AllTrans
                        ORDER BY TransDate DESC;
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new TransactionItem
                            {
                                TransactionID = Convert.ToInt32(reader["TransID"]),
                                InvoiceNo = reader["InvoiceNo"].ToString() ?? "",
                                TransactionType = reader["TransType"].ToString() ?? "Sale",
                                TransactionDate = reader["TransDate"] != DBNull.Value ? Convert.ToDateTime(reader["TransDate"]) : DateTime.Now,
                                CustomerName = reader["CustomerName"].ToString() ?? "Walk-in Customer",
                                CashierName = reader["CashierName"].ToString() ?? "Admin",
                                MechanicName = reader["MechanicName"].ToString() ?? "N/A",
                                Subtotal = reader["SubTotal"] != DBNull.Value ? Convert.ToDecimal(reader["SubTotal"]) : 0,
                                Discount = reader["Discount"] != DBNull.Value ? Convert.ToDecimal(reader["Discount"]) : 0,
                                TotalAmount = reader["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAmount"]) : 0,
                                PaymentMethod = reader["PaymentMethod"].ToString() ?? "Cash",
                                Status = reader["Status"].ToString() ?? "Completed"
                            };

                            bool matchSearch = string.IsNullOrWhiteSpace(search) ||
                                               item.InvoiceNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                               item.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                               item.CashierName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                               item.MechanicName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                               item.PaymentMethod.Contains(search, StringComparison.OrdinalIgnoreCase);

                            bool matchType = typeFilter == "All Types" || typeFilter == "All" ||
                                             (typeFilter == "Sales" && item.TransactionType == "Sale") ||
                                             (typeFilter == "Job Orders" && item.TransactionType == "JobOrder");

                            bool matchStatus = statusFilter == "All Statuses" || statusFilter == "All" ||
                                               item.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase);

                            bool matchDate = true;
                            if (fromDate.HasValue)
                                matchDate = matchDate && item.TransactionDate.Date >= fromDate.Value.Date;
                            if (toDate.HasValue)
                                matchDate = matchDate && item.TransactionDate.Date <= toDate.Value.Date;

                            if (matchSearch && matchType && matchStatus && matchDate)
                            {
                                list.Add(item);
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        public static ReceiptFullDetails GetReceiptDetails(string invoiceNo, string type)
        {
            var receipt = new ReceiptFullDetails { InvoiceNo = invoiceNo, TransactionType = type };

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    if (type == "JobOrder")
                    {
                        string sqlJob = @"
                            SELECT j.JobOrderID, j.JobOrderNo, j.CustomerName, j.ContactNumber, j.MotorcycleUnit, j.PlateNumber,
                                   j.TotalLabor, j.TotalParts, j.TotalAmount, j.CreatedAt, j.PaymentMethod, j.PaymentRef, j.Status,
                                   ISNULL(u.FullName, 'Admin') AS CashierName,
                                   ISNULL(m.FullName, 'Unassigned') AS MechanicName
                            FROM JobOrders j
                            LEFT JOIN Users u ON j.CashierID = u.UserID
                            LEFT JOIN Mechanics m ON j.MechanicID = m.MechanicID
                            WHERE j.JobOrderNo = @inv";

                        int jobOrderId = 0;
                        using (var cmd = new SqlCommand(sqlJob, conn))
                        {
                            cmd.Parameters.AddWithValue("@inv", invoiceNo);
                            using (var r = cmd.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    jobOrderId = Convert.ToInt32(r["JobOrderID"]);
                                    receipt.CustomerName = r["CustomerName"]?.ToString() ?? "Walk-in Customer";
                                    receipt.ContactNumber = r["ContactNumber"]?.ToString() ?? "N/A";
                                    receipt.MotorcycleUnit = r["MotorcycleUnit"]?.ToString() ?? "N/A";
                                    receipt.PlateNumber = r["PlateNumber"]?.ToString() ?? "N/A";
                                    receipt.PaymentMethod = r["PaymentMethod"]?.ToString() ?? "Cash";
                                    receipt.PaymentRef = r["PaymentRef"]?.ToString() ?? "";
                                    receipt.Status = r["Status"]?.ToString() ?? "Completed";
                                    receipt.TransactionDate = Convert.ToDateTime(r["CreatedAt"]);
                                    receipt.CashierName = r["CashierName"]?.ToString() ?? "Admin";
                                    receipt.MechanicName = r["MechanicName"]?.ToString() ?? "Unassigned";
                                    receipt.Subtotal = Convert.ToDecimal(r["TotalLabor"]) + Convert.ToDecimal(r["TotalParts"]);
                                    receipt.Discount = 0;
                                    receipt.TotalAmount = Convert.ToDecimal(r["TotalAmount"]);

                                    // Extract ref if embedded e.g. "E-Wallet (GC-123456)"
                                    if (string.IsNullOrWhiteSpace(receipt.PaymentRef) && receipt.PaymentMethod.Contains("(") && receipt.PaymentMethod.EndsWith(")"))
                                    {
                                        int start = receipt.PaymentMethod.IndexOf('(');
                                        receipt.PaymentRef = receipt.PaymentMethod.Substring(start + 1).TrimEnd(')').Trim();
                                        receipt.PaymentMethod = receipt.PaymentMethod.Substring(0, start).Trim();
                                    }
                                }
                            }
                        }

                        if (jobOrderId > 0)
                        {
                            string sqlItems = "SELECT ItemType, ItemName, Quantity, UnitPrice, Subtotal FROM JobOrderItems WHERE JobOrderID = @jid";
                            using (var cmdItems = new SqlCommand(sqlItems, conn))
                            {
                                cmdItems.Parameters.AddWithValue("@jid", jobOrderId);
                                using (var r = cmdItems.ExecuteReader())
                                {
                                    int counter = 1;
                                    while (r.Read())
                                    {
                                        string itype = r["ItemType"]?.ToString() ?? "Part";
                                        string iname = r["ItemName"]?.ToString() ?? "Item";
                                        string prefix = itype.Equals("Service", StringComparison.OrdinalIgnoreCase) || itype.Equals("Labor", StringComparison.OrdinalIgnoreCase)
                                            ? "[Service Labor] " : "";

                                        receipt.Items.Add(new ReceiptLineItem
                                        {
                                            ItemNo = counter++,
                                            Description = $"{prefix}{iname}",
                                            Quantity = Convert.ToInt32(r["Quantity"]),
                                            UnitPrice = Convert.ToDecimal(r["UnitPrice"]),
                                            Subtotal = Convert.ToDecimal(r["Subtotal"])
                                        });
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        string sqlSale = @"
                            SELECT s.SaleID, s.InvoiceNo, s.CustomerName, s.SaleDate, s.SubTotal, s.Discount, s.TotalAmount, s.PaymentMethod, s.PaymentRef, s.Status,
                                   ISNULL(u.FullName, 'Admin') AS CashierName
                            FROM Sales s
                            LEFT JOIN Users u ON s.UserID = u.UserID
                            WHERE s.InvoiceNo = @inv";

                        int saleId = 0;
                        using (var cmd = new SqlCommand(sqlSale, conn))
                        {
                            cmd.Parameters.AddWithValue("@inv", invoiceNo);
                            using (var r = cmd.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    saleId = Convert.ToInt32(r["SaleID"]);
                                    receipt.CustomerName = r["CustomerName"]?.ToString() ?? "Walk-in Customer";
                                    receipt.TransactionDate = Convert.ToDateTime(r["SaleDate"]);
                                    receipt.CashierName = r["CashierName"]?.ToString() ?? "Admin";
                                    receipt.MechanicName = "N/A";
                                    receipt.Subtotal = Convert.ToDecimal(r["SubTotal"]);
                                    receipt.Discount = Convert.ToDecimal(r["Discount"]);
                                    receipt.TotalAmount = Convert.ToDecimal(r["TotalAmount"]);
                                    receipt.PaymentMethod = r["PaymentMethod"]?.ToString() ?? "Cash";
                                    receipt.PaymentRef = r["PaymentRef"]?.ToString() ?? "";
                                    receipt.Status = r["Status"]?.ToString() ?? "Completed";

                                    // Extract ref if embedded e.g. "E-Wallet (GC-123456)"
                                    if (string.IsNullOrWhiteSpace(receipt.PaymentRef) && receipt.PaymentMethod.Contains("(") && receipt.PaymentMethod.EndsWith(")"))
                                    {
                                        int start = receipt.PaymentMethod.IndexOf('(');
                                        receipt.PaymentRef = receipt.PaymentMethod.Substring(start + 1).TrimEnd(')').Trim();
                                        receipt.PaymentMethod = receipt.PaymentMethod.Substring(0, start).Trim();
                                    }
                                }
                            }
                        }

                        if (saleId > 0)
                        {
                            string sqlDetails = @"
                                SELECT ISNULL(p.ProductName, 'Item') AS ProdName, sd.Quantity, sd.UnitPrice, sd.Subtotal 
                                FROM SalesDetails sd
                                LEFT JOIN Products p ON sd.ProductID = p.ProductID
                                WHERE sd.SaleID = @sid";

                            using (var cmdDetails = new SqlCommand(sqlDetails, conn))
                            {
                                cmdDetails.Parameters.AddWithValue("@sid", saleId);
                                using (var r = cmdDetails.ExecuteReader())
                                {
                                    int counter = 1;
                                    while (r.Read())
                                    {
                                        receipt.Items.Add(new ReceiptLineItem
                                        {
                                            ItemNo = counter++,
                                            Description = r["ProdName"]?.ToString() ?? "Product Item",
                                            Quantity = Convert.ToInt32(r["Quantity"]),
                                            UnitPrice = Convert.ToDecimal(r["UnitPrice"]),
                                            Subtotal = Convert.ToDecimal(r["Subtotal"])
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return receipt;
        }

        public static bool VoidTransaction(string invoiceNo, string type, string reason, int userId, string username, string role)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            if (type == "Sale")
                            {
                                string restoreSql = @"
                                    UPDATE p
                                    SET p.StockQuantity = p.StockQuantity + sd.Quantity
                                    FROM Products p
                                    INNER JOIN SalesDetails sd ON p.ProductID = sd.ProductID
                                    INNER JOIN Sales s ON sd.SaleID = s.SaleID
                                    WHERE s.InvoiceNo = @Inv";

                                using (var cmdRestore = new SqlCommand(restoreSql, conn, trans))
                                {
                                    cmdRestore.Parameters.AddWithValue("@Inv", invoiceNo);
                                    cmdRestore.ExecuteNonQuery();
                                }

                                string updateSql = "UPDATE Sales SET Status = 'Voided' WHERE InvoiceNo = @Inv";
                                using (var cmdUpdate = new SqlCommand(updateSql, conn, trans))
                                {
                                    cmdUpdate.Parameters.AddWithValue("@Inv", invoiceNo);
                                    cmdUpdate.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                string restoreSql = @"
                                    UPDATE p
                                    SET p.StockQuantity = p.StockQuantity + joi.Quantity
                                    FROM Products p
                                    INNER JOIN JobOrderItems joi ON p.ProductID = joi.ProductID
                                    INNER JOIN JobOrders j ON joi.JobOrderID = j.JobOrderID
                                    WHERE j.JobOrderNo = @Inv AND joi.ItemType = 'Product' AND joi.ProductID IS NOT NULL";

                                using (var cmdRestore = new SqlCommand(restoreSql, conn, trans))
                                {
                                    cmdRestore.Parameters.AddWithValue("@Inv", invoiceNo);
                                    cmdRestore.ExecuteNonQuery();
                                }

                                string updateSql = "UPDATE JobOrders SET Status = 'Voided' WHERE JobOrderNo = @Inv";
                                using (var cmdUpdate = new SqlCommand(updateSql, conn, trans))
                                {
                                    cmdUpdate.Parameters.AddWithValue("@Inv", invoiceNo);
                                    cmdUpdate.ExecuteNonQuery();
                                }
                            }

                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }

                AuditLogService.Log("VOID_TRANSACTION", $"Voided {type} #{invoiceNo} and restored product inventory. Reason: {reason}", userId, username, role);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool VoidTransaction(string invoiceNo, string type, out string error)
        {
            error = string.Empty;
            try
            {
                int userId = SessionHelper.CurrentUser?.UserID ?? 1;
                string username = SessionHelper.CurrentUser?.Username ?? "Admin";
                string role = SessionHelper.CurrentUser?.Role ?? "Administrator";
                string reason = "Voided via Sales & Job Orders Control";

                bool success = VoidTransaction(invoiceNo, type, reason, userId, username, role);
                if (!success)
                {
                    error = "An error occurred while updating database transaction records.";
                }
                return success;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            return false;
        }
    }
}