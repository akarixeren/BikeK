using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace BikeKings_System.Database
{
    public class CategorySalesShare
    {
        public string CategoryName { get; set; } = "";
        public decimal Revenue { get; set; }
        public double Percentage { get; set; }
        public string ColorHex { get; set; } = "#3498DB";
        public string FormattedRevenue => $"₱{Revenue:N2} ({Percentage:F1}%)";
    }

    public class MechanicProductivityItem
    {
        public int MechanicID { get; set; }
        public string MechanicName { get; set; } = "";
        public decimal CommissionRate { get; set; } = 80m;
        public int JobsCompleted { get; set; }
        public decimal TotalLabor { get; set; }
        public decimal Commission => TotalLabor * (CommissionRate / 100m);
        public string FormattedJobs => $"{JobsCompleted} JOBS ({CommissionRate:0}%)";
        public string FormattedLabor => $"₱{TotalLabor:N2}";
        public string FormattedCommission => $"₱{Commission:N2}";
    }

    public class StockAlertItem
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = "All products well stocked";
        public string SkuText { get; set; } = "No critical inventory warnings";
        public string BadgeText { get; set; } = "HEALTHY";
        public string BadgeBg { get; set; } = "#1D2A20";
        public string BadgeFg { get; set; } = "#2ECC71";
    }

    public class DailySalesPoint
    {
        public string DayLabel { get; set; } = "";
        public string DateStr { get; set; } = "";
        public decimal Revenue { get; set; }
        public int Transactions { get; set; }
        public double BarHeightPercentage { get; set; }
        public double BarHeightPixels => Math.Max(6, (BarHeightPercentage / 100.0) * 80);
        public double BarWidth { get; set; } = 22;
        public string FormattedRevenue => Revenue >= 1000 ? $"₱{Revenue / 1000:F1}k" : (Revenue > 0 ? $"₱{Revenue:N0}" : "₱0");
        public bool IsToday { get; set; }
    }

    public class MechanicJobItem
    {
        public string JobOrderNo { get; set; } = "";
        public string FormattedDate { get; set; } = "";
        public string CustomerVehicle { get; set; } = "";
        public decimal LaborCost { get; set; }
        public decimal CommissionRate { get; set; } = 80m;
        public decimal Commission => LaborCost * (CommissionRate / 100m);
        public string FormattedLabor => $"₱{LaborCost:N2}";
        public string FormattedComm => $"₱{Commission:N2}";
    }

    public class MechanicModalDetails
    {
        public string MechanicName { get; set; } = "";
        public decimal CommissionRate { get; set; } = 80m;
        public int JobsCompleted { get; set; }
        public decimal TotalLabor { get; set; }
        public decimal Commission => TotalLabor * (CommissionRate / 100m);
        public decimal ShopShare => TotalLabor * ((100m - CommissionRate) / 100m);
        public List<MechanicJobItem> Jobs { get; set; } = new List<MechanicJobItem>();
    }

    public class ProductModalDetails
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = "All products well stocked";
        public string Sku { get; set; } = "N/A";
        public int CurrentStock { get; set; } = 0;
        public decimal SellingPrice { get; set; } = 0;
        public decimal CostPrice { get; set; } = 0;
        public int TotalSold { get; set; } = 0;
        public string CategoryName { get; set; } = "General Spare Parts";
        public string BrandName { get; set; } = "Generic";
        public int ReorderLevel { get; set; } = 10;
        public int CriticalLevel { get; set; } = 2;

        public decimal ProfitMargin => Math.Max(0, SellingPrice - CostPrice);
        public double ProfitMarginPercent => SellingPrice > 0 ? (double)(ProfitMargin / SellingPrice) * 100 : 0;
        public decimal LifetimeRevenue => TotalSold * SellingPrice;

        public string StockStatus => CurrentStock <= 0 ? "OUT OF STOCK" : (CurrentStock <= CriticalLevel ? "CRITICAL ALERT" : (CurrentStock <= ReorderLevel ? "LOW STOCK ALERT" : "HEALTHY"));
        public string StatusColor => CurrentStock <= 0 ? "#DC2626" : (CurrentStock <= CriticalLevel ? "#EF4444" : (CurrentStock <= ReorderLevel ? "#F59E0B" : "#22C55E"));
    }

    public class DashboardSummary
    {
        // ── 1. Top KPI Summary ──
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit => Math.Max(0, TotalRevenue - TotalCost);
        public decimal ProfitMargin => TotalRevenue > 0 ? (GrossProfit / TotalRevenue) * 100 : 0;
        public int TotalTransactions { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }

        // ── 2. Top Categories Breakdown (Dynamic) ──
        public List<CategorySalesShare> TopCategories { get; set; } = new List<CategorySalesShare>();

        // ── 3. Mechanics Productivity (Dynamic) ──
        public List<MechanicProductivityItem> MechanicsList { get; set; } = new List<MechanicProductivityItem>();

        // ── 4. Stock Alerts (Dynamic List) ──
        public List<StockAlertItem> StockAlertsList { get; set; } = new List<StockAlertItem>();
        public int AlertProductID { get; set; }
        public string AlertItemName { get; set; } = "All products well stocked";
        public string AlertItemSku { get; set; } = "No critical inventory warnings";
        public string AlertItemBadge { get; set; } = "HEALTHY";
        public string AlertItemBadgeBg { get; set; } = "#1D2A20";
        public string AlertItemBadgeFg { get; set; } = "#2ECC71";

        // ── 5. Sales Trend Graph Points ──
        public List<DailySalesPoint> DailyTrend { get; set; } = new List<DailySalesPoint>();
    }

    public static class DashboardService
    {
        public static DashboardSummary GetDashboardStats(string period = "Daily", DateTime? customFrom = null, DateTime? customTo = null)
        {
            var summary = new DashboardSummary();

            DateTime now = DateTime.Now;
            DateTime fromDate = now.Date;
            DateTime toDate = now.Date.AddDays(1).AddTicks(-1);

            if (customFrom.HasValue && customTo.HasValue)
            {
                fromDate = customFrom.Value.Date;
                toDate = customTo.Value.Date.AddDays(1).AddTicks(-1);
            }
            else
            {
                switch (period.ToLower())
                {
                    case "weekly":
                        int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                        fromDate = now.Date.AddDays(-diff);
                        break;
                    case "monthly":
                        fromDate = new DateTime(now.Year, now.Month, 1);
                        break;
                    case "annually":
                    case "annual":
                        fromDate = new DateTime(now.Year, 1, 1);
                        break;
                    default: // Daily
                        fromDate = now.Date;
                        break;
                }
            }

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();

                // 1. Retail Sales Revenue & Transactions
                string sqlSales = @"
                    SELECT 
                        ISNULL(SUM(TotalAmount), 0) AS TotalSales,
                        COUNT(*) AS SalesCount
                    FROM Sales
                    WHERE Status = 'Completed' AND SaleDate >= @fromDate AND SaleDate <= @toDate";

                using (var cmd = new SqlCommand(sqlSales, conn))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate);
                    cmd.Parameters.AddWithValue("@toDate", toDate);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        summary.TotalRevenue += Convert.ToDecimal(r["TotalSales"]);
                        summary.TotalTransactions += Convert.ToInt32(r["SalesCount"]);
                    }
                }

                // 2. Job Orders Revenue & Transactions
                decimal totalLaborIncome = 0;
                string sqlJobs = @"
                    SELECT 
                        ISNULL(SUM(TotalAmount), 0) AS TotalJobRev,
                        ISNULL(SUM(TotalLabor), 0) AS TotalLabor,
                        COUNT(*) AS JobsCount
                    FROM JobOrders
                    WHERE Status = 'Completed' AND CreatedAt >= @fromDate AND CreatedAt <= @toDate";

                using (var cmd = new SqlCommand(sqlJobs, conn))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate);
                    cmd.Parameters.AddWithValue("@toDate", toDate);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        summary.TotalRevenue += Convert.ToDecimal(r["TotalJobRev"]);
                        totalLaborIncome = Convert.ToDecimal(r["TotalLabor"]);
                        summary.TotalTransactions += Convert.ToInt32(r["JobsCount"]);
                    }
                }

                // 3. Dynamic Category Sales Share (From Actual System Categories)
                var catList = new List<CategorySalesShare>();
                string[] colors = new string[] { "#3498DB", "#2ECC71", "#E67E22", "#9B59B6", "#E74C3C", "#1ABC9C", "#F1C40F" };

                string sqlCategories = @"
                    WITH CatSales AS (
                        -- Retail Sales Products
                        SELECT 
                            ISNULL(c.CategoryName, 'General Spare Parts') AS CatName,
                            SUM(sd.Subtotal) AS TotalRev
                        FROM SalesDetails sd
                        INNER JOIN Sales s ON sd.SaleID = s.SaleID
                        LEFT JOIN Products p ON sd.ProductID = p.ProductID
                        LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
                        WHERE s.Status = 'Completed' AND s.SaleDate >= @fromDate AND s.SaleDate <= @toDate
                        GROUP BY ISNULL(c.CategoryName, 'General Spare Parts')

                        UNION ALL

                        -- Job Order Parts
                        SELECT 
                            ISNULL(c.CategoryName, 'General Spare Parts') AS CatName,
                            SUM(joi.Subtotal) AS TotalRev
                        FROM JobOrderItems joi
                        INNER JOIN JobOrders jo ON joi.JobOrderID = jo.JobOrderID
                        LEFT JOIN Products p ON joi.ProductID = p.ProductID
                        LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
                        WHERE jo.Status = 'Completed' AND jo.CreatedAt >= @fromDate AND jo.CreatedAt <= @toDate
                          AND joi.ItemType = 'Product'
                        GROUP BY ISNULL(c.CategoryName, 'General Spare Parts')

                        UNION ALL

                        -- Job Order Labor & Services
                        SELECT 
                            'Labor & Services' AS CatName,
                            SUM(jo.TotalLabor) AS TotalRev
                        FROM JobOrders jo
                        WHERE jo.Status = 'Completed' AND jo.CreatedAt >= @fromDate AND jo.CreatedAt <= @toDate
                          AND jo.TotalLabor > 0
                    )
                    SELECT TOP 4
                        CatName, 
                        SUM(TotalRev) AS Revenue
                    FROM CatSales
                    GROUP BY CatName
                    HAVING SUM(TotalRev) > 0
                    ORDER BY Revenue DESC;";

                using (var cmd = new SqlCommand(sqlCategories, conn))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate);
                    cmd.Parameters.AddWithValue("@toDate", toDate);
                    using var r = cmd.ExecuteReader();
                    int colorIdx = 0;
                    while (r.Read())
                    {
                        string cName = r["CatName"].ToString() ?? "Uncategorized";
                        decimal rev = Convert.ToDecimal(r["Revenue"]);
                        double pct = summary.TotalRevenue > 0 ? (double)(rev / summary.TotalRevenue) * 100 : 0;

                        catList.Add(new CategorySalesShare
                        {
                            CategoryName = cName,
                            Revenue = rev,
                            Percentage = pct,
                            ColorHex = colors[colorIdx % colors.Length]
                        });
                        colorIdx++;
                    }
                }

                // If no sales exist in period, load active categories from database with ₱0.00
                if (catList.Count == 0)
                {
                    string sqlActiveCats = @"SELECT TOP 3 CategoryName FROM Categories WHERE IsActive = 1 ORDER BY CategoryName;";
                    using (var cmd = new SqlCommand(sqlActiveCats, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        int colorIdx = 0;
                        while (r.Read())
                        {
                            catList.Add(new CategorySalesShare
                            {
                                CategoryName = r["CategoryName"].ToString() ?? "General Spare Parts",
                                Revenue = 0,
                                Percentage = 0,
                                ColorHex = colors[colorIdx % colors.Length]
                            });
                            colorIdx++;
                        }
                    }

                    if (catList.Count == 0)
                    {
                        catList.Add(new CategorySalesShare { CategoryName = "General Spare Parts", Revenue = 0, Percentage = 0, ColorHex = colors[0] });
                    }

                    catList.Add(new CategorySalesShare { CategoryName = "Labor & Services", Revenue = 0, Percentage = 0, ColorHex = colors[catList.Count % colors.Length] });
                }

                summary.TopCategories = catList;

                // Total Cost Calculation (From sold items or proportional estimate)
                decimal totalParts = Math.Max(0, summary.TotalRevenue - totalLaborIncome);
                summary.TotalCost = totalParts * 0.65m;

                // 4. Dynamic Stock Alerts (Live from Inventory Table)
                string sqlStockCounts = @"
                    SELECT 
                        COUNT(CASE WHEN StockQuantity <= ReorderLevel THEN 1 END) AS LowCount,
                        COUNT(CASE WHEN StockQuantity <= 0 THEN 1 END) AS OutCount
                    FROM Products WHERE IsActive = 1;";

                using (var cmd = new SqlCommand(sqlStockCounts, conn))
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        summary.LowStockCount = Convert.ToInt32(r["LowCount"]);
                        summary.OutOfStockCount = Convert.ToInt32(r["OutCount"]);
                    }
                }

                string sqlStockAlerts = @"
                    SELECT TOP 2 
                        ProductID, ProductName, Barcode, StockQuantity, ReorderLevel, ISNULL(CriticalLimit, 2) AS CriticalLimit
                    FROM Products
                    WHERE IsActive = 1 AND StockQuantity <= ReorderLevel
                    ORDER BY 
                        CASE 
                            WHEN StockQuantity <= 0 THEN 0
                            WHEN StockQuantity <= ISNULL(CriticalLimit, 2) THEN 1
                            ELSE 2 
                        END,
                        StockQuantity ASC,
                        ProductID ASC;";

                using (var cmd = new SqlCommand(sqlStockAlerts, conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int pId = Convert.ToInt32(r["ProductID"]);
                        string pName = r["ProductName"].ToString() ?? "";
                        string barcode = r["Barcode"]?.ToString() ?? "N/A";
                        int qty = Convert.ToInt32(r["StockQuantity"]);
                        int reorder = Convert.ToInt32(r["ReorderLevel"]);
                        int crit = Convert.ToInt32(r["CriticalLimit"]);

                        string badge;
                        string bg;
                        string fg;

                        if (qty <= 0)
                        {
                            badge = "OUT OF STOCK";
                            bg = "#3E161C";
                            fg = "#EF4444";
                        }
                        else if (qty <= crit)
                        {
                            badge = $"{qty} left CRITICAL";
                            bg = "#442E15";
                            fg = "#F39C12";
                        }
                        else
                        {
                            badge = $"{qty} left LOW";
                            bg = "#3B2A10";
                            fg = "#F59E0B";
                        }

                        if (summary.AlertProductID == 0)
                        {
                            summary.AlertProductID = pId;
                            summary.AlertItemName = pName;
                            summary.AlertItemSku = $"SKU: {barcode} | Reorder at: {reorder} units";
                            summary.AlertItemBadge = badge;
                            summary.AlertItemBadgeBg = bg;
                            summary.AlertItemBadgeFg = fg;
                        }

                        summary.StockAlertsList.Add(new StockAlertItem
                        {
                            ProductID = pId,
                            ProductName = pName,
                            SkuText = $"SKU: {barcode} | Reorder: {reorder} units",
                            BadgeText = badge,
                            BadgeBg = bg,
                            BadgeFg = fg
                        });
                    }
                }

                if (summary.StockAlertsList.Count == 0)
                {
                    summary.StockAlertsList.Add(new StockAlertItem
                    {
                        ProductID = 0,
                        ProductName = "All products well stocked",
                        SkuText = "No critical inventory warnings",
                        BadgeText = "HEALTHY",
                        BadgeBg = "#1D2A20",
                        BadgeFg = "#2ECC71"
                    });
                }

                // 5. Dynamic Mechanics Productivity (All Active Mechanics in System)
                string sqlMechanics = @"
                    SELECT 
                        m.MechanicID,
                        m.FullName,
                        ISNULL(m.CommissionRate, 80.00) AS CommissionRate,
                        COUNT(j.JobOrderID) AS JobCount,
                        ISNULL(SUM(j.TotalLabor), 0) AS TotalLabor
                    FROM Mechanics m
                    LEFT JOIN JobOrders j ON m.MechanicID = j.MechanicID 
                        AND j.Status = 'Completed' 
                        AND j.CreatedAt >= @fromDate 
                        AND j.CreatedAt <= @toDate
                    WHERE m.IsActive = 1
                    GROUP BY m.MechanicID, m.FullName, m.CommissionRate
                    ORDER BY TotalLabor DESC, JobCount DESC, m.FullName ASC;";
                using (var cmd = new SqlCommand(sqlMechanics, conn))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate);
                    cmd.Parameters.AddWithValue("@toDate", toDate);
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        summary.MechanicsList.Add(new MechanicProductivityItem
                        {
                            MechanicID = Convert.ToInt32(r["MechanicID"]),
                            MechanicName = r["FullName"].ToString() ?? "Mechanic",
                            CommissionRate = Convert.ToDecimal(r["CommissionRate"]),
                            JobsCompleted = Convert.ToInt32(r["JobCount"]),
                            TotalLabor = Convert.ToDecimal(r["TotalLabor"])
                        });
                    }
                }

                // Fallback if no mechanics created yet
                if (summary.MechanicsList.Count == 0)
                {
                    summary.MechanicsList.Add(new MechanicProductivityItem
                    {
                        MechanicID = 0,
                        MechanicName = "General Repair",
                        JobsCompleted = 0,
                        TotalLabor = 0
                    });
                }

                // 6. Dynamic Graph Data matching the selected timeframe
                summary.DailyTrend = GenerateGraphData(period, fromDate, toDate, conn);
            }
            catch { }

            return summary;
        }

        public static ProductModalDetails GetProductDetails(int? productId = null)
        {
            var p = new ProductModalDetails();
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();

                string sql = @"
                    SELECT TOP 1 
                        p.ProductID,
                        p.ProductName,
                        ISNULL(p.Barcode, 'N/A') AS Barcode,
                        ISNULL(c.CategoryName, 'General Spare Parts') AS CategoryName,
                        ISNULL(b.BrandName, 'Generic') AS BrandName,
                        p.CostPrice,
                        p.SellingPrice,
                        p.StockQuantity,
                        p.ReorderLevel,
                        ISNULL(p.CriticalLimit, 2) AS CriticalLimit,
                        (
                            ISNULL((SELECT SUM(sd.Quantity) FROM SalesDetails sd WHERE sd.ProductID = p.ProductID), 0)
                            +
                            ISNULL((SELECT SUM(joi.Quantity) FROM JobOrderItems joi WHERE joi.ProductID = p.ProductID AND joi.ItemType = 'Product'), 0)
                        ) AS TotalQtySold
                    FROM Products p
                    LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
                    LEFT JOIN Brands b ON p.BrandID = b.BrandID
                    WHERE p.IsActive = 1
                      AND (@prodId IS NULL OR p.ProductID = @prodId)
                    ORDER BY 
                        CASE WHEN @prodId IS NOT NULL THEN 0 ELSE 
                            CASE 
                                WHEN p.StockQuantity <= 0 THEN 0
                                WHEN p.StockQuantity <= ISNULL(p.CriticalLimit, 2) THEN 1
                                WHEN p.StockQuantity <= p.ReorderLevel THEN 2
                                ELSE 3 
                            END 
                        END,
                        p.StockQuantity ASC,
                        p.ProductID ASC;";

                using var cmd = new SqlCommand(sql, conn);
                if (productId.HasValue && productId.Value > 0)
                    cmd.Parameters.AddWithValue("@prodId", productId.Value);
                else
                    cmd.Parameters.AddWithValue("@prodId", DBNull.Value);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    p.ProductID = Convert.ToInt32(r["ProductID"]);
                    p.ProductName = r["ProductName"].ToString() ?? "";
                    p.Sku = r["Barcode"]?.ToString() ?? "N/A";
                    p.CategoryName = r["CategoryName"].ToString() ?? "General Spare Parts";
                    p.BrandName = r["BrandName"].ToString() ?? "Generic";
                    p.CostPrice = Convert.ToDecimal(r["CostPrice"]);
                    p.SellingPrice = Convert.ToDecimal(r["SellingPrice"]);
                    p.CurrentStock = Convert.ToInt32(r["StockQuantity"]);
                    p.ReorderLevel = Convert.ToInt32(r["ReorderLevel"]);
                    p.CriticalLevel = Convert.ToInt32(r["CriticalLimit"]);
                    p.TotalSold = Convert.ToInt32(r["TotalQtySold"]);
                }
            }
            catch { }

            return p;
        }

        public static MechanicModalDetails GetMechanicModalDetails(string mechanicName, string period, DateTime? customFrom, DateTime? customTo)
        {
            var details = new MechanicModalDetails { MechanicName = mechanicName };

            DateTime now = DateTime.Now;
            DateTime fromDate = now.Date;
            DateTime toDate = now.Date.AddDays(1).AddTicks(-1);

            if (customFrom.HasValue && customTo.HasValue)
            {
                fromDate = customFrom.Value.Date;
                toDate = customTo.Value.Date.AddDays(1).AddTicks(-1);
            }
            else
            {
                switch (period.ToLower())
                {
                    case "weekly":
                        int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                        fromDate = now.Date.AddDays(-diff);
                        break;
                    case "monthly":
                        fromDate = new DateTime(now.Year, now.Month, 1);
                        break;
                    case "annually":
                    case "annual":
                        fromDate = new DateTime(now.Year, 1, 1);
                        break;
                    default: // Daily
                        fromDate = now.Date;
                        break;
                }
            }

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();

                string sql = @"
                    SELECT 
                        j.JobOrderNo,
                        j.CreatedAt,
                        ISNULL(j.CustomerName, 'Walk-in') AS Cust,
                        ISNULL(j.MotorcycleUnit, 'Motorcycle') AS Mc,
                        ISNULL(j.PlateNumber, '') AS Plate,
                        j.TotalLabor,
                        ISNULL(m.CommissionRate, 80.00) AS CommissionRate
                    FROM JobOrders j
                    LEFT JOIN Mechanics m ON j.MechanicID = m.MechanicID
                    WHERE (m.FullName LIKE @mechName OR j.CustomerName LIKE @mechName)
                      AND j.Status = 'Completed'
                      AND j.CreatedAt >= @from AND j.CreatedAt <= @to
                    ORDER BY j.CreatedAt DESC;";

                using var cmd = new SqlCommand(sql, conn);
                string shortName = mechanicName.Split(' ')[0];
                cmd.Parameters.AddWithValue("@mechName", $"%{shortName}%");
                cmd.Parameters.AddWithValue("@from", fromDate);
                cmd.Parameters.AddWithValue("@to", toDate);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string jno = r["JobOrderNo"].ToString() ?? "";
                    DateTime dt = Convert.ToDateTime(r["CreatedAt"]);
                    string cust = r["Cust"].ToString() ?? "";
                    string mc = r["Mc"].ToString() ?? "";
                    string plate = r["Plate"].ToString() ?? "";
                    decimal labor = Convert.ToDecimal(r["TotalLabor"]);
                    decimal commRate = Convert.ToDecimal(r["CommissionRate"]);

                    details.CommissionRate = commRate;

                    string vehicleInfo = !string.IsNullOrWhiteSpace(plate) && plate != "N/A"
                        ? $"{cust} ({mc} • {plate})"
                        : $"{cust} ({mc})";

                    details.Jobs.Add(new MechanicJobItem
                    {
                        JobOrderNo = jno,
                        FormattedDate = dt.ToString("MM/dd hh:mm tt"),
                        CustomerVehicle = vehicleInfo,
                        LaborCost = labor,
                        CommissionRate = commRate
                    });

                    details.JobsCompleted++;
                    details.TotalLabor += labor;
                }
            }
            catch { }

            return details;
        }

        private static List<DailySalesPoint> GenerateGraphData(string period, DateTime fromDate, DateTime toDate, SqlConnection conn)
        {
            var points = new List<DailySalesPoint>();
            DateTime now = DateTime.Now;

            // ── SCENARIO 1: ANNUALLY -> 12 Months (Jan - Dec) ──
            if (period.Equals("annually", StringComparison.OrdinalIgnoreCase) || (toDate - fromDate).TotalDays > 120)
            {
                int year = now.Year;
                var monthTotals = new decimal[13];

                string sql = @"
                    SELECT MONTH(SaleDate) AS M, SUM(TotalAmount) AS Rev
                    FROM Sales WHERE Status = 'Completed' AND YEAR(SaleDate) = @y
                    GROUP BY MONTH(SaleDate)
                    UNION ALL
                    SELECT MONTH(CreatedAt) AS M, SUM(TotalAmount) AS Rev
                    FROM JobOrders WHERE Status = 'Completed' AND YEAR(CreatedAt) = @y
                    GROUP BY MONTH(CreatedAt)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@y", year);
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        int m = Convert.ToInt32(r["M"]);
                        if (m >= 1 && m <= 12)
                            monthTotals[m] += Convert.ToDecimal(r["Rev"]);
                    }
                }

                decimal maxRev = 0;
                for (int m = 1; m <= 12; m++) if (monthTotals[m] > maxRev) maxRev = monthTotals[m];
                if (maxRev == 0) maxRev = 1;

                string[] mNames = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                for (int m = 1; m <= 12; m++)
                {
                    points.Add(new DailySalesPoint
                    {
                        DayLabel = mNames[m],
                        DateStr = $"{mNames[m]} {year}: ₱{monthTotals[m]:N2}",
                        Revenue = monthTotals[m],
                        BarHeightPercentage = (double)(monthTotals[m] / maxRev) * 100,
                        BarWidth = 16,
                        IsToday = (m == now.Month)
                    });
                }
            }
            // ── SCENARIO 2: MONTHLY -> 5 Weekly Buckets of the Month ──
            else if (period.Equals("monthly", StringComparison.OrdinalIgnoreCase) || ((toDate - fromDate).TotalDays > 14 && (toDate - fromDate).TotalDays <= 120))
            {
                var weekTotals = new decimal[5];

                string sql = @"
                    SELECT DAY(SaleDate) AS D, SUM(TotalAmount) AS Rev
                    FROM Sales WHERE Status = 'Completed' AND SaleDate >= @s AND SaleDate <= @e
                    GROUP BY DAY(SaleDate)
                    UNION ALL
                    SELECT DAY(CreatedAt) AS D, SUM(TotalAmount) AS Rev
                    FROM JobOrders WHERE Status = 'Completed' AND CreatedAt >= @s AND CreatedAt <= @e
                    GROUP BY DAY(CreatedAt)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@s", fromDate);
                    cmd.Parameters.AddWithValue("@e", toDate);
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        int d = Convert.ToInt32(r["D"]);
                        decimal rev = Convert.ToDecimal(r["Rev"]);
                        int wIdx = Math.Min(4, (d - 1) / 7);
                        weekTotals[wIdx] += rev;
                    }
                }

                decimal maxRev = 0;
                for (int i = 0; i < 5; i++) if (weekTotals[i] > maxRev) maxRev = weekTotals[i];
                if (maxRev == 0) maxRev = 1;

                string[] wLabels = { "Wk 1 (1-7)", "Wk 2 (8-14)", "Wk 3 (15-21)", "Wk 4 (22-28)", "Wk 5 (29+)" };
                int currentWeekIdx = Math.Min(4, (now.Day - 1) / 7);

                for (int i = 0; i < 5; i++)
                {
                    points.Add(new DailySalesPoint
                    {
                        DayLabel = wLabels[i],
                        DateStr = $"{wLabels[i]}: ₱{weekTotals[i]:N2}",
                        Revenue = weekTotals[i],
                        BarHeightPercentage = (double)(weekTotals[i] / maxRev) * 100,
                        BarWidth = 28,
                        IsToday = (fromDate.Month == now.Month && i == currentWeekIdx)
                    });
                }
            }
            // ── SCENARIO 3: DAILY -> Hourly Intervals for Today ──
            else if (period.Equals("daily", StringComparison.OrdinalIgnoreCase))
            {
                var hourTotals = new Dictionary<string, decimal>
                {
                    { "8 AM", 0 }, { "10 AM", 0 }, { "12 PM", 0 }, { "2 PM", 0 }, { "4 PM", 0 }, { "6 PM", 0 }, { "8 PM", 0 }
                };

                string sql = @"
                    SELECT DATEPART(HOUR, SaleDate) AS H, SUM(TotalAmount) AS Rev
                    FROM Sales WHERE Status = 'Completed' AND SaleDate >= @d AND SaleDate < @nextD
                    GROUP BY DATEPART(HOUR, SaleDate)
                    UNION ALL
                    SELECT DATEPART(HOUR, CreatedAt) AS H, SUM(TotalAmount) AS Rev
                    FROM JobOrders WHERE Status = 'Completed' AND CreatedAt >= @d AND CreatedAt < @nextD
                    GROUP BY DATEPART(HOUR, CreatedAt)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@d", fromDate.Date);
                    cmd.Parameters.AddWithValue("@nextD", fromDate.Date.AddDays(1));
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        int h = Convert.ToInt32(r["H"]);
                        decimal rev = Convert.ToDecimal(r["Rev"]);

                        if (h <= 9) hourTotals["8 AM"] += rev;
                        else if (h <= 11) hourTotals["10 AM"] += rev;
                        else if (h <= 13) hourTotals["12 PM"] += rev;
                        else if (h <= 15) hourTotals["2 PM"] += rev;
                        else if (h <= 17) hourTotals["4 PM"] += rev;
                        else if (h <= 19) hourTotals["6 PM"] += rev;
                        else hourTotals["8 PM"] += rev;
                    }
                }

                decimal maxRev = 0;
                foreach (var v in hourTotals.Values) if (v > maxRev) maxRev = v;
                if (maxRev == 0) maxRev = 1;

                foreach (var kv in hourTotals)
                {
                    points.Add(new DailySalesPoint
                    {
                        DayLabel = kv.Key,
                        DateStr = $"Today @ {kv.Key}: ₱{kv.Value:N2}",
                        Revenue = kv.Value,
                        BarHeightPercentage = (double)(kv.Value / maxRev) * 100,
                        BarWidth = 24,
                        IsToday = (kv.Value > 0)
                    });
                }
            }
            // ── SCENARIO 4: WEEKLY / CUSTOM RANGE (7-Day Mon - Sun View) ──
            else
            {
                DateTime startDay = fromDate.Date;
                var dayTotals = new Dictionary<DateTime, decimal>();

                for (int i = 0; i < 7; i++)
                {
                    dayTotals[startDay.AddDays(i)] = 0;
                }

                string sql = @"
                    SELECT CONVERT(date, SaleDate) AS DDate, SUM(TotalAmount) AS DayRev
                    FROM Sales WHERE Status = 'Completed' AND SaleDate >= @start AND SaleDate <= @end
                    GROUP BY CONVERT(date, SaleDate)
                    UNION ALL
                    SELECT CONVERT(date, CreatedAt) AS DDate, SUM(TotalAmount) AS DayRev
                    FROM JobOrders WHERE Status = 'Completed' AND CreatedAt >= @start AND CreatedAt <= @end
                    GROUP BY CONVERT(date, CreatedAt)";

                using var cmd = new SqlCommand(sql, conn);
                {
                    cmd.Parameters.AddWithValue("@start", startDay);
                    cmd.Parameters.AddWithValue("@end", startDay.AddDays(7));
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        if (r["DDate"] != DBNull.Value)
                        {
                            DateTime d = Convert.ToDateTime(r["DDate"]).Date;
                            if (dayTotals.ContainsKey(d))
                                dayTotals[d] += Convert.ToDecimal(r["DayRev"]);
                        }
                    }
                }

                decimal maxRev = 0;
                foreach (var v in dayTotals.Values) if (v > maxRev) maxRev = v;
                if (maxRev == 0) maxRev = 1;

                foreach (var kv in dayTotals)
                {
                    DateTime d = kv.Key;
                    decimal rev = kv.Value;

                    points.Add(new DailySalesPoint
                    {
                        DayLabel = d == now.Date ? "Today" : d.ToString("ddd"),
                        DateStr = $"{d:MMM dd}: ₱{rev:N2}",
                        Revenue = rev,
                        BarHeightPercentage = (double)(rev / maxRev) * 100,
                        BarWidth = 24,
                        IsToday = (d == now.Date)
                    });
                }
            }

            return points;
        }
    }
}