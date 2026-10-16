#nullable disable
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;

namespace BikeKings_System.Database
{
    public class ProductItem
    {
        public int ProductID { get; set; }
        public string SKU { get; set; } = "";

        public string ImagePath { get; set; } = "";
        public string CategoryIcon => Helpers.ProductImageHelper.GetCategoryIcon(ProductName, CategoryName);
        public string DisplayImage => Helpers.ProductImageHelper.ResolveImagePath(ImagePath, ProductName, CategoryName);
        public string Barcode
        {
            get => SKU;
            set => SKU = value;
        }
        public string ProductName { get; set; } = "";
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = "";
        public int BrandID { get; set; }
        public string BrandName { get; set; } = "";
        public int? SupplierID { get; set; }
        public string SupplierName { get; set; } = "Not Assigned";
        public decimal CostPrice { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SellingPrice
        {
            get => UnitPrice;
            set => UnitPrice = value;
        }
        public int Quantity { get; set; }
        public int StockQuantity
        {
            get => Quantity;
            set => Quantity = value;
        }
        public int ReorderLevel { get; set; } = 5;
        public bool IsActive { get; set; } = true;

        public decimal TotalValue => Quantity * CostPrice;
        public decimal PotentialRevenue => Quantity * UnitPrice;
        public decimal Margin => UnitPrice > 0 ? ((UnitPrice - CostPrice) / UnitPrice) * 100 : 0;
        public decimal ProfitMargin => UnitPrice - CostPrice;
        public decimal ProfitMarginPercent => Margin;

        public int SuggestedReorderQty => Math.Max(0, (ReorderLevel * 2) - Quantity);

        public string StockStatus
        {
            get
            {
                if (Quantity <= 0) return "Out of Stock";
                if (Quantity <= ReorderLevel) return "Low Stock";
                return "In Stock";
            }
        }

        public string StatusColor
        {
            get
            {
                if (Quantity <= 0) return "#EF4444";
                if (Quantity <= ReorderLevel) return "#F59E0B";
                return "#10B981";
            }
        }

        public string StatusFgColor => StatusColor;

        public string StatusBgColor
        {
            get
            {
                if (Quantity <= 0) return "#2D1214";
                if (Quantity <= ReorderLevel) return "#2D2214";
                return "#0E2A1E";
            }
        }
    }

    public class ProductSalesHistoryEntry
    {
        public DateTime TransactionDate { get; set; }
        public string FormattedDate => TransactionDate.ToString("MMM dd, yyyy, hh:mm tt");
        public string ReferenceNo { get; set; } = "";
        public string TransactionType { get; set; } = "Retail Sale"; // "Job Order" or "Retail Sale"
        public string TypeBadgeText => TransactionType.ToUpper();
        public string TypeBadgeBg => (TransactionType.Contains("Job", StringComparison.OrdinalIgnoreCase)) ? "#0F2838" : "#0E2A1E";
        public string TypeBadgeFg => (TransactionType.Contains("Job", StringComparison.OrdinalIgnoreCase)) ? "#38BDF8" : "#10B981";
        public string CustomerDetails { get; set; } = "Walk-in Customer";
        public int QuantitySold { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal TotalRevenue => QuantitySold * UnitPrice;
        public decimal GrossProfit => TotalRevenue - (QuantitySold * CostPrice);
        public string FormattedUnitPrice => $"₱{UnitPrice:N2}";
        public string FormattedRevenue => $"₱{TotalRevenue:N2}";
        public string FormattedProfit => $"₱{GrossProfit:N2}";
        public string CashierOrUser { get; set; } = "Ruby";
    }

    public class ProductSalesHistorySummary
    {
        public int ProductID { get; set; }
        public string SKU { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int CurrentStock { get; set; }
        public int TotalUnitsSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit => TotalRevenue - TotalCost;
        public decimal MarginPercent => TotalRevenue > 0 ? (TotalProfit / TotalRevenue) * 100 : 0;
        public List<ProductSalesHistoryEntry> Entries { get; set; } = new List<ProductSalesHistoryEntry>();
    }

    public class SupplierModel
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = "";
        public string ContactPerson { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public bool IsRecommended { get; set; } = false;
        public string RecommendationTag { get; set; } = "";

        public string DisplayName => IsRecommended
            ? (!string.IsNullOrWhiteSpace(RecommendationTag) ? $"⭐ {SupplierName} [{RecommendationTag}]" : $"⭐ {SupplierName} [RECOMMENDED]")
            : SupplierName;

        public override string ToString() => DisplayName;
    }

