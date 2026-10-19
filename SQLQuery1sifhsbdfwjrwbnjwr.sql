-- ============================================================================
-- BIKE KINGS CYCLE & MOTOR REPAIR - COMPREHENSIVE SAMPLE DATA
-- Date Range: August 21, 2026 to October 19, 2026 (Today)
-- Volume: 15-28 Customers/Day (~8-14 Retail Sales + ~7-14 Job Orders)
-- Weekly Sales Volume: ₱10,000 - ₱19,500 (Matches target ₱5,000 - ₱20,000)
-- Strict Invoice Format: INV-YYYYMMDD-0001 & JO-YYYYMMDD-0001
-- Authentic Filipino Customers (No Celebrities)
-- Fully Connected: Suppliers -> Products -> POs -> Sales/JOs -> Stock Logs -> Audit Logs (History Logs)
-- ============================================================================

USE BikeKingsDB;
GO

BEGIN TRANSACTION;

-- 🛡️ 0. Ensure optional columns exist if not already present
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'CriticalLimit')
    ALTER TABLE Products ADD CriticalLimit INT NOT NULL DEFAULT 2;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Mechanics') AND name = 'CommissionRate')
    ALTER TABLE Mechanics ADD CommissionRate DECIMAL(5,2) NOT NULL DEFAULT 70.00;

SET NOCOUNT ON;

PRINT '====================================================================';
PRINT 'Starting sample data generation (Aug 21, 2026 - Oct 19, 2026)...';
PRINT '====================================================================';

-- 🛡️ 1. Temporarily disable foreign keys for clean setup
EXEC sp_MSforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT all";

-- 🧹 2. Clear old transactions to prevent duplicates
IF OBJECT_ID('SalesDetails', 'U') IS NOT NULL DELETE FROM SalesDetails;
IF OBJECT_ID('JobOrderItems', 'U') IS NOT NULL DELETE FROM JobOrderItems;
IF OBJECT_ID('PurchaseOrderItems', 'U') IS NOT NULL DELETE FROM PurchaseOrderItems;
IF OBJECT_ID('StockTransactions', 'U') IS NOT NULL DELETE FROM StockTransactions;
IF OBJECT_ID('StockAdjustments', 'U') IS NOT NULL DELETE FROM StockAdjustments;
IF OBJECT_ID('Sales', 'U') IS NOT NULL DELETE FROM Sales;
IF OBJECT_ID('JobOrders', 'U') IS NOT NULL DELETE FROM JobOrders;
IF OBJECT_ID('PurchaseOrders', 'U') IS NOT NULL DELETE FROM PurchaseOrders;
IF OBJECT_ID('AuditLogs', 'U') IS NOT NULL DELETE FROM AuditLogs;

-- Reseed identities
IF OBJECT_ID('SalesDetails', 'U') IS NOT NULL DBCC CHECKIDENT ('SalesDetails', RESEED, 0);
IF OBJECT_ID('JobOrderItems', 'U') IS NOT NULL DBCC CHECKIDENT ('JobOrderItems', RESEED, 0);
IF OBJECT_ID('Sales', 'U') IS NOT NULL DBCC CHECKIDENT ('Sales', RESEED, 0);
IF OBJECT_ID('JobOrders', 'U') IS NOT NULL DBCC CHECKIDENT ('JobOrders', RESEED, 0);
IF OBJECT_ID('PurchaseOrders', 'U') IS NOT NULL DBCC CHECKIDENT ('PurchaseOrders', RESEED, 0);
IF OBJECT_ID('PurchaseOrderItems', 'U') IS NOT NULL DBCC CHECKIDENT ('PurchaseOrderItems', RESEED, 0);
IF OBJECT_ID('StockTransactions', 'U') IS NOT NULL DBCC CHECKIDENT ('StockTransactions', RESEED, 0);
IF OBJECT_ID('StockAdjustments', 'U') IS NOT NULL DBCC CHECKIDENT ('StockAdjustments', RESEED, 0);
IF OBJECT_ID('AuditLogs', 'U') IS NOT NULL DBCC CHECKIDENT ('AuditLogs', RESEED, 0);

PRINT '✔️ Transactional tables cleared and identities reset.';

-- 🏢 3. ENSURE SYSTEM USERS (Admin, Cashier, Inventory Manager)
IF NOT EXISTS (SELECT * FROM Users WHERE UserID = 1)
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (UserID, FullName, Username, Password, Role, IsActive)
    VALUES (1, 'Rich Roque', 'admin', 'admin123', 'Admin', 1);
    SET IDENTITY_INSERT Users OFF;
END
ELSE
BEGIN
    UPDATE Users SET FullName = 'Rich Roque', Role = 'Admin' WHERE UserID = 1;
END

IF NOT EXISTS (SELECT * FROM Users WHERE UserID = 2)
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (UserID, FullName, Username, Password, Role, IsActive)
    VALUES (2, 'Ruby Dela Cruz', 'cashier', 'cashier123', 'Cashier', 1);
    SET IDENTITY_INSERT Users OFF;
