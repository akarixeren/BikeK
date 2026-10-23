-- =========================================================================
-- BIKE KINGS CYCLE & MOTOR REPAIR - MASTER DATABASE & USER SETUP SCRIPT
-- =========================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'BikeKingsDB')
BEGIN
    CREATE DATABASE BikeKingsDB;
END
GO

USE BikeKingsDB;
GO

-- 1. USERS & AUTHENTICATION TABLE
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
END
GO

-- 2. SEED DEFAULT USER ACCOUNTS (Admin, Cashier, Inventory Manager)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (FullName, Username, Password, Role, IsActive)
    VALUES ('System Administrator', 'admin', 'admin123', 'Admin', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'cashier')
BEGIN
    INSERT INTO Users (FullName, Username, Password, Role, IsActive)
    VALUES ('Ruby Dela Cruz', 'cashier', 'cashier123', 'Cashier', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'inventory')
BEGIN
    INSERT INTO Users (FullName, Username, Password, Role, IsActive)
    VALUES ('Mark Reyes', 'inventory', 'inventory123', 'StoreClerk', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'clerk')
BEGIN
    INSERT INTO Users (FullName, Username, Password, Role, IsActive)
    VALUES ('Mark Reyes', 'clerk', 'clerk123', 'StoreClerk', 1);
END
GO

-- 3. MECHANICS TABLE
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Mechanics')
BEGIN
    CREATE TABLE Mechanics (
        MechanicID    INT IDENTITY(1,1) PRIMARY KEY,
        FullName      NVARCHAR(100) NOT NULL,
        ContactNumber NVARCHAR(50) NULL,
        Specialization NVARCHAR(100) NULL,
        IsActive      BIT NOT NULL DEFAULT 1
    );

    INSERT INTO Mechanics (FullName, ContactNumber, Specialization, IsActive) VALUES 
    ('Bert Magsino', '0917-555-8811', 'Engine Overhaul & Performance Tuning', 1),
    ('James Nacionales', '0922-444-9922', 'CVT Upgrades, FI Systems & Electricals', 1);
END
GO

-- 4. CATEGORIES TABLE
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE Categories (
        CategoryID   INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL UNIQUE,
        IsActive     BIT NOT NULL DEFAULT 1,
        CreatedAt    DATETIME DEFAULT GETDATE()
    );

    INSERT INTO Categories (CategoryName, IsActive) VALUES 
    ('Engine Oils & Lubricants', 1),
    ('Tires, Tubes & Mags', 1),
    ('Brakes, Discs & Cables', 1),
    ('Transmission, CVT & Belts', 1),
    ('Engine Parts, Cylinders & Pistons', 1),
    ('Electrical, FI & Batteries', 1),
    ('Suspension, Shocks & Forks', 1),
    ('Performance Parts & Upgrades', 1);
END
GO

-- 5. BRANDS TABLE
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Brands')
BEGIN
    CREATE TABLE Brands (
        BrandID   INT IDENTITY(1,1) PRIMARY KEY,
        BrandName NVARCHAR(100) NOT NULL UNIQUE,
        IsActive  BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE()
    );

    INSERT INTO Brands (BrandName, IsActive) VALUES 
    ('Yamaha Genuine Parts (YGP)', 1),
    ('Honda Genuine Parts (HGP)', 1),
    ('Motul', 1),
    ('Castrol', 1),
    ('Brembo', 1),
    ('IRC Tire', 1);
END
GO

-- 6. SUPPLIERS TABLE
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

    INSERT INTO Suppliers (SupplierName, ContactPerson, ContactNumber, Phone, Email, Address, IsActive) VALUES 
    ('Motul Philippines Official', 'Carlos Mendoza', '0917-888-1122', '0917-888-1122', 'sales@motul.ph', 'Quezon City', 1),
    ('PowerDrive Parts Distributor', 'Dennis Tan', '0918-222-3344', '0918-222-3344', 'orders@powerdrive.com', 'Caloocan City', 1),
    ('Apex Cycle Supply Co.', 'Elena Ramos', '0922-333-5566', '0922-333-5566', 'elena@apexcycle.ph', 'Manila', 1);