    public class POLineItem : System.ComponentModel.INotifyPropertyChanged
    {
        public int POItemID { get; set; }
        public int POID { get; set; }
        public int? ProductID { get; set; }
        public string ItemDescription { get; set; } = "";
        public string Unit { get; set; } = "pcs";
        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                    TotalPrice = _quantity * _unitPrice;
                }
            }
        }
        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (_unitPrice != value)
                {
                    _unitPrice = value;
                    OnPropertyChanged(nameof(UnitPrice));
                    TotalPrice = _quantity * _unitPrice;
                }
            }
        }
        private decimal _totalPrice;
        public decimal TotalPrice
        {
            get => _totalPrice;
            set
            {
                if (_totalPrice != value)
                {
                    _totalPrice = value;
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class PurchaseOrderItem
    {
        public int POID { get; set; }
        public string PONumber { get; set; } = "";
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = "";
        public string SupplierContact { get; set; } = "";
        public string SupplierAddress { get; set; } = "";
        public string SupplierPhone { get; set; } = "";
        public string SupplierEmail { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public DateTime PODate => OrderDate;
        public DateTime? ExpectedDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Draft";
        public string RequesterName { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string Notes
        {
            get => Remarks;
            set => Remarks = value;
        }
        public string Terms { get; set; } = "Net 30";
        public string ShippedVia { get; set; } = "Standard Freight";
        public List<POLineItem> Items { get; set; } = new List<POLineItem>();

        public bool IsPending => (Status ?? "").Equals("Draft", StringComparison.OrdinalIgnoreCase) || (Status ?? "").Equals("Pending", StringComparison.OrdinalIgnoreCase);
        public bool IsApproved => (Status ?? "").Equals("Approved", StringComparison.OrdinalIgnoreCase) || (Status ?? "").Equals("Sent", StringComparison.OrdinalIgnoreCase);
        public bool IsReceived => (Status ?? "").Equals("Received", StringComparison.OrdinalIgnoreCase);

        public string StatusColor
        {
            get
            {
                return (Status ?? "").ToUpper() switch
                {
                    "RECEIVED" or "COMPLETED" => "#10B981",
                    "SENT" => "#38BDF8",
                    "APPROVED" => "#818CF8",
                    "CANCELLED" or "REJECTED" => "#EF4444",
                    _ => "#F59E0B"
                };
            }
        }

        public string StatusBgColor
        {
            get
            {
                return (Status ?? "").ToUpper() switch
                {
                    "RECEIVED" or "COMPLETED" => "#0E2A1E",
                    "SENT" => "#0F2838",
                    "APPROVED" => "#1E1E38",
                    "CANCELLED" or "REJECTED" => "#2D1214",
                    _ => "#2D2214"
                };
            }
        }
    }

    public class InventoryStatsSummary
    {
        public decimal TotalValuation { get; set; }
        public decimal TotalStockValue
        {
            get => TotalValuation;
            set => TotalValuation = value;
        }
        public int CriticalCount { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int ReplenishmentNeededCount => CriticalCount;
        public int TotalSKUs { get; set; }
        public int TotalUnits { get; set; }
    }

    public class StockTransactionItem
    {
        public int TransactionID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string FormattedDate => TransactionDate.ToString("yyyy-MM-dd HH:mm");
        public string TransactionType { get; set; } = "";
        public string SKU { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string ReferenceNumber { get; set; } = "";
        public string Reason { get; set; } = "";
        public int QuantityAdjusted { get; set; }
        public string ProcessedBy { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public class StockAnalyticsItem
    {
        public int ProductID { get; set; }
        public string SKU { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public int UnitsSoldLast30Days { get; set; }
        public int CurrentStock { get; set; }
        public string VelocityCategory { get; set; } = "";
        public string VelocityBgColor { get; set; } = "";
        public string VelocityFgColor { get; set; } = "";
        public string ActionRecommendation { get; set; } = "";
    }

    public static class InventoryService
    {
        public static void EnsureDatabaseSchema()
        {
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                string script = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
                    BEGIN
                        CREATE TABLE Categories (
                            CategoryID INT IDENTITY(1,1) PRIMARY KEY,
                            CategoryName NVARCHAR(100) NOT NULL,
                            Description NVARCHAR(255) NULL,
                            IsActive BIT DEFAULT 1
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'IsActive')
                        BEGIN
                            ALTER TABLE Categories ADD IsActive BIT DEFAULT 1;
                        END
                        UPDATE Categories SET IsActive = 1 WHERE IsActive IS NULL;
                    END

                    IF NOT EXISTS (SELECT 1 FROM Categories)
                    BEGIN
                        INSERT INTO Categories (CategoryName, Description, IsActive) VALUES
                        ('Engine Oil & Lubricants', 'Engine oils, gear oils, brake fluids, coolants', 1),
                        ('Tires & Tubes', 'Tubeless tires, interior tubes, tire valves', 1),
                        ('Brakes & Pads', 'Brake pads, shoes, discs, brake levers', 1),
                        ('Batteries & Electrical', 'Motorcycle batteries, fuses, relays, horns', 1),
                        ('Spark Plugs', 'Standard, platinum, iridium spark plugs', 1),
                        ('Chains & Sprockets', 'Drive chains, drive sprockets, chain master links', 1),
                        ('Filters', 'Air filters, oil filters, fuel filters', 1),
                        ('Suspension & Shocks', 'Rear shock absorbers, front fork oil, fork seals', 1),
                        ('Lighting & Bulbs', 'Headlight bulbs, signal lights, tail lights, LEDs', 1),
                        ('Accessories & Helmets', 'Side mirrors, grips, top boxes, phone mounts', 1),
                        ('Bearings & Seals', 'Wheel bearings, crankshaft bearings, oil seals', 1),
                        ('Belts & CVT Parts', 'Drive belts, flyball rollers, clutch springs', 1);
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Brands')
                    BEGIN
                        CREATE TABLE Brands (
                            BrandID INT IDENTITY(1,1) PRIMARY KEY,
                            BrandName NVARCHAR(100) NOT NULL,
                            Description NVARCHAR(255) NULL,
                            IsActive BIT DEFAULT 1
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Brands') AND name = 'IsActive')
                        BEGIN
                            ALTER TABLE Brands ADD IsActive BIT DEFAULT 1;
                        END
                        UPDATE Brands SET IsActive = 1 WHERE IsActive IS NULL;
                    END

                    IF NOT EXISTS (SELECT 1 FROM Brands)
                    BEGIN
                        INSERT INTO Brands (BrandName, Description, IsActive) VALUES
                        ('Motul', 'Engine Oils & Lubricants', 1),
                        ('Castrol', 'Engine Oils & Lubricants', 1),
                        ('Shell Advance', 'Motorcycle Oils', 1),
                        ('Yamalube', 'Yamaha Genuine Parts & Oils', 1),
                        ('Honda OEM', 'Honda Genuine Parts & Oils', 1),
                        ('Maxxis', 'Motorcycle Tires & Tubes', 1),
                        ('Michelin', 'Premium Tires', 1),
                        ('Kenda', 'Tires & Tubes', 1),
                        ('Pirelli', 'High Performance Tires', 1),
                        ('Brembo', 'Brakes & Calipers', 1),
                        ('RCB (Racing Boy)', 'Brakes, Shocks & Accessories', 1),
                        ('NGK', 'Spark Plugs', 1),
                        ('Denso', 'Spark Plugs & Electrical', 1),
                        ('RK Takasago', 'Chains & Sprockets', 1),
                        ('DID', 'Chains & Sprockets', 1),
                        ('Koso', 'Gauges & Electrical Accessories', 1),
                        ('Generic / OEM', 'Standard Replacement Parts', 1);
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
                    BEGIN
                        CREATE TABLE Suppliers (
                            SupplierID INT IDENTITY(1,1) PRIMARY KEY,
                            SupplierName NVARCHAR(150) NOT NULL,
                            ContactPerson NVARCHAR(100) NULL,
                            Phone NVARCHAR(50) NULL,
                            Email NVARCHAR(100) NULL,
                            Address NVARCHAR(255) NULL,
                            IsActive BIT DEFAULT 1
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Suppliers') AND name = 'IsActive')
                        BEGIN
                            ALTER TABLE Suppliers ADD IsActive BIT DEFAULT 1;
                        END
                        UPDATE Suppliers SET IsActive = 1 WHERE IsActive IS NULL;
                    END

                    IF NOT EXISTS (SELECT 1 FROM Suppliers)
                    BEGIN
                        INSERT INTO Suppliers (SupplierName, ContactPerson, Phone, Email, Address, IsActive) VALUES
                        ('Mega Motor Parts Dist.', 'Robert Tan', '0917-123-4567', 'megamotor@supply.com', 'Quezon City', 1),
                        ('Apex Cycle Wholesale Inc.', 'Elena Reyes', '0920-987-6543', 'apexcycle@parts.com', 'Manila', 1),
                        ('SpeedTech Lubricants Ph', 'Carlos Mendoza', '0918-555-7788', 'speedtech@oils.com', 'Pasig City', 1);
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
                    BEGIN
                        CREATE TABLE Products (
                            ProductID INT IDENTITY(1,1) PRIMARY KEY,
                            Barcode NVARCHAR(50) NOT NULL,
                            ProductName NVARCHAR(200) NOT NULL,
                            CategoryID INT NULL,
                            BrandID INT NULL,
                            CostPrice DECIMAL(18,2) DEFAULT 0,
                            SellingPrice DECIMAL(18,2) DEFAULT 0,
                            StockQuantity INT DEFAULT 0,
                            ReorderLevel INT DEFAULT 5,
                            IsActive BIT DEFAULT 1
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'IsActive')
                            ALTER TABLE Products ADD IsActive BIT DEFAULT 1;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ReorderLevel')
                            ALTER TABLE Products ADD ReorderLevel INT DEFAULT 5;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'CostPrice')
                            ALTER TABLE Products ADD CostPrice DECIMAL(18,2) DEFAULT 0;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'SupplierID')
                            ALTER TABLE Products ADD SupplierID INT NULL;
                                                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ImagePath')
                            ALTER TABLE Products ADD ImagePath NVARCHAR(500) NULL;
                        UPDATE Products SET IsActive = 1 WHERE IsActive IS NULL;
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrders')
                    BEGIN
                        CREATE TABLE PurchaseOrders (
                            POID INT IDENTITY(1,1) PRIMARY KEY,
                            PONumber NVARCHAR(50) NOT NULL,
                            SupplierID INT NOT NULL,
                            OrderDate DATETIME DEFAULT GETDATE(),
                            ExpectedDate DATETIME NULL,
                            TotalAmount DECIMAL(18,2) DEFAULT 0,
                            Status NVARCHAR(50) DEFAULT 'Draft',
                            RequesterName NVARCHAR(100) NULL,
                            Remarks NVARCHAR(MAX) NULL,
                            Terms NVARCHAR(100) NULL,
                            ShippedVia NVARCHAR(100) NULL
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'PONumber')
                            ALTER TABLE PurchaseOrders ADD PONumber NVARCHAR(50) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'SupplierID')
                            ALTER TABLE PurchaseOrders ADD SupplierID INT NOT NULL DEFAULT 1;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'OrderDate')
                            ALTER TABLE PurchaseOrders ADD OrderDate DATETIME NOT NULL DEFAULT GETDATE();
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'ExpectedDate')
                            ALTER TABLE PurchaseOrders ADD ExpectedDate DATETIME NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'TotalAmount')
                            ALTER TABLE PurchaseOrders ADD TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'Status')
                            ALTER TABLE PurchaseOrders ADD Status NVARCHAR(50) NOT NULL DEFAULT 'Draft';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'RequesterName')
                            ALTER TABLE PurchaseOrders ADD RequesterName NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'Remarks')
                            ALTER TABLE PurchaseOrders ADD Remarks NVARCHAR(MAX) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'Terms')
                            ALTER TABLE PurchaseOrders ADD Terms NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'ShippedVia')
                            ALTER TABLE PurchaseOrders ADD ShippedVia NVARCHAR(100) NULL;

                        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'PODate')
                        BEGIN
                            EXEC('UPDATE PurchaseOrders SET OrderDate = PODate WHERE OrderDate IS NULL');
                        END
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrderItems')
                    BEGIN
                        CREATE TABLE PurchaseOrderItems (
                            POItemID INT IDENTITY(1,1) PRIMARY KEY,
                            POID INT NOT NULL,
                            ProductID INT NULL,
                            ItemDescription NVARCHAR(255) NOT NULL,
                            Unit NVARCHAR(50) DEFAULT 'pcs',
                            Quantity INT NOT NULL,
                            UnitPrice DECIMAL(18,2) NOT NULL,
                            TotalPrice DECIMAL(18,2) NOT NULL
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrderItems') AND name = 'POID')
                            ALTER TABLE PurchaseOrderItems ADD POID INT NOT NULL DEFAULT 0;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrderItems') AND name = 'ProductID')
                            ALTER TABLE PurchaseOrderItems ADD ProductID INT NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrderItems') AND name = 'ItemDescription')
                            ALTER TABLE PurchaseOrderItems ADD ItemDescription NVARCHAR(255) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrderItems') AND name = 'Unit')
                            ALTER TABLE PurchaseOrderItems ADD Unit NVARCHAR(50) DEFAULT 'pcs';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrderItems') AND name = 'Quantity')
                            ALTER TABLE PurchaseOrderItems ADD Quantity INT NOT NULL DEFAULT 1;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrderItems') AND name = 'UnitPrice')
                            ALTER TABLE PurchaseOrderItems ADD UnitPrice DECIMAL(18,2) NOT NULL DEFAULT 0;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrderItems') AND name = 'TotalPrice')
                            ALTER TABLE PurchaseOrderItems ADD TotalPrice DECIMAL(18,2) NOT NULL DEFAULT 0;
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransactions')
                    BEGIN
                        CREATE TABLE StockTransactions (
                            TransactionID INT IDENTITY(1,1) PRIMARY KEY,
                            ProductID INT NOT NULL,
                            TransactionType NVARCHAR(50) NOT NULL,
                            Quantity INT NOT NULL,
                            ReferenceNumber NVARCHAR(100) NULL,
                            Reason NVARCHAR(255) NULL,
                            Notes NVARCHAR(MAX) NULL,
                            ProcessedBy NVARCHAR(100) NULL,
                            TransactionDate DATETIME DEFAULT GETDATE()
                        );
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockTransactions') AND name = 'ReferenceNumber')
                            ALTER TABLE StockTransactions ADD ReferenceNumber NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockTransactions') AND name = 'Reason')
                            ALTER TABLE StockTransactions ADD Reason NVARCHAR(255) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockTransactions') AND name = 'Notes')
                            ALTER TABLE StockTransactions ADD Notes NVARCHAR(MAX) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockTransactions') AND name = 'ProcessedBy')
                            ALTER TABLE StockTransactions ADD ProcessedBy NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockTransactions') AND name = 'TransactionDate')
                            ALTER TABLE StockTransactions ADD TransactionDate DATETIME DEFAULT GETDATE();
                    END";
                using var cmd = new SqlCommand(script, con);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static List<CategoryModel> GetDefaultCategories()
        {
            return new List<CategoryModel>
            {
                new CategoryModel { CategoryID = 1, CategoryName = "Engine Oil & Lubricants", Description = "Engine oils, gear oils, coolants" },
                new CategoryModel { CategoryID = 2, CategoryName = "Tires & Tubes", Description = "Tubeless tires, interior tubes" },
                new CategoryModel { CategoryID = 3, CategoryName = "Brakes & Pads", Description = "Brake pads, shoes, discs" },
                new CategoryModel { CategoryID = 4, CategoryName = "Batteries & Electrical", Description = "Batteries, fuses, relays" },
                new CategoryModel { CategoryID = 5, CategoryName = "Spark Plugs", Description = "Standard & iridium plugs" },
                new CategoryModel { CategoryID = 6, CategoryName = "Chains & Sprockets", Description = "Drive chains & sprockets" },
                new CategoryModel { CategoryID = 7, CategoryName = "Filters", Description = "Air, oil & fuel filters" },
                new CategoryModel { CategoryID = 8, CategoryName = "Suspension & Shocks", Description = "Shock absorbers & fork oil" },
                new CategoryModel { CategoryID = 9, CategoryName = "Lighting & Bulbs", Description = "Bulbs, LEDs, signals" },
                new CategoryModel { CategoryID = 10, CategoryName = "Accessories & Helmets", Description = "Mirrors, grips, boxes" },
                new CategoryModel { CategoryID = 11, CategoryName = "Bearings & Seals", Description = "Bearings and oil seals" },
                new CategoryModel { CategoryID = 12, CategoryName = "Belts & CVT Parts", Description = "Drive belts, flyballs" }
            };
        }

        public static List<BrandModel> GetDefaultBrands()
        {
            return new List<BrandModel>
            {
                new BrandModel { BrandID = 1, BrandName = "Motul", Description = "Engine Oils & Lubricants" },
                new BrandModel { BrandID = 2, BrandName = "Castrol", Description = "Engine Oils & Lubricants" },
                new BrandModel { BrandID = 3, BrandName = "Shell Advance", Description = "Motorcycle Oils" },
                new BrandModel { BrandID = 4, BrandName = "Yamalube", Description = "Yamaha Genuine Parts & Oils" },
                new BrandModel { BrandID = 5, BrandName = "Honda OEM", Description = "Honda Genuine Parts & Oils" },
                new BrandModel { BrandID = 6, BrandName = "Maxxis", Description = "Motorcycle Tires & Tubes" },
                new BrandModel { BrandID = 7, BrandName = "Michelin", Description = "Premium Tires" },
                new BrandModel { BrandID = 8, BrandName = "Kenda", Description = "Tires & Tubes" },
                new BrandModel { BrandID = 9, BrandName = "Pirelli", Description = "High Performance Tires" },
                new BrandModel { BrandID = 10, BrandName = "Brembo", Description = "Brakes & Calipers" },
                new BrandModel { BrandID = 11, BrandName = "RCB (Racing Boy)", Description = "Brakes & Shocks" },
                new BrandModel { BrandID = 12, BrandName = "NGK", Description = "Spark Plugs" },
                new BrandModel { BrandID = 13, BrandName = "Denso", Description = "Spark Plugs & Electrical" },
                new BrandModel { BrandID = 14, BrandName = "RK Takasago", Description = "Chains & Sprockets" },
                new BrandModel { BrandID = 15, BrandName = "DID", Description = "Chains & Sprockets" },
                new BrandModel { BrandID = 16, BrandName = "Koso", Description = "Gauges & Accessories" },
                new BrandModel { BrandID = 17, BrandName = "Generic / OEM", Description = "Standard Replacement Parts" }
            };
        }

        public static List<SupplierModel> GetDefaultSuppliers()
        {
            return new List<SupplierModel>
            {
                new SupplierModel { SupplierID = 1, SupplierName = "Mega Motor Parts Dist.", ContactPerson = "Robert Tan", Phone = "0917-123-4567", Email = "megamotor@supply.com", Address = "Quezon City" },
                new SupplierModel { SupplierID = 2, SupplierName = "Apex Cycle Wholesale Inc.", ContactPerson = "Elena Reyes", Phone = "0920-987-6543", Email = "apexcycle@parts.com", Address = "Manila" },
                new SupplierModel { SupplierID = 3, SupplierName = "SpeedTech Lubricants Ph", ContactPerson = "Carlos Mendoza", Phone = "0918-555-7788", Email = "speedtech@oils.com", Address = "Pasig City" }
            };
        }

        public static string GenerateSKU(string categoryName, string brandName = "", string productName = "")
        {
            string cCode = GetCategoryCode(categoryName);
            string prefix = $"BK-{cCode}-";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                // Find all existing barcodes with this category prefix
                string sql = "SELECT Barcode FROM Products WHERE Barcode LIKE @pattern";
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@pattern", $"{prefix}%");
                int maxNum = 0;
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string code = rdr["Barcode"]?.ToString() ?? "";
                    // Extract trailing number (e.g. from BK-OIL-007, extracts 7)
                    var match = System.Text.RegularExpressions.Regex.Match(code, @"\d+$");
                    if (match.Success && int.TryParse(match.Value, out int n))
                    {
                        if (n > maxNum) maxNum = n;
                    }
                }
                // Next sequential number with 3-digit zero padding (001, 002, 008, etc.)
                int nextNum = maxNum + 1;
                return $"{prefix}{nextNum:D3}";
            }
            catch
            {
                return $"{prefix}001";
            }
        }

        private static string GetCategoryCode(string cat)
        {
            if (string.IsNullOrWhiteSpace(cat)) return "PRD";
            cat = cat.Trim();
            // Mappings matching all 8 categories in your Maintenance database:
            if (cat.Contains("Oil", StringComparison.OrdinalIgnoreCase) || cat.Contains("Lubricant", StringComparison.OrdinalIgnoreCase)) return "OIL";
            if (cat.Contains("Transmission", StringComparison.OrdinalIgnoreCase) || cat.Contains("CVT", StringComparison.OrdinalIgnoreCase) || cat.Contains("Belt", StringComparison.OrdinalIgnoreCase)) return "DRV";
            if (cat.Contains("Brake", StringComparison.OrdinalIgnoreCase) || cat.Contains("Disc", StringComparison.OrdinalIgnoreCase) || cat.Contains("Cable", StringComparison.OrdinalIgnoreCase)) return "BRK";
            if (cat.Contains("Tire", StringComparison.OrdinalIgnoreCase) || cat.Contains("Tube", StringComparison.OrdinalIgnoreCase) || cat.Contains("Mag", StringComparison.OrdinalIgnoreCase)) return "TIR";
            if (cat.Contains("Electrical", StringComparison.OrdinalIgnoreCase) || cat.Contains("Battery", StringComparison.OrdinalIgnoreCase) || cat.Contains("FI", StringComparison.OrdinalIgnoreCase)) return "ELE";
            if (cat.Contains("Engine Part", StringComparison.OrdinalIgnoreCase) || cat.Contains("Cylinder", StringComparison.OrdinalIgnoreCase) || cat.Contains("Piston", StringComparison.OrdinalIgnoreCase)) return "ENG";
            if (cat.Contains("Suspension", StringComparison.OrdinalIgnoreCase) || cat.Contains("Shock", StringComparison.OrdinalIgnoreCase) || cat.Contains("Fork", StringComparison.OrdinalIgnoreCase)) return "SUS";
            if (cat.Contains("Performance", StringComparison.OrdinalIgnoreCase) || cat.Contains("Upgrade", StringComparison.OrdinalIgnoreCase) || cat.Contains("Accessory", StringComparison.OrdinalIgnoreCase)) return "ACC";
            var clean = System.Text.RegularExpressions.Regex.Replace(cat, @"[^a-zA-Z0-9]", "");
            return clean.Length >= 3 ? clean.Substring(0, 3).ToUpper() : clean.PadRight(3, 'X').ToUpper();
        }

        private static string GetBrandCode(string brand)
        {
            if (string.IsNullOrWhiteSpace(brand)) return "GEN";
            brand = brand.Trim();
            if (brand.Contains("Motul", StringComparison.OrdinalIgnoreCase)) return "MOT";
            if (brand.Contains("Castrol", StringComparison.OrdinalIgnoreCase)) return "CAS";
            if (brand.Contains("Maxxis", StringComparison.OrdinalIgnoreCase)) return "MAX";
            if (brand.Contains("Michelin", StringComparison.OrdinalIgnoreCase)) return "MIC";
            if (brand.Contains("Yamaha", StringComparison.OrdinalIgnoreCase) || brand.Contains("Yamalube", StringComparison.OrdinalIgnoreCase)) return "YAM";
            if (brand.Contains("Honda", StringComparison.OrdinalIgnoreCase)) return "HON";
            if (brand.Contains("Brembo", StringComparison.OrdinalIgnoreCase)) return "BRM";
            if (brand.Contains("RCB", StringComparison.OrdinalIgnoreCase) || brand.Contains("Racing Boy", StringComparison.OrdinalIgnoreCase)) return "RCB";
            if (brand.Contains("Pirelli", StringComparison.OrdinalIgnoreCase)) return "PIR";
            if (brand.Contains("Shell", StringComparison.OrdinalIgnoreCase)) return "SHL";
            if (brand.Contains("NGK", StringComparison.OrdinalIgnoreCase)) return "NGK";
            if (brand.Contains("Denso", StringComparison.OrdinalIgnoreCase)) return "DNS";
            if (brand.Contains("RK", StringComparison.OrdinalIgnoreCase)) return "RKT";
            if (brand.Contains("DID", StringComparison.OrdinalIgnoreCase)) return "DID";
            if (brand.Contains("Koso", StringComparison.OrdinalIgnoreCase)) return "KSO";
            if (brand.Contains("Generic", StringComparison.OrdinalIgnoreCase)) return "GEN";

            var clean = System.Text.RegularExpressions.Regex.Replace(brand, @"[^a-zA-Z0-9]", "");
            return clean.Length >= 3 ? clean.Substring(0, 3).ToUpper() : clean.PadRight(3, 'X').ToUpper();
        }

        private static string GetProductNameCode(string prodName)
        {
            if (string.IsNullOrWhiteSpace(prodName)) return "";

            var words = prodName.Split(new[] { ' ', '-', '_', '/', ',', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            var candidateParts = new List<string>();

            foreach (var w in words)
            {
                string clean = System.Text.RegularExpressions.Regex.Replace(w, @"[^a-zA-Z0-9]", "").ToUpper();
                if (string.IsNullOrEmpty(clean)) continue;

                if (clean == "ENGINE" || clean == "OIL" || clean == "TIRE" || clean == "BRAKE" || clean == "PAD" ||
                    clean == "PLUG" || clean == "FOR" || clean == "THE" || clean == "AND" || clean == "MOTOR" || clean == "CYCLE")
                    continue;

                if (clean.Any(char.IsDigit) || clean.Length >= 3)
                {
                    candidateParts.Add(clean);
                }
            }

            if (candidateParts.Count > 0)
            {
                string joined = string.Join("-", candidateParts.Take(2));
                if (joined.Length > 12) joined = joined.Substring(0, 12);
                return joined;
            }

            string alphaNumOnly = System.Text.RegularExpressions.Regex.Replace(prodName, @"[^a-zA-Z0-9]", "").ToUpper();
            return alphaNumOnly.Length >= 3 ? alphaNumOnly.Substring(0, Math.Min(5, alphaNumOnly.Length)) : alphaNumOnly;
        }

        public static List<CategoryModel> GetCategories()
        {
            var list = new List<CategoryModel>();
            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                // Query real database categories
                using var cmd = new SqlCommand("SELECT CategoryID, CategoryName FROM Categories WHERE ISNULL(IsActive, 1) = 1 ORDER BY CategoryName", con);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new CategoryModel
                    {
                        CategoryID = Convert.ToInt32(rdr["CategoryID"]),
                        CategoryName = rdr["CategoryName"].ToString() ?? ""
                    });
                }
            }
            catch { }
            if (list.Count == 0)
            {
                list = GetDefaultCategories();
            }
            return list;
        }

        public static List<BrandModel> GetBrands()
        {
            var list = new List<BrandModel>();
            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                // Query real database brands (matches Maintenance)
                using var cmd = new SqlCommand("SELECT BrandID, BrandName FROM Brands WHERE ISNULL(IsActive, 1) = 1 ORDER BY BrandName", con);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new BrandModel
                    {
                        BrandID = Convert.ToInt32(rdr["BrandID"]),
                        BrandName = rdr["BrandName"].ToString() ?? ""
                    });
                }
            }
            catch { }
            if (list.Count == 0)
            {
                list = GetDefaultBrands();
            }
            return list;
        }

        public static List<SupplierModel> GetSuppliers()
        {
            var list = new List<SupplierModel>();
            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                using var cmd = new SqlCommand("SELECT SupplierID, SupplierName, ISNULL(ContactPerson, '') AS ContactPerson, ISNULL(Phone, '') AS Phone, ISNULL(Email, '') AS Email, ISNULL(Address, '') AS Address FROM Suppliers WHERE ISNULL(IsActive, 1) = 1 ORDER BY SupplierName", con);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new SupplierModel
                    {
                        SupplierID = Convert.ToInt32(rdr["SupplierID"]),
                        SupplierName = rdr["SupplierName"].ToString() ?? "",
                        ContactPerson = rdr["ContactPerson"]?.ToString() ?? "",
                        Phone = rdr["Phone"]?.ToString() ?? "",
                        Email = rdr["Email"]?.ToString() ?? "",
                        Address = rdr["Address"]?.ToString() ?? ""
                    });
                }
            }
            catch { }

            if (list.Count == 0)
            {
                list = GetDefaultSuppliers();
            }
            return list;
        }

        public static List<ProductItem> GetProducts(string search = "", string status = "All Stocks")
        {
            var list = new List<ProductItem>();
            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();

                string query = @"
                    SELECT p.ProductID, p.Barcode, p.ProductName, p.CategoryID, 
                           ISNULL(c.CategoryName, 'General') AS CategoryName,
                           p.BrandID, 
                           ISNULL(b.BrandName, 'Generic') AS BrandName,
                           p.SupplierID,
                           ISNULL(s.SupplierName, 'Not Assigned') AS SupplierName,
                           ISNULL(p.CostPrice, 0) AS CostPrice,
                           ISNULL(p.SellingPrice, 0) AS SellingPrice,
                           ISNULL(p.StockQuantity, 0) AS StockQuantity,
                           ISNULL(p.ReorderLevel, 5) AS ReorderLevel,
                           ISNULL(p.IsActive, 1) AS IsActive,
                           ISNULL(p.ImagePath, '') AS ImagePath
                    FROM Products p
                    LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
                    LEFT JOIN Brands b ON p.BrandID = b.BrandID
                    LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
                    WHERE ISNULL(p.IsActive, 1) = 1";

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += " AND (p.ProductName LIKE @search OR p.Barcode LIKE @search OR c.CategoryName LIKE @search OR b.BrandName LIKE @search)";
                }

                if (status == "In Stock Only")
                {
                    query += " AND p.StockQuantity > p.ReorderLevel";
                }
                else if (status == "Critical / Low Stock" || status == "Low Stock" || status == "Low Stock Alert")
                {
                    query += " AND p.StockQuantity <= p.ReorderLevel AND p.StockQuantity > 0";
                }
                else if (status == "Out of Stock Only" || status == "Out of Stock")
                {
                    query += " AND p.StockQuantity <= 0";
                }

                query += " ORDER BY p.ProductName ASC";

                using var cmd = new SqlCommand(query, con);
                if (!string.IsNullOrWhiteSpace(search))
                {
                    cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                }

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new ProductItem
                    {
                        ProductID = Convert.ToInt32(rdr["ProductID"]),
                        SKU = rdr["Barcode"].ToString() ?? "",
                        ProductName = rdr["ProductName"].ToString() ?? "",
                        CategoryID = rdr["CategoryID"] != DBNull.Value ? Convert.ToInt32(rdr["CategoryID"]) : 0,
                        CategoryName = rdr["CategoryName"].ToString() ?? "",
                        BrandID = rdr["BrandID"] != DBNull.Value ? Convert.ToInt32(rdr["BrandID"]) : 0,
                        BrandName = rdr["BrandName"].ToString() ?? "",
                        CostPrice = Convert.ToDecimal(rdr["CostPrice"]),
                        UnitPrice = Convert.ToDecimal(rdr["SellingPrice"]),
                        Quantity = Convert.ToInt32(rdr["StockQuantity"]),
                        SupplierID = rdr["SupplierID"] != DBNull.Value ? Convert.ToInt32(rdr["SupplierID"]) : (int?)null,
                        SupplierName = rdr["SupplierName"]?.ToString() ?? "Not Assigned",
                        ReorderLevel = Convert.ToInt32(rdr["ReorderLevel"]),
                        IsActive = Convert.ToBoolean(rdr["IsActive"]),
                        ImagePath = rdr["ImagePath"]?.ToString() ?? ""
                    });
                }
            }
            catch { }
            return list;
        }

        public static InventoryStatsSummary GetInventoryStats()
        {
            var summary = new InventoryStatsSummary();
            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();

                string query = @"
                    SELECT 
                        ISNULL(SUM(CostPrice * StockQuantity), 0) AS TotalValuation,
                        ISNULL(SUM(CASE WHEN StockQuantity <= ReorderLevel AND StockQuantity > 0 THEN 1 ELSE 0 END), 0) AS LowStockCount,
                        ISNULL(SUM(CASE WHEN StockQuantity <= 0 THEN 1 ELSE 0 END), 0) AS OutOfStockCount,
                        COUNT(ProductID) AS TotalSKUs,
                        ISNULL(SUM(StockQuantity), 0) AS TotalUnits
                    FROM Products 
                    WHERE ISNULL(IsActive, 1) = 1";

                using var cmd = new SqlCommand(query, con);
                using var rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    summary.TotalValuation = Convert.ToDecimal(rdr["TotalValuation"]);
                    summary.LowStockCount = Convert.ToInt32(rdr["LowStockCount"]);
                    summary.OutOfStockCount = Convert.ToInt32(rdr["OutOfStockCount"]);
                    summary.CriticalCount = summary.LowStockCount + summary.OutOfStockCount;
                    summary.TotalSKUs = Convert.ToInt32(rdr["TotalSKUs"]);
                    summary.TotalUnits = Convert.ToInt32(rdr["TotalUnits"]);
                }
            }
            catch { }
            return summary;
        }

        public static InventoryStatsSummary GetInventoryStatsSummary()
        {
            return GetInventoryStats();
        }

        // =========================================================================
        // ── PRODUCT SALES HISTORY AGGREGATION (Retail Sales + Job Orders) ───────
        // =========================================================================
        public static ProductSalesHistorySummary GetProductSalesHistory(int productId)
        {
            var summary = new ProductSalesHistorySummary { ProductID = productId };

            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();

                // Fetch Product Details
                string prodSql = "SELECT Barcode, ProductName, StockQuantity, CostPrice FROM Products WHERE ProductID = @pid";
                using (var cmdProd = new SqlCommand(prodSql, con))
                {
                    cmdProd.Parameters.AddWithValue("@pid", productId);
                    using var rProd = cmdProd.ExecuteReader();
                    if (rProd.Read())
                    {
                        summary.SKU = rProd["Barcode"].ToString() ?? "";
                        summary.ProductName = rProd["ProductName"].ToString() ?? "";
                        summary.CurrentStock = Convert.ToInt32(rProd["StockQuantity"]);
                    }
                }

                // Query itemized history across Sales & JobOrders
                string histSql = @"
                    SELECT 
                        s.SaleDate AS TransDate,
                        s.InvoiceNo AS RefNo,
                        'Retail Sale' AS TransType,
                        ISNULL(s.CustomerName, 'Walk-in Customer') AS CustomerDetails,
                        sd.Quantity AS QtySold,
                        sd.UnitPrice AS UnitPrice,
                        ISNULL(p.CostPrice, 0) AS CostPrice,
                        ISNULL(u.FullName, 'Cashier') AS CashierUser
                    FROM SalesDetails sd
                    JOIN Sales s ON sd.SaleID = s.SaleID
                    JOIN Products p ON sd.ProductID = p.ProductID
                    LEFT JOIN Users u ON s.UserID = u.UserID
                    WHERE sd.ProductID = @pid AND ISNULL(s.Status, 'Completed') <> 'Voided'

                    UNION ALL

                    SELECT 
                        j.CreatedAt AS TransDate,
                        j.JobOrderNo AS RefNo,
                        'Job Order' AS TransType,
                        ISNULL(j.CustomerName, 'Walk-in Customer') + 
                            CASE WHEN j.MotorcycleUnit IS NOT NULL AND j.MotorcycleUnit <> '' THEN ' (' + j.MotorcycleUnit + ')' ELSE '' END AS CustomerDetails,
                        joi.Quantity AS QtySold,
                        joi.UnitPrice AS UnitPrice,
                        ISNULL(p.CostPrice, 0) AS CostPrice,
                        ISNULL(u.FullName, 'Cashier') AS CashierUser
                    FROM JobOrderItems joi
                    JOIN JobOrders j ON joi.JobOrderID = j.JobOrderID
                    JOIN Products p ON (joi.ProductID = p.ProductID OR (joi.ProductID IS NULL AND joi.ItemName = p.ProductName))
                    LEFT JOIN Users u ON j.CashierID = u.UserID
                    WHERE p.ProductID = @pid AND ISNULL(j.Status, 'Completed') <> 'Voided'

                    ORDER BY TransDate DESC";

                using (var cmdHist = new SqlCommand(histSql, con))
                {
                    cmdHist.Parameters.AddWithValue("@pid", productId);
                    using var r = cmdHist.ExecuteReader();
                    while (r.Read())
                    {
                        int qty = Convert.ToInt32(r["QtySold"]);
                        decimal uPrice = Convert.ToDecimal(r["UnitPrice"]);
                        decimal cPrice = Convert.ToDecimal(r["CostPrice"]);

                        summary.Entries.Add(new ProductSalesHistoryEntry
                        {
                            TransactionDate = Convert.ToDateTime(r["TransDate"]),
                            ReferenceNo = r["RefNo"].ToString() ?? "",
                            TransactionType = r["TransType"].ToString() ?? "Sale",
                            CustomerDetails = r["CustomerDetails"].ToString() ?? "Walk-in Customer",
                            QuantitySold = qty,
                            UnitPrice = uPrice,
                            CostPrice = cPrice,
                            CashierOrUser = r["CashierUser"].ToString() ?? "Cashier"
                        });

                        summary.TotalUnitsSold += qty;
                        summary.TotalRevenue += (qty * uPrice);
                        summary.TotalCost += (qty * cPrice);
                    }
                }
            }
            catch { }

            return summary;
        }

        public static bool ExportProductSalesHistoryToCsv(ProductSalesHistorySummary history, string filePath, out string error)
        {
            error = "";
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Product Sales History Report - Bike Kings Cycle & Motor Repair");
                sb.AppendLine($"Product SKU: {history.SKU}, Product Name: \"{history.ProductName}\", Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Current Stock: {history.CurrentStock}, Total Units Sold: {history.TotalUnitsSold}, Total Revenue: ₱{history.TotalRevenue:N2}, Total Profit: ₱{history.TotalProfit:N2} ({history.MarginPercent:F1}%)");
                sb.AppendLine();
                sb.AppendLine("Date & Time,Reference ID,Type,Customer / Details,Qty Sold,Unit Price (PHP),Total Revenue (PHP),Gross Profit (PHP),Cashier / User");

                foreach (var item in history.Entries)
                {
                    sb.AppendLine($"\"{item.FormattedDate}\",\"{item.ReferenceNo}\",\"{item.TransactionType}\",\"{item.CustomerDetails.Replace("\"", "\"\"")}\",{item.QuantitySold},{item.UnitPrice:F2},{item.TotalRevenue:F2},{item.GrossProfit:F2},\"{item.CashierOrUser}\"");
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AddProduct(string sku, string name, int catId, int brandId, decimal cost, decimal price, int stock, int reorder, out string error, int? supplierId = null, string imagePath = "")
        {
            EnsureDatabaseSchema();
            error = "";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                // 🛡️ Database Check 1: Prevent Duplicate SKU
                string checkSkuSql = "SELECT COUNT(*) FROM Products WHERE Barcode = @sku AND IsActive = 1";
                using (var cmdCheck = new SqlCommand(checkSkuSql, con))
                {
                    cmdCheck.Parameters.AddWithValue("@sku", sku);
                    if ((int)cmdCheck.ExecuteScalar() > 0)
                    {
                        error = $"A product with SKU '{sku}' already exists in the database.";
                        return false;
                    }
                }
                // 🛡️ Database Check 2: Prevent Duplicate Name under same Brand
                string checkNameSql = "SELECT COUNT(*) FROM Products WHERE ProductName = @name AND BrandID = @brand AND IsActive = 1";
                using (var cmdCheckName = new SqlCommand(checkNameSql, con))
                {
                    cmdCheckName.Parameters.AddWithValue("@name", name);
                    cmdCheckName.Parameters.AddWithValue("@brand", brandId);
                    if ((int)cmdCheckName.ExecuteScalar() > 0)
                    {
                        error = $"A product named '{name}' already exists under the selected brand.";
                        return false;
                    }
                }
                string query = @"
                    INSERT INTO Products (Barcode, ProductName, CategoryID, BrandID, SupplierID, CostPrice, SellingPrice, StockQuantity, ReorderLevel, IsActive, ImagePath)
                    VALUES (@sku, @name, @cat, @brand, @supplierId, @cost, @price, @stock, @reorder, 1, @imagePath)";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@sku", sku);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@cat", catId);
                cmd.Parameters.AddWithValue("@brand", brandId);
                cmd.Parameters.AddWithValue("@supplierId", (object)supplierId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cost", cost);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@stock", stock);
                cmd.Parameters.AddWithValue("@reorder", reorder);
                cmd.Parameters.AddWithValue("@imagePath", string.IsNullOrWhiteSpace(imagePath) ? (object)DBNull.Value : imagePath.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool UpdateProduct(int productId, string sku, string name, int catId, int brandId, decimal cost, decimal price, int stock, int reorder, out string error, int? supplierId = null, string imagePath = "")
        {
            EnsureDatabaseSchema();
            error = "";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                // 🛡️ Database Check 1: Prevent Duplicate SKU (excluding self)
                string checkSkuSql = "SELECT COUNT(*) FROM Products WHERE Barcode = @sku AND ProductID <> @id AND IsActive = 1";
                using (var cmdCheck = new SqlCommand(checkSkuSql, con))
                {
                    cmdCheck.Parameters.AddWithValue("@sku", sku);
                    cmdCheck.Parameters.AddWithValue("@id", productId);
                    if ((int)cmdCheck.ExecuteScalar() > 0)
                    {
                        error = $"Another product with SKU '{sku}' already exists in the database.";
                        return false;
                    }
                }
                // 🛡️ Database Check 2: Prevent Duplicate Name under same Brand (excluding self)
                string checkNameSql = "SELECT COUNT(*) FROM Products WHERE ProductName = @name AND BrandID = @brand AND ProductID <> @id AND IsActive = 1";
                using (var cmdCheckName = new SqlCommand(checkNameSql, con))
                {
                    cmdCheckName.Parameters.AddWithValue("@name", name);
                    cmdCheckName.Parameters.AddWithValue("@brand", brandId);
                    cmdCheckName.Parameters.AddWithValue("@id", productId);
                    if ((int)cmdCheckName.ExecuteScalar() > 0)
                    {
                        error = $"Another product named '{name}' already exists under the selected brand.";
                        return false;
                    }
                }
                string query = @"
                    UPDATE Products 
                    SET Barcode = @sku, ProductName = @name, CategoryID = @cat, BrandID = @brand, SupplierID = @supplierId,
                        CostPrice = @cost, SellingPrice = @price, StockQuantity = @stock, ReorderLevel = @reorder, ImagePath = @imagePath
                    WHERE ProductID = @id";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@id", productId);
                cmd.Parameters.AddWithValue("@sku", sku);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@cat", catId);
                cmd.Parameters.AddWithValue("@brand", brandId);
                cmd.Parameters.AddWithValue("@supplierId", (object)supplierId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cost", cost);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@stock", stock);
                cmd.Parameters.AddWithValue("@reorder", reorder);
                cmd.Parameters.AddWithValue("@imagePath", string.IsNullOrWhiteSpace(imagePath) ? (object)DBNull.Value : imagePath.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool DeleteProduct(int productId, out string error)
        {
            error = "";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                using var cmd = new SqlCommand("UPDATE Products SET IsActive = 0 WHERE ProductID = @id", con);
                cmd.Parameters.AddWithValue("@id", productId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool ProcessStockIn(int productId, int quantity, string refNum, int? supplierId, string notes, string processedBy, out string error)
        {
            EnsureDatabaseSchema();
            error = "";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                using var tx = con.BeginTransaction();

                try
                {
                    string updateQuery = "UPDATE Products SET StockQuantity = StockQuantity + @qty WHERE ProductID = @id";
                    using var cmdUpdate = new SqlCommand(updateQuery, con, tx);
                    cmdUpdate.Parameters.AddWithValue("@qty", quantity);
                    cmdUpdate.Parameters.AddWithValue("@id", productId);
                    cmdUpdate.ExecuteNonQuery();

                    string insertTx = @"
                        INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate)
                        VALUES (@pid, 'STOCK_IN', @qty, @ref, 'Supplier Restock', @notes, @user, GETDATE())";
                    using var cmdTx = new SqlCommand(insertTx, con, tx);
                    cmdTx.Parameters.AddWithValue("@pid", productId);
                    cmdTx.Parameters.AddWithValue("@qty", quantity);
                    cmdTx.Parameters.AddWithValue("@ref", refNum ?? "");
                    cmdTx.Parameters.AddWithValue("@notes", notes ?? "");
                    cmdTx.Parameters.AddWithValue("@user", processedBy ?? "System");
                    cmdTx.ExecuteNonQuery();

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool RecordStockIn(int productId, int supplierId, int quantity, string refNum, string notes, string processedBy, out string error)
        {
            return ProcessStockIn(productId, quantity, refNum, supplierId, notes, processedBy, out error);
        }

        public static bool RecordStockIn(int productId, int quantity, string refNum, int? supplierId, string notes, string processedBy, out string error)
        {
            return ProcessStockIn(productId, quantity, refNum, supplierId, notes, processedBy, out error);
        }

        public static bool RecordStockIn(int productId, int quantity, string refNum, string notes, string processedBy, out string error)
        {
            return ProcessStockIn(productId, quantity, refNum, null, notes, processedBy, out error);
        }

        public static bool ProcessStockOut(int productId, int quantity, string refNum, string reason, string notes, string processedBy, out string error)
        {
            EnsureDatabaseSchema();
            error = "";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                using var tx = con.BeginTransaction();

                try
                {
                    string checkQuery = "SELECT StockQuantity FROM Products WHERE ProductID = @id";
                    using var cmdCheck = new SqlCommand(checkQuery, con, tx);
                    cmdCheck.Parameters.AddWithValue("@id", productId);
                    object result = cmdCheck.ExecuteScalar();
                    int currentStock = result != null ? Convert.ToInt32(result) : 0;

                    if (currentStock < quantity)
                    {
                        error = $"Cannot deduct {quantity} units. Current stock is only {currentStock}.";
                        tx.Rollback();
                        return false;
                    }

                    string updateQuery = "UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE ProductID = @id";
                    using var cmdUpdate = new SqlCommand(updateQuery, con, tx);
                    cmdUpdate.Parameters.AddWithValue("@qty", quantity);
                    cmdUpdate.Parameters.AddWithValue("@id", productId);
                    cmdUpdate.ExecuteNonQuery();

                    string insertTx = @"
                        INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate)
                        VALUES (@pid, 'STOCK_OUT', @qty, @ref, @reason, @notes, @user, GETDATE())";
                    using var cmdTx = new SqlCommand(insertTx, con, tx);
                    cmdTx.Parameters.AddWithValue("@pid", productId);
                    cmdTx.Parameters.AddWithValue("@qty", quantity);
                    cmdTx.Parameters.AddWithValue("@ref", refNum ?? "");
                    cmdTx.Parameters.AddWithValue("@reason", reason ?? "Adjustment");
                    cmdTx.Parameters.AddWithValue("@notes", notes ?? "");
                    cmdTx.Parameters.AddWithValue("@user", processedBy ?? "System");
                    cmdTx.ExecuteNonQuery();

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool RecordStockOut(int productId, int quantity, string reason, string notes, string processedBy, out string error)
        {
            return ProcessStockOut(productId, quantity, $"ADJ-{DateTime.Now:yyyyMMddHHmm}", reason, notes, processedBy, out error);
        }

        public static bool RecordStockOut(int productId, int quantity, string refNum, string reason, string notes, string processedBy, out string error)
        {
            return ProcessStockOut(productId, quantity, refNum, reason, notes, processedBy, out error);
        }

        public static List<StockTransactionItem> GetStockTransactions(string filterType = "All Types", string search = "")
        {
            var list = new List<StockTransactionItem>();
            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();

                string query = @"
                    SELECT st.TransactionID, st.TransactionDate, st.TransactionType, 
                           p.Barcode AS SKU, p.ProductName,
                           ISNULL(st.ReferenceNumber, '') AS ReferenceNumber,
                           ISNULL(st.Reason, '') AS Reason,
                           st.Quantity AS QuantityAdjusted,
                           ISNULL(st.ProcessedBy, 'Admin') AS ProcessedBy,
                           ISNULL(st.Notes, '') AS Notes
                    FROM StockTransactions st
                    JOIN Products p ON st.ProductID = p.ProductID
                    WHERE 1=1";

                if (filterType == "Stock In Only")
                {
                    query += " AND st.TransactionType = 'STOCK_IN'";
                }
                else if (filterType == "Stock Out Only")
                {
                    query += " AND st.TransactionType = 'STOCK_OUT'";
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += " AND (p.ProductName LIKE @search OR p.Barcode LIKE @search OR st.ReferenceNumber LIKE @search OR st.Reason LIKE @search)";
                }

                query += " ORDER BY st.TransactionDate DESC";

                using var cmd = new SqlCommand(query, con);
                if (!string.IsNullOrWhiteSpace(search))
                {
                    cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                }

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new StockTransactionItem
                    {
                        TransactionID = Convert.ToInt32(rdr["TransactionID"]),
                        TransactionDate = Convert.ToDateTime(rdr["TransactionDate"]),
                        TransactionType = rdr["TransactionType"].ToString() ?? "",
                        SKU = rdr["SKU"].ToString() ?? "",
                        ProductName = rdr["ProductName"].ToString() ?? "",
                        ReferenceNumber = rdr["ReferenceNumber"].ToString() ?? "",
                        Reason = rdr["Reason"].ToString() ?? "",
                        QuantityAdjusted = Convert.ToInt32(rdr["QuantityAdjusted"]),
                        ProcessedBy = rdr["ProcessedBy"].ToString() ?? "",
                        Notes = rdr["Notes"].ToString() ?? ""
                    });
                }
            }
            catch { }
            return list;
        }

        public static List<StockTransactionItem> GetStockInLogs(string search = "")
        {
            return GetStockTransactions("Stock In Only", search);
        }

        public static List<StockTransactionItem> GetStockOutLogs(string search = "")
        {
            return GetStockTransactions("Stock Out Only", search);
        }

        public static List<StockAnalyticsItem> GetStockAnalytics()
        {
            var list = new List<StockAnalyticsItem>();
            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                // Safe check: ensure StockTransactions table exists
                string initTable = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransactions')
                    BEGIN
                        CREATE TABLE StockTransactions (
                            TransactionID INT IDENTITY(1,1) PRIMARY KEY,
                            ProductID INT NOT NULL,
                            TransactionType NVARCHAR(50) NOT NULL,
                            Quantity INT NOT NULL,
                            ReferenceNumber NVARCHAR(100) NULL,
                            Reason NVARCHAR(255) NULL,
                            Notes NVARCHAR(MAX) NULL,
                            ProcessedBy NVARCHAR(100) NULL,
                            TransactionDate DATETIME DEFAULT GETDATE()
                        );
                    END";
                using (var cmdInit = new SqlCommand(initTable, con))
                {
                    cmdInit.ExecuteNonQuery();
                }
                // Query integrates retail POS sales, Job Order parts, and manual stock-outs over the last 30 days
                string query = @"
                    SELECT p.ProductID, p.Barcode AS SKU, p.ProductName, 
                           ISNULL(c.CategoryName, 'General') AS CategoryName,
                           ISNULL(p.StockQuantity, 0) AS CurrentStock,
                           (
                               ISNULL((
                                   SELECT SUM(sd.Quantity)
                                   FROM SalesDetails sd
                                   JOIN Sales s ON sd.SaleID = s.SaleID
                                   WHERE sd.ProductID = p.ProductID 
                                     AND s.SaleDate >= DATEADD(DAY, -30, GETDATE())
                                     AND ISNULL(s.Status, 'Completed') <> 'Voided'
                               ), 0)
                               +
                               ISNULL((
                                   SELECT SUM(joi.Quantity)
                                   FROM JobOrderItems joi
                                   JOIN JobOrders j ON joi.JobOrderID = j.JobOrderID
                                   WHERE (joi.ProductID = p.ProductID OR (joi.ProductID IS NULL AND joi.ItemName = p.ProductName))
                                     AND j.CreatedAt >= DATEADD(DAY, -30, GETDATE())
                                     AND ISNULL(j.Status, 'Completed') <> 'Voided'
                               ), 0)
                               +
                               ISNULL((
                                   SELECT SUM(st.Quantity) 
                                   FROM StockTransactions st 
                                   WHERE st.ProductID = p.ProductID 
                                     AND st.TransactionType = 'STOCK_OUT'
                                     AND st.TransactionDate >= DATEADD(DAY, -30, GETDATE())
                               ), 0)
                           ) AS UnitsSoldLast30Days
                    FROM Products p
                    LEFT JOIN Categories c ON p.CategoryID = c.CategoryID
                    WHERE ISNULL(p.IsActive, 1) = 1
                    ORDER BY UnitsSoldLast30Days DESC, p.ProductName ASC";
                using var cmd = new SqlCommand(query, con);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    int sold = Convert.ToInt32(rdr["UnitsSoldLast30Days"]);
                    int stock = Convert.ToInt32(rdr["CurrentStock"]);
                    string velocity;
                    string bg;
                    string fg;
                    string rec;
                    if (sold >= 30)
                    {
                        velocity = "FAST-MOVING (CLASS A)";
                        bg = "#0E2A1E";
                        fg = "#10B981";
                        rec = stock <= 10 ? "URGENT: Reorder now! High turnover risk of stockout." : "Optimal stock. Maintain weekly reorder buffer.";
                    }
                    else if (sold >= 10)
                    {
                        velocity = "MODERATE (CLASS B)";
                        bg = "#0F2838";
                        fg = "#38BDF8";
                        rec = stock <= 5 ? "Plan purchase order in upcoming restocking cycle." : "Healthy inventory level. Normal restock schedule.";
                    }
                    else if (sold > 0)
                    {
                        velocity = "SLOW-MOVING (CLASS C)";
                        bg = "#2D2214";
                        fg = "#F59E0B";
                        rec = stock > 20 ? "Overstocked. Reduce PO volume or create bundle promos." : "Low demand. Maintain minimal safety stock.";
                    }
                    else
                    {
                        velocity = "DEAD STOCK / ZERO MOVEMENT";
                        bg = "#2D1214";
                        fg = "#EF4444";
                        rec = "No sales in 30 days. Consider discount sale or vendor return.";
                    }
                    list.Add(new StockAnalyticsItem
                    {
                        ProductID = Convert.ToInt32(rdr["ProductID"]),
                        SKU = rdr["SKU"].ToString() ?? "",
                        ProductName = rdr["ProductName"].ToString() ?? "",
                        CategoryName = rdr["CategoryName"].ToString() ?? "",
                        UnitsSoldLast30Days = sold,
                        CurrentStock = stock,
                        VelocityCategory = velocity,
                        VelocityBgColor = bg,
                        VelocityFgColor = fg,
                        ActionRecommendation = rec
                    });
                }
            }
            catch { }
            return list;
        }

        public static List<PurchaseOrderItem> GetPurchaseOrders(string filterStatus = "All Statuses", string search = "")
        {
            var list = new List<PurchaseOrderItem>();
            try
            {
                EnsureDatabaseSchema();
                using var con = DatabaseHelper.GetConnection();
                con.Open();

                string query = @"
                    SELECT po.POID, po.PONumber, po.SupplierID, s.SupplierName,
                           ISNULL(s.ContactPerson, '') AS SupplierContact,
                           ISNULL(s.Address, '') AS SupplierAddress,
                           ISNULL(s.Phone, '') AS SupplierPhone,
                           ISNULL(s.Email, '') AS SupplierEmail,
                           po.OrderDate, po.ExpectedDate, po.TotalAmount, po.Status,
                           ISNULL(po.RequesterName, '') AS RequesterName,
                           ISNULL(po.Remarks, '') AS Remarks,
                           ISNULL(po.Terms, 'Net 30') AS Terms,
                           ISNULL(po.ShippedVia, 'Standard Freight') AS ShippedVia
                    FROM PurchaseOrders po
                    JOIN Suppliers s ON po.SupplierID = s.SupplierID
                    WHERE 1=1";

                if (filterStatus != "All Statuses" && !string.IsNullOrWhiteSpace(filterStatus))
                {
                    query += " AND po.Status = @status";
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += " AND (po.PONumber LIKE @search OR s.SupplierName LIKE @search OR po.RequesterName LIKE @search)";
                }

                query += " ORDER BY po.OrderDate DESC";

                using var cmd = new SqlCommand(query, con);
                if (filterStatus != "All Statuses" && !string.IsNullOrWhiteSpace(filterStatus))
                {
                    cmd.Parameters.AddWithValue("@status", filterStatus);
                }
                if (!string.IsNullOrWhiteSpace(search))
                {
                    cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                }

                using var rdr = cmd.ExecuteReader();
                var poList = new List<PurchaseOrderItem>();
                while (rdr.Read())
                {
                    poList.Add(new PurchaseOrderItem
                    {
                        POID = Convert.ToInt32(rdr["POID"]),
                        PONumber = rdr["PONumber"].ToString() ?? "",
                        SupplierID = Convert.ToInt32(rdr["SupplierID"]),
                        SupplierName = rdr["SupplierName"].ToString() ?? "",
                        SupplierContact = rdr["SupplierContact"].ToString() ?? "",
                        SupplierAddress = rdr["SupplierAddress"].ToString() ?? "",
                        SupplierPhone = rdr["SupplierPhone"].ToString() ?? "",
                        SupplierEmail = rdr["SupplierEmail"].ToString() ?? "",
                        OrderDate = Convert.ToDateTime(rdr["OrderDate"]),
                        ExpectedDate = rdr["ExpectedDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rdr["ExpectedDate"]) : null,
                        TotalAmount = Convert.ToDecimal(rdr["TotalAmount"]),
                        Status = rdr["Status"].ToString() ?? "Draft",
                        RequesterName = rdr["RequesterName"].ToString() ?? "",
                        Remarks = rdr["Remarks"].ToString() ?? "",
                        Terms = rdr["Terms"].ToString() ?? "",
                        ShippedVia = rdr["ShippedVia"].ToString() ?? ""
                    });
                }
                rdr.Close();

                foreach (var po in poList)
                {
                    string itemsQuery = @"
                        SELECT POItemID, POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice
                        FROM PurchaseOrderItems
                        WHERE POID = @poid";
                    using var cmdItems = new SqlCommand(itemsQuery, con);
                    cmdItems.Parameters.AddWithValue("@poid", po.POID);
                    using var rdrItems = cmdItems.ExecuteReader();
                    while (rdrItems.Read())
                    {
                        po.Items.Add(new POLineItem
                        {
                            POItemID = Convert.ToInt32(rdrItems["POItemID"]),
                            POID = Convert.ToInt32(rdrItems["POID"]),
                            ProductID = rdrItems["ProductID"] != DBNull.Value ? Convert.ToInt32(rdrItems["ProductID"]) : (int?)null,
                            ItemDescription = rdrItems["ItemDescription"].ToString() ?? "",
                            Unit = rdrItems["Unit"].ToString() ?? "pcs",
                            Quantity = Convert.ToInt32(rdrItems["Quantity"]),
                            UnitPrice = Convert.ToDecimal(rdrItems["UnitPrice"]),
                            TotalPrice = Convert.ToDecimal(rdrItems["TotalPrice"])
                        });
                    }
                    rdrItems.Close();
                }

                list = poList;
            }
            catch { }
            return list;
        }

        public static bool CreatePurchaseOrder(int supplierId, List<POLineItem> items, string remarks, string terms, out string error)
        {
            return CreatePurchaseOrder(supplierId, null, "Store Clerk", terms, "Standard Freight", remarks, items, out _, out error);
        }

        public static bool CreatePurchaseOrder(int supplierId, DateTime? expectedDate, string requester, string terms, string shippedVia, string remarks, List<POLineItem> items, out string poNumber, out string error)
        {
            EnsureDatabaseSchema();
            poNumber = $"PO-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            error = "";

            if (items == null || items.Count == 0)
            {
                error = "Purchase order must contain at least one item.";
                return false;
            }

            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                using var tx = con.BeginTransaction();

                try
                {
                    decimal totalAmount = 0;
                    foreach (var itm in items)
                    {
                        itm.TotalPrice = itm.Quantity * itm.UnitPrice;
                        totalAmount += itm.TotalPrice;
                    }

                    string insertPO = @"
                        INSERT INTO PurchaseOrders (PONumber, SupplierID, OrderDate, ExpectedDate, TotalAmount, Status, RequesterName, Remarks, Terms, ShippedVia)
                        OUTPUT INSERTED.POID
                        VALUES (@ponum, @suppId, GETDATE(), @expDate, @total, 'Draft', @req, @remarks, @terms, @ship)";

                    using var cmdPO = new SqlCommand(insertPO, con, tx);
                    cmdPO.Parameters.AddWithValue("@ponum", poNumber);
                    cmdPO.Parameters.AddWithValue("@suppId", supplierId);
                    cmdPO.Parameters.AddWithValue("@expDate", (object?)expectedDate ?? DBNull.Value);
                    cmdPO.Parameters.AddWithValue("@total", totalAmount);
                    cmdPO.Parameters.AddWithValue("@req", requester ?? "");
                    cmdPO.Parameters.AddWithValue("@remarks", remarks ?? "");
                    cmdPO.Parameters.AddWithValue("@terms", terms ?? "Net 30");
                    cmdPO.Parameters.AddWithValue("@ship", shippedVia ?? "Standard Freight");

                    int poId = Convert.ToInt32(cmdPO.ExecuteScalar());

                    foreach (var item in items)
                    {
                        string insertItem = @"
                            INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice)
                            VALUES (@poid, @pid, @desc, @unit, @qty, @price, @total)";
                        using var cmdItem = new SqlCommand(insertItem, con, tx);
                        cmdItem.Parameters.AddWithValue("@poid", poId);
                        cmdItem.Parameters.AddWithValue("@pid", (object?)item.ProductID ?? DBNull.Value);
                        cmdItem.Parameters.AddWithValue("@desc", item.ItemDescription ?? "Item");
                        cmdItem.Parameters.AddWithValue("@unit", item.Unit ?? "pcs");
                        cmdItem.Parameters.AddWithValue("@qty", item.Quantity);
                        cmdItem.Parameters.AddWithValue("@price", item.UnitPrice);
                        cmdItem.Parameters.AddWithValue("@total", item.TotalPrice);
                        cmdItem.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool CreatePurchaseOrder(int supplierId, DateTime? expectedDate, string requester, string remarks, List<POLineItem> items, out string poNumber, out string error)
        {
            return CreatePurchaseOrder(supplierId, expectedDate, requester, "Net 30", "Standard Freight", remarks, items, out poNumber, out error);
        }

        public static bool UpdatePurchaseOrder(int poId, int supplierId, DateTime? expectedDate, string requester, string terms, string shippedVia, string remarks, List<POLineItem> items, out string error)
        {
            EnsureDatabaseSchema();
            error = "";

            if (items == null || items.Count == 0)
            {
                error = "Purchase order must contain at least one item.";
                return false;
            }

            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                using var tx = con.BeginTransaction();

                try
                {
                    decimal totalAmount = 0;
                    foreach (var itm in items)
                    {
                        itm.TotalPrice = itm.Quantity * itm.UnitPrice;
                        totalAmount += itm.TotalPrice;
                    }

                    string updatePO = @"
                        UPDATE PurchaseOrders 
                        SET SupplierID = @suppId, ExpectedDate = @expDate, TotalAmount = @total,
                            RequesterName = @req, Remarks = @remarks, Terms = @terms, ShippedVia = @ship
                        WHERE POID = @poid";

                    using var cmdPO = new SqlCommand(updatePO, con, tx);
                    cmdPO.Parameters.AddWithValue("@poid", poId);
                    cmdPO.Parameters.AddWithValue("@suppId", supplierId);
                    cmdPO.Parameters.AddWithValue("@expDate", (object?)expectedDate ?? DBNull.Value);
                    cmdPO.Parameters.AddWithValue("@total", totalAmount);
                    cmdPO.Parameters.AddWithValue("@req", requester ?? "");
                    cmdPO.Parameters.AddWithValue("@remarks", remarks ?? "");
                    cmdPO.Parameters.AddWithValue("@terms", terms ?? "Net 30");
                    cmdPO.Parameters.AddWithValue("@ship", shippedVia ?? "Standard Freight");
                    cmdPO.ExecuteNonQuery();

                    string deleteItems = "DELETE FROM PurchaseOrderItems WHERE POID = @poid";
                    using var cmdDel = new SqlCommand(deleteItems, con, tx);
                    cmdDel.Parameters.AddWithValue("@poid", poId);
                    cmdDel.ExecuteNonQuery();

                    foreach (var item in items)
                    {
                        string insertItem = @"
                            INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice)
                            VALUES (@poid, @pid, @desc, @unit, @qty, @price, @total)";
                        using var cmdItem = new SqlCommand(insertItem, con, tx);
                        cmdItem.Parameters.AddWithValue("@poid", poId);
                        cmdItem.Parameters.AddWithValue("@pid", (object?)item.ProductID ?? DBNull.Value);
                        cmdItem.Parameters.AddWithValue("@desc", item.ItemDescription ?? "Item");
                        cmdItem.Parameters.AddWithValue("@unit", item.Unit ?? "pcs");
                        cmdItem.Parameters.AddWithValue("@qty", item.Quantity);
                        cmdItem.Parameters.AddWithValue("@price", item.UnitPrice);
                        cmdItem.Parameters.AddWithValue("@total", item.TotalPrice);
                        cmdItem.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool UpdatePurchaseOrder(int poId, int supplierId, List<POLineItem> items, string remarks, out string error)
        {
            return UpdatePurchaseOrder(poId, supplierId, null, "Store Clerk", "Net 30", "Standard Delivery", remarks, items, out error);
        }

        public static bool UpdatePurchaseOrderStatus(int poId, string newStatus, out string error)
        {
            EnsureDatabaseSchema();
            error = "";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                string query = "UPDATE PurchaseOrders SET Status = @status WHERE POID = @id";
                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@status", newStatus);
                cmd.Parameters.AddWithValue("@id", poId);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool ApprovePurchaseOrder(int poId, out string error)
        {
            return UpdatePurchaseOrderStatus(poId, "APPROVED", out error);
        }

        public static bool RejectPurchaseOrder(int poId, out string error)
        {
            return UpdatePurchaseOrderStatus(poId, "REJECTED", out error);
        }

        public static bool ReceivePurchaseOrder(int poId, out string error)
        {
            return ReceivePurchaseOrder(poId, "Admin", out error);
        }

        public static bool ReceivePurchaseOrder(int poId, string receivedBy, out string error)
        {
            EnsureDatabaseSchema();
            error = "";
            try
            {
                using var con = DatabaseHelper.GetConnection();
                con.Open();
                using var tx = con.BeginTransaction();

                try
                {
                    string poQuery = "SELECT PONumber, Status FROM PurchaseOrders WHERE POID = @poid";
                    string poNum = "";
                    string currentStatus = "";
                    using (var cmdPO = new SqlCommand(poQuery, con, tx))
                    {
                        cmdPO.Parameters.AddWithValue("@poid", poId);
                        using var rdr = cmdPO.ExecuteReader();
                        if (rdr.Read())
                        {
                            poNum = rdr["PONumber"].ToString() ?? "";
                            currentStatus = rdr["Status"].ToString() ?? "";
                        }
                    }

                    if ((currentStatus ?? "").Equals("Received", StringComparison.OrdinalIgnoreCase) || (currentStatus ?? "").Equals("Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        error = "This purchase order has already been received.";
                        tx.Rollback();
                        return false;
                    }

                    var items = new List<POLineItem>();
                    string itemsQuery = "SELECT ProductID, ItemDescription, Quantity FROM PurchaseOrderItems WHERE POID = @poid";
                    using (var cmdItems = new SqlCommand(itemsQuery, con, tx))
                    {
                        cmdItems.Parameters.AddWithValue("@poid", poId);
                        using var rdrItems = cmdItems.ExecuteReader();
                        while (rdrItems.Read())
                        {
                            items.Add(new POLineItem
                            {
                                ProductID = rdrItems["ProductID"] != DBNull.Value ? Convert.ToInt32(rdrItems["ProductID"]) : (int?)null,
                                ItemDescription = rdrItems["ItemDescription"].ToString() ?? "",
                                Quantity = Convert.ToInt32(rdrItems["Quantity"])
                            });
                        }
                    }

                    foreach (var itm in items)
                    {
                        if (itm.ProductID.HasValue && itm.ProductID.Value > 0)
                        {
                            string updateProd = "UPDATE Products SET StockQuantity = StockQuantity + @qty WHERE ProductID = @pid";
                            using var cmdProd = new SqlCommand(updateProd, con, tx);
                            cmdProd.Parameters.AddWithValue("@qty", itm.Quantity);
                            cmdProd.Parameters.AddWithValue("@pid", itm.ProductID.Value);
                            cmdProd.ExecuteNonQuery();

                            string insertTx = @"
                                INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate)
                                VALUES (@pid, 'STOCK_IN', @qty, @ref, 'PO Receiving', @notes, @user, GETDATE())";
                            using var cmdTx = new SqlCommand(insertTx, con, tx);
                            cmdTx.Parameters.AddWithValue("@pid", itm.ProductID.Value);
                            cmdTx.Parameters.AddWithValue("@qty", itm.Quantity);
                            cmdTx.Parameters.AddWithValue("@ref", poNum);
                            cmdTx.Parameters.AddWithValue("@notes", $"Goods received from PO {poNum}");
                            cmdTx.Parameters.AddWithValue("@user", receivedBy ?? "Inventory Manager");
                            cmdTx.ExecuteNonQuery();
                        }
                    }

                    string updatePOStatus = "UPDATE PurchaseOrders SET Status = 'COMPLETED' WHERE POID = @poid";
                    using (var cmdUpdateStatus = new SqlCommand(updatePOStatus, con, tx))
                    {
                        cmdUpdateStatus.Parameters.AddWithValue("@poid", poId);
                        cmdUpdateStatus.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
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