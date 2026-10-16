using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;

namespace BikeKings_System.Database
{
    public class POSCartItem : INotifyPropertyChanged
    {
        private int _quantity = 1;
        public int ItemID { get; set; }
        public string ItemType { get; set; } = "Product";
        public string Barcode { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int StockAvailable { get; set; } = 999;

        public string ImagePath { get; set; } = "";
        public string CategoryIcon => Helpers.ProductImageHelper.GetCategoryIcon(Description, ItemType);
        public string DisplayImage => Helpers.ProductImageHelper.ResolveImagePath(ImagePath);
        public bool HasImage => !string.IsNullOrWhiteSpace(DisplayImage);

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                    OnPropertyChanged(nameof(Subtotal));
                    OnPropertyChanged(nameof(FormattedSubtotal));
                }
            }
        }

        public decimal Subtotal => Quantity * UnitPrice;
        public string FormattedUnitPrice => $"₱{UnitPrice:N2}";
        public string FormattedSubtotal => $"₱{Subtotal:N2}";
        public string TypeBadge => ItemType == "Service" ? "🔧 Service" : "📦 Part";
        public string TypeBadgeBg => ItemType == "Service" ? "#31183B" : "#1A2634";
        public string TypeBadgeFg => ItemType == "Service" ? "#E879F9" : "#60A5FA";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class POSSearchItem
    {
        public int ID { get; set; }
        public string ItemType { get; set; } = "Product";
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public decimal Price { get; set; }
        public string ImagePath { get; set; } = "";
        public string CategoryName { get; set; } = "General";
        public string CategoryIcon => Helpers.ProductImageHelper.GetCategoryIcon(Name, ItemType);
        public string DisplayImage => Helpers.ProductImageHelper.ResolveImagePath(ImagePath, Name, ItemType);
        public bool HasImage => !string.IsNullOrWhiteSpace(DisplayImage);
        public int Stock { get; set; }
        public string StockStatus => ItemType == "Service" ? "SERVICE" : (Stock <= 0 ? "Out of Stock" : (Stock <= 5 ? $"{Stock} left" : $"{Stock} in stock"));
        public string StockBgColor => ItemType == "Service" ? "#1A2536" : (Stock <= 0 ? "#2D1214" : (Stock <= 5 ? "#2D2214" : "#0E2A1E"));
        public string StockFgColor => ItemType == "Service" ? "#38BDF8" : (Stock <= 0 ? "#EF4444" : (Stock <= 5 ? "#F59E0B" : "#10B981"));

        public string StockLabel => ItemType == "Service"
    ? "Service (Ready)"
    : (Stock <= 0 ? "Out of Stock" : $"{Stock} units left");
        public string FormattedPrice => $"₱{Price:N2}";
        public string DisplayText => ItemType == "Service"
            ? $"[SERVICE] {Name} — ₱{Price:N2}"
            : $"[PART] {Name} ({Code}) — ₱{Price:N2} | Stock: {Stock}";
        public string SubText => ItemType == "Service" ? "Labor Service" : $"SKU: {Code} • Stock: {Stock} available";
    }

    public class LaborServiceModel
    {
        public int ServiceID { get; set; }
        public string ServiceName { get; set; } = "";
        public decimal LaborFee { get; set; }
        public string FormattedFee => $"₱{LaborFee:N2}";
        public string DisplayChip => $"🔧 {ServiceName} ({FormattedFee})";
    }

    public class DailyAuditReport
    {
        public string ReportType { get; set; } = "X-Report (Mid-Day)";
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string CashierName { get; set; } = "Ruby";
        public int TotalTransactions { get; set; }
        public int RetailSalesCount { get; set; }
        public int JobOrdersCount { get; set; }
        public int VoidedCount { get; set; }
        public decimal TotalGrossRevenue { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal TotalNetRevenue { get; set; }
        public decimal CashSales { get; set; }
        public decimal GCashSales { get; set; }
        public decimal MayaSales { get; set; }
        public decimal VoidedAmount { get; set; }

        public string FormattedDate => GeneratedAt.ToString("MMMM dd, yyyy hh:mm:ss tt");
        public string FormattedNetRevenue => $"₱{TotalNetRevenue:N2}";
        public string FormattedCashSales => $"₱{CashSales:N2}";
        public string FormattedGCashSales => $"₱{GCashSales:N2}";
        public string FormattedMayaSales => $"₱{MayaSales:N2}";
        public string FormattedDiscounts => $"₱{TotalDiscounts:N2}";
        public string FormattedVoided => $"₱{VoidedAmount:N2}";
    }

    public static class CashierService
    {
        public static List<LaborServiceModel> GetActiveServices()
        {
            var list = new List<LaborServiceModel>();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                string sql = "SELECT ServiceID, ServiceName, LaborFee FROM Services WHERE IsActive = 1 ORDER BY ServiceName";
                using var cmd = new SqlCommand(sql, conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new LaborServiceModel
                    {
                        ServiceID = Convert.ToInt32(r["ServiceID"]),
                        ServiceName = r["ServiceName"].ToString() ?? "",
                        LaborFee = Convert.ToDecimal(r["LaborFee"])
                    });
                }
            }
            catch { }
            return list;
        }

            /// <summary>
            /// Retrieves the real-time available stock quantity for a product directly from the database.
            /// </summary>
        public static int GetProductStock(int productId)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                string sql = "SELECT ISNULL(StockQuantity, 0) FROM Products WHERE ProductID = @pid AND IsActive = 1";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@pid", productId);
                var obj = cmd.ExecuteScalar();
                if (obj != null && obj != DBNull.Value)
                {
                    return Convert.ToInt32(obj);
                }
            }
            catch { }
            return 0;
        }

        public static List<POSSearchItem> SearchItems(string term)
        {
            var results = new List<POSSearchItem>();
            term = (term ?? "").Trim();
            if (string.IsNullOrWhiteSpace(term)) return results;

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();

                // 1. Search Products
                string prodSql = @"
                    SELECT TOP 15 ProductID, Barcode, ProductName, SellingPrice, StockQuantity,
                           ISNULL(ImagePath, '') AS ImagePath
                    FROM Products
                    WHERE IsActive = 1 AND (ProductName LIKE @term OR Barcode LIKE @term)
                    ORDER BY ProductName";

                using (var cmd = new SqlCommand(prodSql, conn))
                {
                    cmd.Parameters.AddWithValue("@term", $"%{term}%");
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        results.Add(new POSSearchItem
                        {
                            ID = Convert.ToInt32(r["ProductID"]),
                            ItemType = "Product",
                            Name = r["ProductName"].ToString() ?? "",
                            Code = r["Barcode"].ToString() ?? "",
                            Price = Convert.ToDecimal(r["SellingPrice"]),
                            Stock = Convert.ToInt32(r["StockQuantity"]),
                            ImagePath = r["ImagePath"]?.ToString() ?? ""
                        });
                    }
                }

                // 2. Search Services
                string srvSql = @"
                    SELECT TOP 10 ServiceID, ServiceName, LaborFee
                    FROM Services
                    WHERE IsActive = 1 AND ServiceName LIKE @term
                    ORDER BY ServiceName";

                using (var cmdSrv = new SqlCommand(srvSql, conn))
                {
                    cmdSrv.Parameters.AddWithValue("@term", $"%{term}%");
                    using var rSrv = cmdSrv.ExecuteReader();
                    while (rSrv.Read())
                    {
                        results.Add(new POSSearchItem
                        {
                            ID = Convert.ToInt32(rSrv["ServiceID"]),
                            ItemType = "Service",
                            Name = rSrv["ServiceName"].ToString() ?? "",
                            Code = "SRV",
                            Price = Convert.ToDecimal(rSrv["LaborFee"]),
                            Stock = 999
                        });
                    }
                }
            }
            catch { }

            return results;
        }

        public static List<string> GetMechanics()
        {
            var list = new List<string> { "Unassigned" };
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                string sql = "SELECT FullName FROM Mechanics WHERE IsActive = 1 ORDER BY FullName";
                using var cmd = new SqlCommand(sql, conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string name = r["FullName"].ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(name) && !list.Contains(name))
                    {
                        list.Add(name);
                    }
                }
            }
            catch { }
            return list;
        }

        public static (bool Success, string ReferenceNo, string TransType, string ErrorMessage) ProcessCheckout(
            List<POSCartItem> cartItems,
            string customerName,
            string contactNo,
            string mcBrand,
            string mcModel,
            string plateNo,
            string mechanicName,
            decimal discount,
            string paymentMethod,
            decimal tenderedAmount,
            int cashierUserId,
            string cashierName,
            string cashierRole)
        {
            if (cartItems == null || cartItems.Count == 0)
                return (false, "", "", "Cart is empty.");

            decimal subtotal = 0;
            decimal totalLabor = 0;
            decimal totalParts = 0;
            bool hasServices = false;

            foreach (var item in cartItems)
            {
                subtotal += item.Subtotal;
                if (item.ItemType == "Service")
                {
                    hasServices = true;
                    totalLabor += item.Subtotal;
                }
                else
                {
                    totalParts += item.Subtotal;
                }
            }

            decimal grandTotal = Math.Max(0, subtotal - discount);
            decimal changeDue = Math.Max(0, tenderedAmount - grandTotal);

            bool isJobOrder = hasServices ||
                              !string.IsNullOrWhiteSpace(plateNo) ||
                              !string.IsNullOrWhiteSpace(mcModel) ||
                              (!string.IsNullOrWhiteSpace(mechanicName) && mechanicName != "Unassigned");

            string cleanCust = string.IsNullOrWhiteSpace(customerName) ? "Walk-in Customer" : customerName.Trim();
            string cleanContact = string.IsNullOrWhiteSpace(contactNo) ? "N/A" : contactNo.Trim();
            string cleanMc = string.IsNullOrWhiteSpace(mcBrand) && string.IsNullOrWhiteSpace(mcModel) ? "N/A" : $"{mcBrand} {mcModel}".Trim();
            string cleanPlate = string.IsNullOrWhiteSpace(plateNo) ? "N/A" : plateNo.Trim().ToUpper();

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                using var trans = conn.BeginTransaction();

                try
                {
                    string generatedRef = "";
                    string transType = isJobOrder ? "JobOrder" : "Sale";

                    if (isJobOrder)
                    {
                        string todayStr = DateTime.Now.ToString("yyyyMMdd");
                        string countSql = "SELECT COUNT(*) + 1 FROM JobOrders WHERE CONVERT(date, CreatedAt) = CONVERT(date, GETDATE())";
                        int seq = 1;
                        using (var cmdSeq = new SqlCommand(countSql, conn, trans))
                        {
                            seq = Convert.ToInt32(cmdSeq.ExecuteScalar());
                        }
                        generatedRef = $"JO-{todayStr}-{seq:D4}";

                        int? mechId = null;
                        if (!string.IsNullOrWhiteSpace(mechanicName) && mechanicName != "Unassigned")
                        {
                            string mechSql = "SELECT TOP 1 MechanicID FROM Mechanics WHERE FullName = @fn AND IsActive = 1";
                            using var cmdMech = new SqlCommand(mechSql, conn, trans);
                            cmdMech.Parameters.AddWithValue("@fn", mechanicName);
                            var obj = cmdMech.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value) mechId = Convert.ToInt32(obj);
                        }

                        string insertJobSql = @"
                            INSERT INTO JobOrders 
                            (JobOrderNo, CustomerName, ContactNumber, MotorcycleUnit, PlateNumber, MechanicID, CashierID, TotalLabor, TotalParts, TotalAmount, Status, CreatedAt, CompletedAt, PaymentMethod)
                            VALUES 
                            (@jno, @cname, @contact, @mc, @plate, @mid, @cid, @labor, @parts, @tot, 'Completed', GETDATE(), GETDATE(), @pay);
                            SELECT SCOPE_IDENTITY();";

                        int newJobId = 0;
                        using (var cmdJob = new SqlCommand(insertJobSql, conn, trans))
                        {
                            cmdJob.Parameters.AddWithValue("@jno", generatedRef);
                            cmdJob.Parameters.AddWithValue("@cname", cleanCust);
                            cmdJob.Parameters.AddWithValue("@contact", cleanContact);
                            cmdJob.Parameters.AddWithValue("@mc", cleanMc);
                            cmdJob.Parameters.AddWithValue("@plate", cleanPlate);
                            cmdJob.Parameters.AddWithValue("@mid", (object?)mechId ?? DBNull.Value);
                            cmdJob.Parameters.AddWithValue("@cid", cashierUserId);
                            cmdJob.Parameters.AddWithValue("@labor", totalLabor);
                            cmdJob.Parameters.AddWithValue("@parts", totalParts);
                            cmdJob.Parameters.AddWithValue("@tot", grandTotal);
                            cmdJob.Parameters.AddWithValue("@pay", paymentMethod);
                            newJobId = Convert.ToInt32(cmdJob.ExecuteScalar());
                        }

                        foreach (var itm in cartItems)
                        {
                            string insertItemSql = @"
                                INSERT INTO JobOrderItems (JobOrderID, ItemType, ServiceID, ProductID, ItemName, Quantity, UnitPrice, Subtotal)
                                VALUES (@jid, @type, @sid, @pid, @name, @qty, @uprice, @subtot)";

                            using (var cmdItm = new SqlCommand(insertItemSql, conn, trans))
                            {
                                cmdItm.Parameters.AddWithValue("@jid", newJobId);
                                cmdItm.Parameters.AddWithValue("@type", itm.ItemType);
                                cmdItm.Parameters.AddWithValue("@sid", itm.ItemType == "Service" && itm.ItemID > 0 ? (object)itm.ItemID : DBNull.Value);
                                cmdItm.Parameters.AddWithValue("@pid", itm.ItemType == "Product" ? (object)itm.ItemID : DBNull.Value);
                                cmdItm.Parameters.AddWithValue("@name", itm.Description);
                                cmdItm.Parameters.AddWithValue("@qty", itm.Quantity);
                                cmdItm.Parameters.AddWithValue("@uprice", itm.UnitPrice);
                                cmdItm.Parameters.AddWithValue("@subtot", itm.Subtotal);
                                cmdItm.ExecuteNonQuery();
                            }

                            if (itm.ItemType == "Product")
                            {
                                // 🛡️ Atomic stock deduction & negative stock prevention
                                string deductSql = "UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE ProductID = @pid AND StockQuantity >= @qty";
                                using var cmdDeduct = new SqlCommand(deductSql, conn, trans);
                                cmdDeduct.Parameters.AddWithValue("@qty", itm.Quantity);
                                cmdDeduct.Parameters.AddWithValue("@pid", itm.ItemID);
                                int rows = cmdDeduct.ExecuteNonQuery();
                                if (rows == 0)
                                {
                                    throw new InvalidOperationException($"Insufficient inventory stock for '{itm.Description}'. Please adjust cart quantity.");
                                }
                            }
                        }
                    }
                    else
                    {
                        string todayStr = DateTime.Now.ToString("yyyyMMdd");
                        string countSql = "SELECT COUNT(*) + 1 FROM Sales WHERE CONVERT(date, SaleDate) = CONVERT(date, GETDATE())";
                        int seq = 1;
                        using (var cmdSeq = new SqlCommand(countSql, conn, trans))
                        {
                            seq = Convert.ToInt32(cmdSeq.ExecuteScalar());
                        }
                        generatedRef = $"INV-{todayStr}-{seq:D4}";

                        string insertSaleSql = @"
                            INSERT INTO Sales 
                            (InvoiceNo, UserID, CustomerName, PaymentMethod, SubTotal, Discount, TotalAmount, AmountPaid, ChangeAmount, SaleDate, Status)
                            VALUES 
                            (@inv, @uid, @cname, @pay, @sub, @disc, @tot, @paid, @change, GETDATE(), 'Completed');
                            SELECT SCOPE_IDENTITY();";

                        int newSaleId = 0;
                        using (var cmdSale = new SqlCommand(insertSaleSql, conn, trans))
                        {
                            cmdSale.Parameters.AddWithValue("@inv", generatedRef);
                            cmdSale.Parameters.AddWithValue("@uid", cashierUserId);
                            cmdSale.Parameters.AddWithValue("@cname", cleanCust);
                            cmdSale.Parameters.AddWithValue("@pay", paymentMethod);
                            cmdSale.Parameters.AddWithValue("@sub", subtotal);
                            cmdSale.Parameters.AddWithValue("@disc", discount);
                            cmdSale.Parameters.AddWithValue("@tot", grandTotal);
                            cmdSale.Parameters.AddWithValue("@paid", tenderedAmount);
                            cmdSale.Parameters.AddWithValue("@change", changeDue);
                            newSaleId = Convert.ToInt32(cmdSale.ExecuteScalar());
                        }

                        foreach (var itm in cartItems)
                        {
                            string insertDetailSql = @"
                                INSERT INTO SalesDetails (SaleID, ProductID, Quantity, UnitPrice, Subtotal)
                                VALUES (@sid, @pid, @qty, @uprice, @subtot)";

                            using (var cmdDet = new SqlCommand(insertDetailSql, conn, trans))
                            {
                                cmdDet.Parameters.AddWithValue("@sid", newSaleId);
                                cmdDet.Parameters.AddWithValue("@pid", itm.ItemID);
                                cmdDet.Parameters.AddWithValue("@qty", itm.Quantity);
                                cmdDet.Parameters.AddWithValue("@uprice", itm.UnitPrice);
                                cmdDet.Parameters.AddWithValue("@subtot", itm.Subtotal);
                                cmdDet.ExecuteNonQuery();
                            }

                            // 🛡️ Atomic stock deduction & negative stock prevention
                            string deductSql = "UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE ProductID = @pid AND StockQuantity >= @qty";
                            using var cmdDeduct = new SqlCommand(deductSql, conn, trans);
                            cmdDeduct.Parameters.AddWithValue("@qty", itm.Quantity);
                            cmdDeduct.Parameters.AddWithValue("@pid", itm.ItemID);
                            int rows = cmdDeduct.ExecuteNonQuery();
                            if (rows == 0)
                            {
                                throw new InvalidOperationException($"Insufficient inventory stock for '{itm.Description}'. Please adjust cart quantity.");
                            }
                        }
                    }

                    trans.Commit();

                    AuditLogService.Log(
                        isJobOrder ? "MAINTENANCE" : "INVENTORY",
                        $"Completed {transType} #{generatedRef} for customer {cleanCust}. Total: ₱{grandTotal:N2} via {paymentMethod}",
                    cashierUserId, cashierName, cashierRole);

                    return (true, generatedRef, transType, "");
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return (false, "", "", ex.Message);
                }
            }
            catch (Exception ex)
            {
                return (false, "", "", ex.Message);
            }
        }

        public static DailyAuditReport GenerateDailyAudit(string reportType, string cashierName)
        {
            var report = new DailyAuditReport
            {
                ReportType = reportType,
                CashierName = cashierName,
                GeneratedAt = DateTime.Now
            };

            try
            {
                var transactions = SalesService.GetTransactions("", "All Types", "All Statuses", DateTime.Today, DateTime.Today);

                report.TotalTransactions = transactions.Count;
                foreach (var t in transactions)
                {
                    if (t.Status == "Voided")
                    {
                        report.VoidedCount++;
                        report.VoidedAmount += t.TotalAmount;
                        continue;
                    }

                    if (t.TransactionType == "JobOrder") report.JobOrdersCount++;
                    else report.RetailSalesCount++;

                    report.TotalGrossRevenue += t.Subtotal;
                    report.TotalDiscounts += t.Discount;
                    report.TotalNetRevenue += t.TotalAmount;

                    string method = (t.PaymentMethod ?? "").ToUpper();
                    if (method.Contains("WALLET") || method.Contains("GCASH") || method.Contains("MAYA")) report.GCashSales += t.TotalAmount;
                    else report.CashSales += t.TotalAmount;
                }
            }
            catch { }

            return report;
        }
    }
}