END
ELSE
BEGIN
    UPDATE Users SET FullName = 'Ruby Dela Cruz', Role = 'Cashier' WHERE UserID = 2;
END

IF NOT EXISTS (SELECT * FROM Users WHERE UserID = 3)
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (UserID, FullName, Username, Password, Role, IsActive)
    VALUES (3, 'Mark Reyes', 'clerk', 'clerk123', 'Inventory Manager', 1);
    SET IDENTITY_INSERT Users OFF;
END
ELSE
BEGIN
    UPDATE Users SET FullName = 'Mark Reyes', Role = 'Inventory Manager' WHERE UserID = 3;
END

-- 👨‍🔧 4. ENSURE MECHANICS (Standalone Technicians)
IF NOT EXISTS (SELECT * FROM Mechanics WHERE MechanicID = 1)
BEGIN
    SET IDENTITY_INSERT Mechanics ON;
    INSERT INTO Mechanics (MechanicID, FullName, ContactNumber, Specialization, CommissionRate, IsActive)
    VALUES (1, 'Bert Magsino', '0917-555-8811', 'Engine Overhaul & Performance Tuning', 80.00, 1);
    SET IDENTITY_INSERT Mechanics OFF;
END

IF NOT EXISTS (SELECT * FROM Mechanics WHERE MechanicID = 2)
BEGIN
    SET IDENTITY_INSERT Mechanics ON;
    INSERT INTO Mechanics (MechanicID, FullName, ContactNumber, Specialization, CommissionRate, IsActive)
    VALUES (2, 'James Nacionales', '0922-444-9922', 'CVT Upgrades, FI Systems & Electricals', 70.00, 1);
    SET IDENTITY_INSERT Mechanics OFF;
END

-- 🏷️ 5. ENSURE CATEGORIES
DECLARE @CatMap TABLE (ID INT, Name NVARCHAR(100));
INSERT INTO @CatMap (ID, Name) VALUES 
(1, 'Engine Oils & Lubricants'),
(2, 'Tires, Tubes & Mags'),
(3, 'Brakes, Discs & Cables'),
(4, 'Transmission, CVT & Belts'),
(5, 'Engine & Ignition Parts'),
(6, 'Maintenance & Cleaning Chemicals');

DECLARE @cId INT, @cName NVARCHAR(100);
DECLARE cat_cursor CURSOR FOR SELECT ID, Name FROM @CatMap;
OPEN cat_cursor;
FETCH NEXT FROM cat_cursor INTO @cId, @cName;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT * FROM Categories WHERE CategoryID = @cId)
    BEGIN
        SET IDENTITY_INSERT Categories ON;
        INSERT INTO Categories (CategoryID, CategoryName, IsActive) VALUES (@cId, @cName, 1);
        SET IDENTITY_INSERT Categories OFF;
    END
    FETCH NEXT FROM cat_cursor INTO @cId, @cName;
END;
CLOSE cat_cursor;
DEALLOCATE cat_cursor;

-- 🛡️ 6. ENSURE BRANDS
DECLARE @BrandMap TABLE (ID INT, Name NVARCHAR(100));
INSERT INTO @BrandMap (ID, Name) VALUES 
(1, 'Motul'),
(2, 'Yamalube'),
(3, 'Brembo'),
(4, 'Bando'),
(5, 'NGK'),
(6, 'Maxxis'),
(7, 'Quick'),
(8, 'Bendix');

DECLARE @bId INT, @bName NVARCHAR(100);
DECLARE br_cursor CURSOR FOR SELECT ID, Name FROM @BrandMap;
OPEN br_cursor;
FETCH NEXT FROM br_cursor INTO @bId, @bName;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT * FROM Brands WHERE BrandID = @bId)
    BEGIN
        SET IDENTITY_INSERT Brands ON;
        INSERT INTO Brands (BrandID, BrandName, IsActive) VALUES (@bId, @bName, 1);
        SET IDENTITY_INSERT Brands OFF;
    END
    FETCH NEXT FROM br_cursor INTO @bId, @bName;
END;
CLOSE br_cursor;
DEALLOCATE br_cursor;

-- 🚚 7. ENSURE SUPPLIERS
DECLARE @SuppMap TABLE (ID INT, Name NVARCHAR(150), Contact NVARCHAR(100), Phone NVARCHAR(50), Email NVARCHAR(100), Addr NVARCHAR(200));
INSERT INTO @SuppMap VALUES
(1, 'Motul Philippines', 'Aris Reyes', '0922-987-6543', 'orders@motul.com.ph', 'Quezon City'),
(2, 'Yamaha Motor Philippines', 'Dave Lim', '0918-555-8888', 'parts@yamaha-motor.ph', 'Caloocan City'),
(3, 'Brembo Racing PH', 'Carlo Cruz', '0905-333-2211', 'sales@bremboph.com', 'Manila'),
(4, 'Everest Auto & Motorcycle Supply', 'Marc Santos', '0917-123-4567', 'sales@everestmotor.ph', 'Manila'),
(5, 'PowerDrive Wholesale Distribution', 'John Gomez', '0920-444-1122', 'contact@powerdrive.ph', 'Pasig City');