END
GO

-- 7. REPAIR SERVICES TABLE
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Services')
BEGIN
    CREATE TABLE Services (
        ServiceID   INT IDENTITY(1,1) PRIMARY KEY,
        ServiceName NVARCHAR(150) NOT NULL UNIQUE,
        LaborFee    DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        IsActive    BIT NOT NULL DEFAULT 1,
        CreatedAt   DATETIME DEFAULT GETDATE()
    );

    INSERT INTO Services (ServiceName, LaborFee, IsActive) VALUES
    ('Basic PMS & Oil Change Labor', 200.00, 1),
    ('CVT Cleaning, Degreasing & Tuning', 350.00, 1),
    ('Front / Rear Brake Pad Replacement', 150.00, 1),
    ('Engine Top Overhaul & Valve Lapping', 1200.00, 1);
END
GO

-- 8. PRODUCTS TABLE
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
        CriticalLimit  INT NOT NULL DEFAULT 5,
        IsActive       BIT NOT NULL DEFAULT 1,
        CreatedAt      DATETIME DEFAULT GETDATE()
    );
END
GO

-- 9. SALES & SALES DETAILS TABLES
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sales')
BEGIN
    CREATE TABLE Sales (
        SaleID        INT IDENTITY(1,1) PRIMARY KEY,
        InvoiceNo     NVARCHAR(50) NOT NULL UNIQUE,
        UserID        INT FOREIGN KEY REFERENCES Users(UserID),
        CustomerName  NVARCHAR(100) DEFAULT 'Walk-in Customer',
        PaymentMethod NVARCHAR(50) DEFAULT 'Cash',
        SubTotal      DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        Discount      DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        TotalAmount   DECIMAL(10,2) NOT NULL,
        AmountPaid    DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        ChangeAmount  DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        SaleDate      DATETIME DEFAULT GETDATE(),
        Status        NVARCHAR(30) NOT NULL DEFAULT 'Completed'
    );
END
GO

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
END
GO

-- 10. JOB ORDERS & ITEMS TABLES
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobOrders')
BEGIN
    CREATE TABLE JobOrders (
        JobOrderID     INT IDENTITY(1,1) PRIMARY KEY,
        JobOrderNo     NVARCHAR(50) NOT NULL UNIQUE,
        CustomerName   NVARCHAR(100) NOT NULL,
        ContactNumber  NVARCHAR(50) NULL,
        MotorcycleUnit NVARCHAR(100) NULL,
        PlateNumber    NVARCHAR(50) NULL,
        MechanicID     INT NULL FOREIGN KEY REFERENCES Mechanics(MechanicID),
        CashierID      INT FOREIGN KEY REFERENCES Users(UserID),
        TotalLabor     DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        TotalParts     DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        TotalAmount    DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        PaymentMethod  NVARCHAR(50) DEFAULT 'Cash',
        Status         NVARCHAR(30) NOT NULL DEFAULT 'Completed',
        CreatedAt      DATETIME DEFAULT GETDATE(),
        CompletedAt    DATETIME NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobOrderItems')
BEGIN
    CREATE TABLE JobOrderItems (
        JobOrderItemID INT IDENTITY(1,1) PRIMARY KEY,
        JobOrderID     INT FOREIGN KEY REFERENCES JobOrders(JobOrderID) ON DELETE CASCADE,
        ItemType       NVARCHAR(20) NOT NULL,
        ServiceID      INT NULL FOREIGN KEY REFERENCES Services(ServiceID),
        ProductID      INT NULL FOREIGN KEY REFERENCES Products(ProductID),
        ItemName       NVARCHAR(150) NOT NULL,
        Quantity       INT NOT NULL DEFAULT 1,
        UnitPrice      DECIMAL(10,2) NOT NULL DEFAULT 0.00,
        Subtotal       DECIMAL(10,2) NOT NULL DEFAULT 0.00
    );
END
GO

-- 11. AUDIT LOGS TABLE
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
END
GO﻿
