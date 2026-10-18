-- ============================================================================
-- BIKE KINGS SYSTEM - COMPLETE DATABASE INITIALIZATION SCRIPT
-- Run this once on any new PC or laptop to set up BikeKingsDB
-- ============================================================================

-- 1. Create Database if it does not exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'BikeKingsDB')
BEGIN
    CREATE DATABASE BikeKingsDB;
    PRINT '✅ Database BikeKingsDB created.';
END
GO

USE BikeKingsDB;
GO

-- 2. Master Users Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        UserID       INT IDENTITY(1,1) PRIMARY KEY,
        FullName     NVARCHAR(100) NOT NULL,
        Username     NVARCHAR(50) NOT NULL UNIQUE,
        Password     NVARCHAR(100) NOT NULL,
        Role         NVARCHAR(50) NOT NULL, -- Admin, Cashier, StoreClerk
        IsActive     BIT NOT NULL DEFAULT 1,
        CreatedAt    DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ Users table created.';
END
GO

-- 3. Master Categories Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE Categories (
        CategoryID   INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL UNIQUE,
        IsActive     BIT NOT NULL DEFAULT 1,
        CreatedAt    DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ Categories table created.';
END
GO

-- 4. Master Brands Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Brands')
BEGIN
    CREATE TABLE Brands (
        BrandID   INT IDENTITY(1,1) PRIMARY KEY,
        BrandName NVARCHAR(100) NOT NULL UNIQUE,
        IsActive  BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ Brands table created.';
END
GO

-- 5. Standalone Mechanics Table (Shop Technicians)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Mechanics')
BEGIN
    CREATE TABLE Mechanics (
        MechanicID     INT IDENTITY(1,1) PRIMARY KEY,
        FullName       NVARCHAR(100) NOT NULL,
        ContactNumber  NVARCHAR(50) NULL,
        Specialization NVARCHAR(100) NULL,
        CommissionRate DECIMAL(5,2) NOT NULL DEFAULT 70.00,
        IsActive       BIT NOT NULL DEFAULT 1
    );
    PRINT '✅ Mechanics table created.';
END
GO

-- 6. Repair Services & Labor Fees Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Services')
BEGIN
    CREATE TABLE Services (
        ServiceID   INT IDENTITY(1,1) PRIMARY KEY,
        ServiceName NVARCHAR(150) NOT NULL UNIQUE,
        LaborFee    DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        IsActive    BIT NOT NULL DEFAULT 1,
        CreatedAt   DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ Services table created.';
END
GO

-- 7. Suppliers Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
BEGIN
    CREATE TABLE Suppliers (
        SupplierID     INT IDENTITY(1,1) PRIMARY KEY,
        SupplierName   NVARCHAR(150) NOT NULL UNIQUE,
        ContactPerson  NVARCHAR(100) NULL,
        ContactNumber  NVARCHAR(50) NULL,
        Phone          NVARCHAR(50) NULL,
        Email          NVARCHAR(100) NULL,
        Address        NVARCHAR(200) NULL,
        IsActive       BIT NOT NULL DEFAULT 1
    );
    PRINT '✅ Suppliers table created.';
END
GO

-- 8. Products / Inventory Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE Products (
        ProductID      INT IDENTITY(1,1) PRIMARY KEY,
        Barcode        NVARCHAR(50) NULL,
        ProductName    NVARCHAR(150) NOT NULL,
        CategoryID     INT FOREIGN KEY REFERENCES Categories(CategoryID),
        BrandID        INT FOREIGN KEY REFERENCES Brands(BrandID),
        SupplierID     INT NULL FOREIGN KEY REFERENCES Suppliers(SupplierID),
        CostPrice      DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        SellingPrice   DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        StockQuantity  INT NOT NULL DEFAULT 0,
        ReorderLevel   INT NOT NULL DEFAULT 5,
        CriticalLimit  INT NOT NULL DEFAULT 2,
        IsActive       BIT NOT NULL DEFAULT 1,
        CreatedAt      DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ Products table created.';
END
GO

-- 9. Sales Header
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sales')
BEGIN
    CREATE TABLE Sales (
        SaleID        INT IDENTITY(1,1) PRIMARY KEY,
        InvoiceNo     NVARCHAR(50) NOT NULL UNIQUE,
        UserID        INT FOREIGN KEY REFERENCES Users(UserID),
        CustomerName  NVARCHAR(100) DEFAULT 'Walk-in Customer',
        PaymentMethod NVARCHAR(50) DEFAULT 'Cash',
        PaymentRef    NVARCHAR(50) NULL,
        SubTotal      DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        Discount      DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        TotalAmount   DECIMAL(10,2) NOT NULL,
        AmountPaid    DECIMAL(10,2) NOT NULL,
        ChangeAmount  DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        Status        NVARCHAR(20) DEFAULT 'Completed',
        SaleDate      DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ Sales table created.';
END
GO

-- 10. Sales Details (Line Items)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SalesDetails')
BEGIN
    CREATE TABLE SalesDetails (
        SaleDetailID INT IDENTITY(1,1) PRIMARY KEY,
        SaleID       INT FOREIGN KEY REFERENCES Sales(SaleID) ON DELETE CASCADE,
        ProductID    INT FOREIGN KEY REFERENCES Products(ProductID),
        Quantity     INT NOT NULL,
        UnitPrice    DECIMAL(10,2) NOT NULL,
        Subtotal     DECIMAL(10,2) NOT NULL
    );
    PRINT '✅ SalesDetails table created.';
END
GO

-- 11. Job Orders Header
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobOrders')
BEGIN
    CREATE TABLE JobOrders (
        JobOrderID     INT IDENTITY(1,1) PRIMARY KEY,
        JobOrderNo     NVARCHAR(50) NOT NULL UNIQUE,
        CustomerName   NVARCHAR(100) NOT NULL,
        ContactNumber  NVARCHAR(20) NULL,
        MotorcycleUnit NVARCHAR(100) NOT NULL DEFAULT 'Motorcycle',
        PlateNumber    NVARCHAR(20) NULL,
        MechanicID     INT FOREIGN KEY REFERENCES Mechanics(MechanicID),
        CashierID      INT FOREIGN KEY REFERENCES Users(UserID),
        TotalLabor     DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        TotalParts     DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        TotalAmount    DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        Status         NVARCHAR(30) NOT NULL DEFAULT 'Pending',
        Notes          NVARCHAR(500) NULL,
        PaymentMethod  NVARCHAR(50) DEFAULT 'Cash',
        PaymentRef     NVARCHAR(50) NULL,
        CreatedAt      DATETIME DEFAULT GETDATE(),
        CompletedAt    DATETIME NULL
    );
    PRINT '✅ JobOrders table created.';
END
GO

-- 12. Job Order Line Items
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobOrderItems')
BEGIN
    CREATE TABLE JobOrderItems (
        JobItemID   INT IDENTITY(1,1) PRIMARY KEY,
        JobOrderID  INT FOREIGN KEY REFERENCES JobOrders(JobOrderID) ON DELETE CASCADE,
        ItemType    NVARCHAR(20) NOT NULL, -- 'Service' or 'Product'
        ServiceID   INT NULL FOREIGN KEY REFERENCES Services(ServiceID),
        ProductID   INT NULL FOREIGN KEY REFERENCES Products(ProductID),
        ItemName    NVARCHAR(150) NOT NULL,
        Quantity    INT NOT NULL DEFAULT 1,
        UnitPrice   DECIMAL(10,2) NOT NULL,
        Subtotal    DECIMAL(10,2) NOT NULL
    );
    PRINT '✅ JobOrderItems table created.';
END
GO

-- 13. Purchase Orders
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrders')
BEGIN
    CREATE TABLE PurchaseOrders (
        POID           INT IDENTITY(1,1) PRIMARY KEY,
        PONumber       NVARCHAR(50) NOT NULL UNIQUE,
        SupplierID     INT FOREIGN KEY REFERENCES Suppliers(SupplierID),
        RequesterName  NVARCHAR(100) NOT NULL DEFAULT 'Admin',
        TotalAmount    DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        Status         NVARCHAR(30) NOT NULL DEFAULT 'Pending Approval',
        Remarks        NVARCHAR(500) NULL,
        RequestedBy    INT FOREIGN KEY REFERENCES Users(UserID),
        Terms          NVARCHAR(50) NULL,
        ShippedVia     NVARCHAR(100) NULL,
        OrderDate      DATETIME DEFAULT GETDATE(),
        ExpectedDate   DATETIME NULL,
        CreatedAt      DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ PurchaseOrders table created.';
END
GO

-- 14. Purchase Order Items
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrderItems')
BEGIN
    CREATE TABLE PurchaseOrderItems (
        POItemID        INT IDENTITY(1,1) PRIMARY KEY,
        POID            INT FOREIGN KEY REFERENCES PurchaseOrders(POID) ON DELETE CASCADE,
        ProductID       INT FOREIGN KEY REFERENCES Products(ProductID),
        ItemDescription NVARCHAR(200) NOT NULL,
        Unit            NVARCHAR(20) DEFAULT 'pcs',
        Quantity        INT NOT NULL,
        UnitPrice       DECIMAL(10,2) NOT NULL,
        TotalPrice      DECIMAL(10,2) NOT NULL
    );
    PRINT '✅ PurchaseOrderItems table created.';
END
GO

-- 15. Stock Transactions & Adjustments
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransactions')
BEGIN
    CREATE TABLE StockTransactions (
        TransactionID   INT IDENTITY(1,1) PRIMARY KEY,
        ProductID       INT FOREIGN KEY REFERENCES Products(ProductID),
        TransactionType NVARCHAR(50) NOT NULL,
        Quantity        INT NOT NULL,
        ReferenceNumber NVARCHAR(50) NULL,
        Reason          NVARCHAR(200) NULL,
        Notes           NVARCHAR(500) NULL,
        ProcessedBy     NVARCHAR(100) NULL,
        TransactionDate DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ StockTransactions table created.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockAdjustments')
BEGIN
    CREATE TABLE StockAdjustments (
        AdjustmentID   INT IDENTITY(1,1) PRIMARY KEY,
        ProductID      INT FOREIGN KEY REFERENCES Products(ProductID),
        AdjustmentType NVARCHAR(20) NOT NULL,
        Quantity       INT NOT NULL,
        Remarks        NVARCHAR(255) NULL,
        AdjustedBy     INT FOREIGN KEY REFERENCES Users(UserID),
        CreatedAt      DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ StockAdjustments table created.';
END
GO

-- 16. Audit Logs
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE AuditLogs (
        LogID      INT IDENTITY(1,1) PRIMARY KEY,
        UserID     INT NULL FOREIGN KEY REFERENCES Users(UserID),
        Username   NVARCHAR(50) NOT NULL,
        Role       NVARCHAR(50) NOT NULL,
        ActionType NVARCHAR(50) NOT NULL,
        Details    NVARCHAR(500) NOT NULL,
        Timestamp  DATETIME DEFAULT GETDATE()
    );
    PRINT '✅ AuditLogs table created.';
END
GO

-- ============================================================================
-- SEED INITIAL DEFAULT SYSTEM DATA
-- ============================================================================

-- Seed Default Login Accounts (Admin, Cashier, StoreClerk)
IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (FullName, Username, Password, Role, IsActive)
    VALUES ('System Administrator', 'admin', 'admin123', 'Admin', 1);
END

IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'cashier')
BEGIN
    INSERT INTO Users (FullName, Username, Password, Role, IsActive)
    VALUES ('Ruby Dela Cruz', 'cashier', 'cashier123', 'Cashier', 1);
END

IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'clerk')
BEGIN
    INSERT INTO Users (FullName, Username, Password, Role, IsActive)
    VALUES ('Mark Reyes', 'clerk', 'clerk123', 'StoreClerk', 1);
END
PRINT '✅ Default login accounts initialized.';

-- Seed Mechanics
IF NOT EXISTS (SELECT * FROM Mechanics)
BEGIN
    INSERT INTO Mechanics (FullName, ContactNumber, Specialization, CommissionRate, IsActive)
    VALUES 
        ('Bert Magsino', '0917-555-8811', 'Engine Overhaul & Performance Tuning', 80.00, 1),
        ('James Nacionales', '0922-444-9922', 'CVT Upgrades, FI Systems & Electricals', 70.00, 1);
    PRINT '✅ Default mechanics initialized.';
END

-- Seed Categories
IF NOT EXISTS (SELECT * FROM Categories)
BEGIN
    INSERT INTO Categories (CategoryName, IsActive)
    VALUES 
        ('Engine Oils & Lubricants', 1),
        ('Tires, Tubes & Mags', 1),
        ('Brakes, Discs & Cables', 1),
        ('Transmission, CVT & Belts', 1),
        ('Engine Parts & Cylinders', 1),
        ('Electrical, FI & Batteries', 1);
    PRINT '✅ Default categories initialized.';
END

-- Seed Brands
IF NOT EXISTS (SELECT * FROM Brands)
BEGIN
    INSERT INTO Brands (BrandName, IsActive)
    VALUES 
        ('Motul', 1),
        ('Yamalube', 1),
        ('Brembo', 1),
        ('Maxxis', 1),
        ('NGK', 1);
    PRINT '✅ Default brands initialized.';
END

-- Seed Services
IF NOT EXISTS (SELECT * FROM Services)
BEGIN
    INSERT INTO Services (ServiceName, LaborFee, IsActive)
    VALUES 
        ('Oil Change & Filter Replacement', 100.00, 1),
        ('CVT Cleaning, Degreasing & Tuning', 250.00, 1),
        ('Brake Service & Pad Replacement', 150.00, 1),
        ('Full Fi Cleaning & Calibration', 450.00, 1),
        ('Tire Replacement / Vulcanizing', 120.00, 1);
    PRINT '✅ Default services initialized.';
END

PRINT '🎉 DATABASE SETUP COMPLETE! You can now log into the Bike Kings app.';
GO