DECLARE @supId INT, @supName NVARCHAR(150), @supCont NVARCHAR(100), @supPh NVARCHAR(50), @supEm NVARCHAR(100), @supAd NVARCHAR(200);
DECLARE sup_cursor CURSOR FOR SELECT ID, Name, Contact, Phone, Email, Addr FROM @SuppMap;
OPEN sup_cursor;
FETCH NEXT FROM sup_cursor INTO @supId, @supName, @supCont, @supPh, @supEm, @supAd;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT * FROM Suppliers WHERE SupplierID = @supId)
    BEGIN
        SET IDENTITY_INSERT Suppliers ON;
        INSERT INTO Suppliers (SupplierID, SupplierName, ContactPerson, ContactNumber, Phone, Email, Address, IsActive)
        VALUES (@supId, @supName, @supCont, @supPh, @supPh, @supEm, @supAd, 1);
        SET IDENTITY_INSERT Suppliers OFF;
        
        -- Connect to History Log
        INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp)
        VALUES (1, 'admin', 'Admin', 'SUPPLIERS', 'Registered new authorized supplier: ' + @supName + ' (' + @supCont + ')', '2026-08-15 09:30:00');
    END
    FETCH NEXT FROM sup_cursor INTO @supId, @supName, @supCont, @supPh, @supEm, @supAd;
END;
CLOSE sup_cursor;
DEALLOCATE sup_cursor;

-- 📦 8. ENSURE PRODUCTS (Directly linked to Category, Brand & Supplier)
DECLARE @ProdMap TABLE (
    ID INT, Barcode NVARCHAR(50), Name NVARCHAR(150), CatID INT, BrandID INT, SuppID INT,
    Cost DECIMAL(10,2), Sell DECIMAL(10,2), Stock INT, Reorder INT, Crit INT
);
INSERT INTO @ProdMap VALUES
(1,  '48000101', 'Motul Scooter Expert LE 10W40 4T (800ml)', 1, 1, 1, 180.00, 240.00, 110, 15, 5),
(2,  '48000102', 'Motul Scooter Gear Oil 80W90 (120ml)',      1, 1, 1,  60.00,  95.00,  95, 12, 4),
(3,  '48000103', 'Motul Brake & Parts Clean Spray (400ml)',   6, 1, 1, 150.00, 220.00,  70, 10, 3),
(4,  '48000104', 'Motul Chain Lube Road C2 Spray (400ml)',    6, 1, 1, 190.00, 280.00,  55,  8, 3),
(5,  '48000201', 'Yamalube AT 10W-40 4T (800ml)',            1, 2, 2, 150.00, 200.00, 120, 20, 6),
(6,  '48000202', 'Yamalube Scooter Gear Oil (100ml)',        1, 2, 2,  45.00,  75.00, 105, 15, 5),
(7,  '48000203', 'Yamalube Coolant Pre-mixed (1L)',           6, 2, 2,  95.00, 150.00,  60, 10, 3),
(8,  '48000204', 'Yamalube DOT 4 Brake Fluid (100ml)',        3, 2, 2,  55.00,  90.00,  75, 10, 3),
(9,  '48000205', 'Yamaha Aerox 155 Air Filter',              5, 2, 2, 260.00, 420.00,  45,  8, 2),
(10, '48000206', 'Yamaha Genuine V-Belt (NMAX V1/V2)',        4, 2, 2, 360.00, 520.00,  40,  8, 2),
(11, '48000301', 'Brembo Ceramic Front Brake Pads (NMAX/Aerox)', 3, 3, 3, 260.00, 390.00, 65, 10, 3),
(12, '48000302', 'Brembo Premium Brake Fluid DOT 4 (250ml)',  3, 3, 3, 140.00, 220.00,  50,  8, 2),
(13, '48000401', 'Bando Power Drive Belt (Honda Click 125/150)', 4, 4, 5, 250.00, 380.00, 50, 10, 3),
(14, '48000402', 'Bando Flyball Set 13g (Aerox/NMAX)',        4, 4, 5, 130.00, 200.00,  65, 10, 3),
(15, '48000501', 'NGK Standard Spark Plug CPR8EA-9',         5, 5, 4,  80.00, 120.00, 130, 20, 6),
(16, '48000502', 'NGK G-Power Platinum Spark Plug CPR8EAGP-9',5, 5, 4, 110.00, 160.00,  90, 15, 4),
(17, '48000701', 'Quick Motorcycle Inner Tube 14" (2.50/2.75-14)', 2, 7, 4, 75.00, 120.00, 85, 12, 4),
(18, '48000601', 'Maxxis Victra S98 ST 110/70-13 Tubeless',   2, 6, 4, 1050.00, 1450.00, 30,  5, 2),
(19, '48000801', 'Bendix Motorcycle Front Brake Pads (Click/Beat)', 3, 8, 4, 140.00, 220.00, 60, 10, 3),
(20, '48000802', 'Bendix Motorcycle Rear Brake Shoes (Mio/Click)',  3, 8, 4, 120.00, 180.00, 55,  8, 2);

DECLARE @pId INT, @pBc NVARCHAR(50), @pNm NVARCHAR(150), @pCat INT, @pBr INT, @pSup INT, @pCo DECIMAL(10,2), @pSe DECIMAL(10,2), @pSt INT, @pRe INT, @pCr INT;
DECLARE prod_cursor CURSOR FOR SELECT ID, Barcode, Name, CatID, BrandID, SuppID, Cost, Sell, Stock, Reorder, Crit FROM @ProdMap;
OPEN prod_cursor;
FETCH NEXT FROM prod_cursor INTO @pId, @pBc, @pNm, @pCat, @pBr, @pSup, @pCo, @pSe, @pSt, @pRe, @pCr;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT * FROM Products WHERE ProductID = @pId)
    BEGIN
        SET IDENTITY_INSERT Products ON;
        INSERT INTO Products (ProductID, Barcode, ProductName, CategoryID, BrandID, SupplierID, CostPrice, SellingPrice, StockQuantity, ReorderLevel, CriticalLimit, IsActive)
        VALUES (@pId, @pBc, @pNm, @pCat, @pBr, @pSup, @pCo, @pSe, @pSt, @pRe, @pCr, 1);
        SET IDENTITY_INSERT Products OFF;
    END
    ELSE
    BEGIN
        UPDATE Products 
        SET Barcode = @pBc, ProductName = @pNm, CategoryID = @pCat, BrandID = @pBr, SupplierID = @pSup,
            CostPrice = @pCo, SellingPrice = @pSe, StockQuantity = @pSt, ReorderLevel = @pRe, CriticalLimit = @pCr
        WHERE ProductID = @pId;
    END
    FETCH NEXT FROM prod_cursor INTO @pId, @pBc, @pNm, @pCat, @pBr, @pSup, @pCo, @pSe, @pSt, @pRe, @pCr;
END;
CLOSE prod_cursor;
DEALLOCATE prod_cursor;

-- 🔧 9. ENSURE REPAIR SERVICES & LABOR FEES
DECLARE @SvcMap TABLE (ID INT, Name NVARCHAR(150), Fee DECIMAL(10,2));
INSERT INTO @SvcMap VALUES
(1, 'Oil Change & Minor Inspection', 70.00),
(2, 'Gear Oil Service & Inspection', 50.00),
(3, 'Brake Service & Pad Replacement', 120.00),
(4, 'CVT Cleaning, Degreasing & Tuning', 200.00),
(5, 'Tire Replacement / Vulcanizing', 80.00),
(6, 'Spark Plug Check & Replacement', 50.00),
(7, 'Full Fi Cleaning & Calibration', 350.00);

DECLARE @svId INT, @svNm NVARCHAR(150), @svFee DECIMAL(10,2);
DECLARE svc_cursor CURSOR FOR SELECT ID, Name, Fee FROM @SvcMap;
OPEN svc_cursor;
FETCH NEXT FROM svc_cursor INTO @svId, @svNm, @svFee;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT * FROM Services WHERE ServiceID = @svId)
    BEGIN
        SET IDENTITY_INSERT Services ON;
        INSERT INTO Services (ServiceID, ServiceName, LaborFee, IsActive) VALUES (@svId, @svNm, @svFee, 1);
        SET IDENTITY_INSERT Services OFF;
    END
    FETCH NEXT FROM svc_cursor INTO @svId, @svNm, @svFee;
END;
CLOSE svc_cursor;
DEALLOCATE svc_cursor;

PRINT '✔️ Master tables (Users, Mechanics, Categories, Brands, Suppliers, Products, Services) connected.';

-- 📋 10. REALISTIC PURCHASE ORDERS & HISTORY LOGS
-- PO 1: Motul Philippines (Aug 18, 2026)
INSERT INTO PurchaseOrders (PONumber, SupplierID, RequesterName, TotalAmount, Status, Remarks, Terms, ShippedVia, OrderDate, ExpectedDate)
VALUES ('PO-20260818-4001', 1, 'Mark Reyes', 27600.00, 'Received', 'Initial stock-in of Motul engine & gear oils and cleaner sprays', 'Net 30', 'Motul Logistics Van', '2026-08-18 09:00:00', '2026-08-20 14:00:00');

INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice) VALUES 
((SELECT MAX(POID) FROM PurchaseOrders), 1, 'Motul Scooter Expert LE 10W40 4T (800ml)', 'pcs', 60, 180.00, 10800.00),
((SELECT MAX(POID) FROM PurchaseOrders), 2, 'Motul Scooter Gear Oil 80W90 (120ml)', 'pcs', 60, 60.00, 3600.00),
((SELECT MAX(POID) FROM PurchaseOrders), 3, 'Motul Brake & Parts Clean Spray (400ml)', 'pcs', 40, 150.00, 6000.00),
((SELECT MAX(POID) FROM PurchaseOrders), 4, 'Motul Chain Lube Road C2 Spray (400ml)', 'pcs', 38, 190.00, 7200.00);

INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate) VALUES
(1, 'Stock-In', 60, 'PO-20260818-4001', 'PO Delivery', 'Received from Motul Philippines', 'Mark Reyes', '2026-08-20 14:00:00'),
(2, 'Stock-In', 60, 'PO-20260818-4001', 'PO Delivery', 'Received from Motul Philippines', 'Mark Reyes', '2026-08-20 14:00:00'),
(3, 'Stock-In', 40, 'PO-20260818-4001', 'PO Delivery', 'Received from Motul Philippines', 'Mark Reyes', '2026-08-20 14:00:00'),
(4, 'Stock-In', 38, 'PO-20260818-4001', 'PO Delivery', 'Received from Motul Philippines', 'Mark Reyes', '2026-08-20 14:00:00');

INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) VALUES
(3, 'clerk', 'Inventory Manager', 'PURCHASE_ORDER', 'Created Purchase Order PO-20260818-4001 for Motul Philippines (Total: ₱27,600.00)', '2026-08-18 09:00:00'),
(3, 'clerk', 'Inventory Manager', 'STOCK_IN', 'Verified delivery and received items for PO-20260818-4001 into inventory.', '2026-08-20 14:00:00');

-- PO 2: Yamaha Motor Philippines (Aug 20, 2026)
INSERT INTO PurchaseOrders (PONumber, SupplierID, RequesterName, TotalAmount, Status, Remarks, Terms, ShippedVia, OrderDate, ExpectedDate)
VALUES ('PO-20260820-4002', 2, 'Mark Reyes', 31650.00, 'Received', 'Yamalube lubricants, genuine belts, coolant and brake fluid', 'Net 30', 'Yamaha Express Fleet', '2026-08-20 10:30:00', '2026-08-22 15:00:00');

INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice) VALUES 
((SELECT MAX(POID) FROM PurchaseOrders), 5, 'Yamalube AT 10W-40 4T (800ml)', 'pcs', 70, 150.00, 10500.00),
((SELECT MAX(POID) FROM PurchaseOrders), 6, 'Yamalube Scooter Gear Oil (100ml)', 'pcs', 70, 45.00, 3150.00),
((SELECT MAX(POID) FROM PurchaseOrders), 7, 'Yamalube Coolant Pre-mixed (1L)', 'pcs', 40, 95.00, 3800.00),
((SELECT MAX(POID) FROM PurchaseOrders), 8, 'Yamalube DOT 4 Brake Fluid (100ml)', 'pcs', 40, 55.00, 2200.00),
((SELECT MAX(POID) FROM PurchaseOrders), 10, 'Yamaha Genuine V-Belt (NMAX V1/V2)', 'pcs', 30, 360.00, 10800.00);

INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate) VALUES
(5, 'Stock-In', 70, 'PO-20260820-4002', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-08-22 15:00:00'),
(6, 'Stock-In', 70, 'PO-20260820-4002', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-08-22 15:00:00'),
(7, 'Stock-In', 40, 'PO-20260820-4002', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-08-22 15:00:00'),
(8, 'Stock-In', 40, 'PO-20260820-4002', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-08-22 15:00:00'),
(10, 'Stock-In', 30, 'PO-20260820-4002', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-08-22 15:00:00');

INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) VALUES
(3, 'clerk', 'Inventory Manager', 'PURCHASE_ORDER', 'Created Purchase Order PO-20260820-4002 for Yamaha Motor Philippines (Total: ₱31,650.00)', '2026-08-20 10:30:00'),
(3, 'clerk', 'Inventory Manager', 'STOCK_IN', 'Verified delivery and received items for PO-20260820-4002 into inventory.', '2026-08-22 15:00:00');

-- PO 3: Everest Auto Supply (Aug 20, 2026)
INSERT INTO PurchaseOrders (PONumber, SupplierID, RequesterName, TotalAmount, Status, Remarks, Terms, ShippedVia, OrderDate, ExpectedDate)
VALUES ('PO-20260820-4003', 4, 'Mark Reyes', 31950.00, 'Received', 'NGK Spark plugs, Quick tubes, Bendix pads & Maxxis tires', 'COD', 'Everest Truck Delivery', '2026-08-20 11:15:00', '2026-08-22 16:30:00');

INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice) VALUES 
((SELECT MAX(POID) FROM PurchaseOrders), 15, 'NGK Standard Spark Plug CPR8EA-9', 'pcs', 80, 80.00, 6400.00),
((SELECT MAX(POID) FROM PurchaseOrders), 16, 'NGK G-Power Platinum Spark Plug CPR8EAGP-9', 'pcs', 50, 110.00, 5500.00),
((SELECT MAX(POID) FROM PurchaseOrders), 17, 'Quick Motorcycle Inner Tube 14"', 'pcs', 60, 75.00, 4500.00),
((SELECT MAX(POID) FROM PurchaseOrders), 18, 'Maxxis Victra S98 ST 110/70-13 Tubeless', 'pcs', 10, 1050.00, 10500.00),
((SELECT MAX(POID) FROM PurchaseOrders), 19, 'Bendix Motorcycle Front Brake Pads', 'pcs', 36, 140.00, 5040.00);

INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate) VALUES
(15, 'Stock-In', 80, 'PO-20260820-4003', 'PO Delivery', 'Received from Everest Auto Supply', 'Mark Reyes', '2026-08-22 16:30:00'),
(16, 'Stock-In', 50, 'PO-20260820-4003', 'PO Delivery', 'Received from Everest Auto Supply', 'Mark Reyes', '2026-08-22 16:30:00'),
(17, 'Stock-In', 60, 'PO-20260820-4003', 'PO Delivery', 'Received from Everest Auto Supply', 'Mark Reyes', '2026-08-22 16:30:00'),
(18, 'Stock-In', 10, 'PO-20260820-4003', 'PO Delivery', 'Received from Everest Auto Supply', 'Mark Reyes', '2026-08-22 16:30:00'),
(19, 'Stock-In', 36, 'PO-20260820-4003', 'PO Delivery', 'Received from Everest Auto Supply', 'Mark Reyes', '2026-08-22 16:30:00');

INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) VALUES
(3, 'clerk', 'Inventory Manager', 'PURCHASE_ORDER', 'Created Purchase Order PO-20260820-4003 for Everest Auto & Motorcycle Supply (Total: ₱31,950.00)', '2026-08-20 11:15:00'),
(3, 'clerk', 'Inventory Manager', 'STOCK_IN', 'Verified delivery and received items for PO-20260820-4003 into inventory.', '2026-08-22 16:30:00');

-- PO 4: Brembo Racing PH (Sep 05, 2026)
INSERT INTO PurchaseOrders (PONumber, SupplierID, RequesterName, TotalAmount, Status, Remarks, Terms, ShippedVia, OrderDate, ExpectedDate)
VALUES ('PO-20260905-4004', 3, 'Mark Reyes', 17400.00, 'Received', 'Brembo Ceramic Front Brake Pads & DOT4 fluids', 'Net 15', 'SpeedTech Logistics', '2026-09-05 13:00:00', '2026-09-07 14:00:00');

INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice) VALUES 
((SELECT MAX(POID) FROM PurchaseOrders), 11, 'Brembo Ceramic Front Brake Pads (NMAX/Aerox)', 'pcs', 40, 260.00, 10400.00),
((SELECT MAX(POID) FROM PurchaseOrders), 12, 'Brembo Premium Brake Fluid DOT 4 (250ml)', 'pcs', 30, 140.00, 4200.00),
((SELECT MAX(POID) FROM PurchaseOrders), 9, 'Yamaha Aerox 155 Air Filter', 'pcs', 10, 280.00, 2800.00);

INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate) VALUES
(11, 'Stock-In', 40, 'PO-20260905-4004', 'PO Delivery', 'Received from Brembo Racing PH', 'Mark Reyes', '2026-09-07 14:00:00'),
(12, 'Stock-In', 30, 'PO-20260905-4004', 'PO Delivery', 'Received from Brembo Racing PH', 'Mark Reyes', '2026-09-07 14:00:00'),
(9, 'Stock-In', 10, 'PO-20260905-4004', 'PO Delivery', 'Received from Brembo Racing PH', 'Mark Reyes', '2026-09-07 14:00:00');

INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) VALUES
(3, 'clerk', 'Inventory Manager', 'PURCHASE_ORDER', 'Created Purchase Order PO-20260905-4004 for Brembo Racing PH (Total: ₱17,400.00)', '2026-09-05 13:00:00'),
(3, 'clerk', 'Inventory Manager', 'STOCK_IN', 'Verified delivery and received items for PO-20260905-4004 into inventory.', '2026-09-07 14:00:00');

-- PO 5: PowerDrive Wholesale Distribution (Sep 18, 2026)
INSERT INTO PurchaseOrders (PONumber, SupplierID, RequesterName, TotalAmount, Status, Remarks, Terms, ShippedVia, OrderDate, ExpectedDate)
VALUES ('PO-20260918-4005', 5, 'Mark Reyes', 18700.00, 'Received', 'Bando Drive Belts and 13g Roller Flyball weights', 'Net 30', 'PowerDrive Van', '2026-09-18 10:00:00', '2026-09-20 15:30:00');

INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice) VALUES 
((SELECT MAX(POID) FROM PurchaseOrders), 13, 'Bando Power Drive Belt (Honda Click 125/150)', 'pcs', 35, 250.00, 8750.00),
((SELECT MAX(POID) FROM PurchaseOrders), 14, 'Bando Flyball Set 13g (Aerox/NMAX)', 'pcs', 45, 130.00, 5850.00),
((SELECT MAX(POID) FROM PurchaseOrders), 9, 'Yamaha Aerox 155 Air Filter', 'pcs', 15, 270.00, 4050.00);

INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate) VALUES
(13, 'Stock-In', 35, 'PO-20260918-4005', 'PO Delivery', 'Received from PowerDrive Wholesale', 'Mark Reyes', '2026-09-20 15:30:00'),
(14, 'Stock-In', 45, 'PO-20260918-4005', 'PO Delivery', 'Received from PowerDrive Wholesale', 'Mark Reyes', '2026-09-20 15:30:00'),
(9, 'Stock-In', 15, 'PO-20260918-4005', 'PO Delivery', 'Received from PowerDrive Wholesale', 'Mark Reyes', '2026-09-20 15:30:00');

INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) VALUES
(3, 'clerk', 'Inventory Manager', 'PURCHASE_ORDER', 'Created Purchase Order PO-20260918-4005 for PowerDrive Wholesale Distribution (Total: ₱18,700.00)', '2026-09-18 10:00:00'),
(3, 'clerk', 'Inventory Manager', 'STOCK_IN', 'Verified delivery and received items for PO-20260918-4005 into inventory.', '2026-09-20 15:30:00');

-- PO 6: Yamaha Motor Philippines (Oct 02, 2026)
INSERT INTO PurchaseOrders (PONumber, SupplierID, RequesterName, TotalAmount, Status, Remarks, Terms, ShippedVia, OrderDate, ExpectedDate)
VALUES ('PO-20261002-4006', 2, 'Mark Reyes', 21500.00, 'Received', 'Monthly Yamalube oils & NMAX drive belts restock', 'Net 30', 'Yamaha Express Fleet', '2026-10-02 11:00:00', '2026-10-04 14:00:00');

INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice) VALUES 
((SELECT MAX(POID) FROM PurchaseOrders), 5, 'Yamalube AT 10W-40 4T (800ml)', 'pcs', 50, 150.00, 7500.00),
((SELECT MAX(POID) FROM PurchaseOrders), 6, 'Yamalube Scooter Gear Oil (100ml)', 'pcs', 40, 45.00, 1800.00),
((SELECT MAX(POID) FROM PurchaseOrders), 10, 'Yamaha Genuine V-Belt (NMAX V1/V2)', 'pcs', 20, 360.00, 7200.00),
((SELECT MAX(POID) FROM PurchaseOrders), 9, 'Yamaha Aerox 155 Air Filter', 'pcs', 18, 270.00, 4860.00);

INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate) VALUES
(5, 'Stock-In', 50, 'PO-20261002-4006', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-10-04 14:00:00'),
(6, 'Stock-In', 40, 'PO-20261002-4006', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-10-04 14:00:00'),
(10, 'Stock-In', 20, 'PO-20261002-4006', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-10-04 14:00:00'),
(9, 'Stock-In', 18, 'PO-20261002-4006', 'PO Delivery', 'Received from Yamaha Motor Philippines', 'Mark Reyes', '2026-10-04 14:00:00');

INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) VALUES
(3, 'clerk', 'Inventory Manager', 'PURCHASE_ORDER', 'Created Purchase Order PO-20261002-4006 for Yamaha Motor Philippines (Total: ₱21,500.00)', '2026-10-02 11:00:00'),
(3, 'clerk', 'Inventory Manager', 'STOCK_IN', 'Verified delivery and received items for PO-20261002-4006 into inventory.', '2026-10-04 14:00:00');

-- PO 7: Motul Philippines (Oct 15, 2026)
INSERT INTO PurchaseOrders (PONumber, SupplierID, RequesterName, TotalAmount, Status, Remarks, Terms, ShippedVia, OrderDate, ExpectedDate)
VALUES ('PO-20261015-4007', 1, 'Mark Reyes', 18000.00, 'Received', 'Mid-October lubricants & cleaner spray batch', 'Net 30', 'Motul Logistics Van', '2026-10-15 09:30:00', '2026-10-17 11:30:00');

INSERT INTO PurchaseOrderItems (POID, ProductID, ItemDescription, Unit, Quantity, UnitPrice, TotalPrice) VALUES 
((SELECT MAX(POID) FROM PurchaseOrders), 1, 'Motul Scooter Expert LE 10W40 4T (800ml)', 'pcs', 50, 180.00, 9000.00),
((SELECT MAX(POID) FROM PurchaseOrders), 2, 'Motul Scooter Gear Oil 80W90 (120ml)', 'pcs', 45, 60.00, 2700.00),
((SELECT MAX(POID) FROM PurchaseOrders), 3, 'Motul Brake & Parts Clean Spray (400ml)', 'pcs', 42, 150.00, 6300.00);

INSERT INTO StockTransactions (ProductID, TransactionType, Quantity, ReferenceNumber, Reason, Notes, ProcessedBy, TransactionDate) VALUES
(1, 'Stock-In', 50, 'PO-20261015-4007', 'PO Delivery', 'Received from Motul Philippines', 'Mark Reyes', '2026-10-17 11:30:00'),
(2, 'Stock-In', 45, 'PO-20261015-4007', 'PO Delivery', 'Received from Motul Philippines', 'Mark Reyes', '2026-10-17 11:30:00'),
(3, 'Stock-In', 42, 'PO-20261015-4007', 'PO Delivery', 'Received from Motul Philippines', 'Mark Reyes', '2026-10-17 11:30:00');

INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) VALUES
(3, 'clerk', 'Inventory Manager', 'PURCHASE_ORDER', 'Created Purchase Order PO-20261015-4007 for Motul Philippines (Total: ₱18,000.00)', '2026-10-15 09:30:00'),
(3, 'clerk', 'Inventory Manager', 'STOCK_IN', 'Verified delivery and received items for PO-20261015-4007 into inventory.', '2026-10-17 11:30:00');

PRINT '✔️ Purchase Orders and Supplier Deliveries generated.';

-- ============================================================================
-- 11. DAILY SALES & JOB ORDERS LOOP (AUG 21, 2026 - OCT 19, 2026)
-- Target: 15-28 Customers/Day
-- Weekly Revenue: ₱10,000 - ₱19,500
-- Format: INV-YYYYMMDD-0001 & JO-YYYYMMDD-0001
-- Connected to History Logs: INVENTORY, MAINTENANCE, LOGIN, LOGOUT
-- ============================================================================

-- Table variable of realistic Filipino Customer Names (NO celebrities)
DECLARE @Customers TABLE (ID INT IDENTITY(1,1), Name NVARCHAR(100));
INSERT INTO @Customers (Name) VALUES 
('Angelo Santos'), ('Christian Ramos'), ('Michael Bautista'), ('Joshua Garcia'), ('Daniel Fernandez'),
('John Paul Dela Cruz'), ('Mark Anthony Reyes'), ('Gabriel Mendoza'), ('Kevin Soriano'), ('Rafael Villanueva'),
('Rommel Cruz'), ('Jerome Dizon'), ('Arnel Tolentino'), ('Brian Castillo'), ('Dennis Manalo'),
('Jeffrey Aquino'), ('Eduardo Reyes'), ('Jason Cortez'), ('Marvin Valenzuela'), ('Ronald Pineda'),
('Lester Pascual'), ('Carlo De Leon'), ('Ryan Navarro'), ('Noel Santiago'), ('Joseph Salazar'),
('Patrick Ocampo'), ('Francis Morales'), ('Roderick Tan'), ('Victor Flores'), ('Elmer Gutierrez'),
('Gerald David'), ('Raymond Macaraeg'), ('Kenneth David'), ('Alvin Del Rosario'), ('Renato Salvador'),
('Dexter Gomez'), ('Paolo Alcantara'), ('Darwin Espino'), ('Emmanuel Castro'), ('Oliver Hernandez');

-- Popular Philippine motorcycle units and authentic plate numbers
DECLARE @Bikes TABLE (ID INT IDENTITY(1,1), Model NVARCHAR(100), Plate NVARCHAR(50));
INSERT INTO @Bikes (Model, Plate) VALUES 
('Yamaha NMAX 155', 'ND 4582'), ('Yamaha NMAX 155', '892-NMX'), ('Yamaha NMAX 155', '104-NMX'),
('Honda Click 125i', '741-CLK'), ('Honda Click 125i', 'NC 9831'), ('Honda Click 125i', '294-HND'),
('Yamaha Aerox 155', '542-ARX'), ('Yamaha Aerox 155', 'NA 7721'), ('Yamaha Aerox 155', '619-ARX'),
('Honda Beat 110', '315-BET'), ('Honda Beat 110', 'NB 4490'), ('Honda Beat 110', '720-BET'),
('Honda ADV 160', '903-ADV'), ('Honda ADV 160', 'ND 1290'), ('Suzuki Raider 150', '910-RDR'),
('Yamaha Mio i125', '629-MIO'), ('Yamaha Mio i125', 'NM 5519');

DECLARE @Phones TABLE (ID INT IDENTITY(1,1), Prefix NVARCHAR(10));
INSERT INTO @Phones (Prefix) VALUES ('0917'), ('0918'), ('0920'), ('0998'), ('0977'), ('0915'), ('0927'), ('0939');

DECLARE @CustCount INT = (SELECT COUNT(*) FROM @Customers);
DECLARE @BikeCount INT = (SELECT COUNT(*) FROM @Bikes);
DECLARE @PhoneCount INT = (SELECT COUNT(*) FROM @Phones);

-- Date Loop: Aug 21, 2026 to Oct 19, 2026
DECLARE @StartDate DATE = '2026-08-21';
DECLARE @EndDate DATE = '2026-10-19';
DECLARE @CurrDate DATE = @StartDate;

WHILE @CurrDate <= @EndDate
BEGIN
    DECLARE @DateStr VARCHAR(8) = CONVERT(VARCHAR(8), @CurrDate, 112); -- e.g. '20260821'
    DECLARE @DayLogStr VARCHAR(10) = CONVERT(VARCHAR(10), @CurrDate, 120);
    
    -- Morning Logins for Shift Start
    INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) 
    VALUES (1, 'admin', 'Admin', 'LOGIN', 'Administrator Rich Roque logged in.', CAST(@DayLogStr + ' 07:45:10' AS DATETIME));

    INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) 
    VALUES (2, 'cashier', 'Cashier', 'LOGIN', 'Cashier Ruby Dela Cruz opened POS shift drawer.', CAST(@DayLogStr + ' 07:55:25' AS DATETIME));

    INSERT INTO AuditLogs (UserID, Username, Role, ActionType, Details, Timestamp) 
    VALUES (3, 'clerk', 'Inventory Manager', 'LOGIN', 'Inventory Manager Mark Reyes signed in.', CAST(@DayLogStr + '