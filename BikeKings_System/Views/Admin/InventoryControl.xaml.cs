#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using BikeKings_System.Database;
using BikeKings_System.Helpers;

namespace BikeKings_System.Views.Admin
{
    public partial class InventoryControl : UserControl
    {
        private List<ProductItem> _allProducts = new List<ProductItem>();
        private List<POLineItem> _createPOLineItems = new List<POLineItem>();
        private List<POLineItem> _editPOLineItems = new List<POLineItem>();
        private PurchaseOrderItem _selectedPO = null;
        private int? _editingProductId = null;

        private ProductSalesHistorySummary _activeProductHistory = null;
        private List<ProductSalesHistoryEntry> _allHistoryEntries = new List<ProductSalesHistoryEntry>();

        // Regex input validation (Includes + for bundles, " for inches, % for synthetic, * for dimensions)
        private static readonly Regex _skuRegex = new Regex(@"^[a-zA-Z0-9\-_./]{2,50}$", RegexOptions.Compiled);
        private static readonly Regex _nameRegex = new Regex(@"^[a-zA-Z0-9\s.,&'\-()/#+""%*\[\]:;!@]{2,200}$", RegexOptions.Compiled);
        private static readonly Regex _decimalRegex = new Regex(@"^\d+(\.\d{1,2})?$", RegexOptions.Compiled);
        private static readonly Regex _intRegex = new Regex(@"^\d+$", RegexOptions.Compiled);
        private static readonly Regex _numFilter = new Regex(@"[^0-9.]", RegexOptions.Compiled);
        private static readonly Regex _intFilter = new Regex(@"[^0-9]", RegexOptions.Compiled);

        private bool _isManualSku = false;

        public InventoryControl()
        {
            InitializeComponent();
            ApplyRoleRestrictions();
            LoadInitialData();
        }

        public void LoadData() => LoadInitialData();
        public void RefreshData() => LoadInitialData();

        // =========================================================================
        // ── ROLE PERMISSION RESTRICTIONS ─────────────────────────────────────────
        // =========================================================================
        private void ApplyRoleRestrictions()
        {
            string role = SessionHelper.CurrentUser?.Role ?? "Admin";

            bool isInventoryManager = role.Equals("StoreClerk", StringComparison.OrdinalIgnoreCase) ||
                                      role.Equals("Store Clerk", StringComparison.OrdinalIgnoreCase) ||
                                      role.Equals("Inventory Manager", StringComparison.OrdinalIgnoreCase) ||
                                      role.Equals("Clerk", StringComparison.OrdinalIgnoreCase);

            bool isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                           role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                           role.Equals("Manager", StringComparison.OrdinalIgnoreCase);

            // Admin indicator badge
            if (pnlAdminBadge != null)
            {
                pnlAdminBadge.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            }
            // Clerk indicator badge
            if (pnlClerkBadge != null)
            {
                pnlClerkBadge.Visibility = isInventoryManager ? Visibility.Visible : Visibility.Collapsed;
            }
            // Operations strictly for Inventory Manager only (Admin is read-only for masterlist)
            if (btnAddProduct != null) btnAddProduct.Visibility = isInventoryManager ? Visibility.Visible : Visibility.Collapsed;
            if (btnOpenStockIn != null) btnOpenStockIn.Visibility = isInventoryManager ? Visibility.Visible : Visibility.Collapsed;
            if (btnOpenStockOut != null) btnOpenStockOut.Visibility = isInventoryManager ? Visibility.Visible : Visibility.Collapsed;
            if (btnTopQuickPO != null) btnTopQuickPO.Visibility = isInventoryManager ? Visibility.Visible : Visibility.Collapsed;

            // PO Document Modal actions: Administrator AND Inventory Manager can EDIT any pending PO before sending out!
            if (btnModalEditPO != null)
            {
                btnModalEditPO.Visibility = (_selectedPO != null && _selectedPO.IsPending) ? Visibility.Visible : Visibility.Collapsed;
            }
            if (btnModalApprovePO != null)
            {
                btnModalApprovePO.Visibility = (isAdmin && _selectedPO != null && _selectedPO.IsPending) ? Visibility.Visible : Visibility.Collapsed;
            }
            if (btnModalRejectPO != null)
            {
                btnModalRejectPO.Visibility = (isAdmin && _selectedPO != null && _selectedPO.IsPending) ? Visibility.Visible : Visibility.Collapsed;
            }
            if (btnModalReceivePO != null)
            {
                btnModalReceivePO.Visibility = (isInventoryManager && _selectedPO != null && _selectedPO.IsApproved) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // =========================================================================
        // ── EXTERNAL NAVIGATION HELPERS (CALLED BY ADMIN DASHBOARD) ──────────────
        // =========================================================================
        public void ShowPurchaseOrdersTab()
        {
            if (tabBtnPO != null)
            {
                tabBtnPO.IsChecked = true;
                TabNav_Click(tabBtnPO, null);
            }
        }

        public void FocusProduct(string barcodeOrSku)
        {
            if (string.IsNullOrWhiteSpace(barcodeOrSku)) return;

            if (tabBtnProducts != null)
            {
                tabBtnProducts.IsChecked = true;
                TabNav_Click(tabBtnProducts, null);
            }

            if (txtSearchProducts != null)
            {
                txtSearchProducts.Text = barcodeOrSku.Trim();
            }

            ApplyProductFilters();

            if (_allProducts != null)
            {
                var found = _allProducts.FirstOrDefault(p =>
                    string.Equals(p.SKU, barcodeOrSku, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Barcode, barcodeOrSku, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                {
                    OpenProductSalesHistoryModal(found);
                }
            }
        }

        public void FilterByCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return;

            if (tabBtnProducts != null)
            {
                tabBtnProducts.IsChecked = true;
                TabNav_Click(tabBtnProducts, null);
            }

            if (cmbFilterCategory != null)
            {
                foreach (var item in cmbFilterCategory.Items)
                {
                    if (item is CategoryModel cat && string.Equals(cat.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        cmbFilterCategory.SelectedItem = item;
                        break;
                    }
                }
            }

            ApplyProductFilters();
        }

        public void FilterByCategory(int categoryId)
        {
            if (tabBtnProducts != null)
            {
                tabBtnProducts.IsChecked = true;
                TabNav_Click(tabBtnProducts, null);
            }

            if (cmbFilterCategory != null)
            {
                cmbFilterCategory.SelectedValue = categoryId;
            }

            ApplyProductFilters();
        }

        public void FilterByStockStatus(string status)
        {
            if (tabBtnProducts != null)
            {
                tabBtnProducts.IsChecked = true;
                TabNav_Click(tabBtnProducts, null);
            }

            if (cmbFilterStockStatus != null)
            {
                for (int i = 0; i < cmbFilterStockStatus.Items.Count; i++)
                {
                    if (cmbFilterStockStatus.Items[i] is ComboBoxItem cbi &&
                        cbi.Content != null &&
                        cbi.Content.ToString().Contains(status, StringComparison.OrdinalIgnoreCase))
                    {
                        cmbFilterStockStatus.SelectedIndex = i;
                        break;
                    }
                }
            }

            ApplyProductFilters();
        }

        // =========================================================================
        // ── DATA LOADING & INITIALIZATION ────────────────────────────────────────
        // =========================================================================
        private void LoadInitialData()
        {
            ApplyRoleRestrictions();
            LoadDropdowns();
            LoadProducts();
            LoadPurchaseOrders();
            LoadAnalytics();
            LoadMonitoring();
        }

        private void LoadDropdowns()
        {
            var categories = InventoryService.GetCategories() ?? new List<CategoryModel>();
            var brands = InventoryService.GetBrands() ?? new List<BrandModel>();
            var suppliers = InventoryService.GetSuppliers() ?? new List<SupplierModel>();

            // Masterlist Filters
            if (cmbFilterCategory != null)
            {
                var filterCats = new List<CategoryModel> { new CategoryModel { CategoryID = 0, CategoryName = "All Categories" } };
                filterCats.AddRange(categories);
                cmbFilterCategory.ItemsSource = filterCats;
                cmbFilterCategory.SelectedIndex = 0;
            }

            if (cmbFilterBrand != null)
            {
                var filterBrands = new List<BrandModel> { new BrandModel { BrandID = 0, BrandName = "All Brands" } };
                filterBrands.AddRange(brands);
                cmbFilterBrand.ItemsSource = filterBrands;
                cmbFilterBrand.SelectedIndex = 0;
            }

            // Product Modal Form Dropdowns
            if (cmbProdCategory != null)
            {
                cmbProdCategory.ItemsSource = categories;
                cmbProdCategory.DisplayMemberPath = "CategoryName";
                cmbProdCategory.SelectedValuePath = "CategoryID";
            }

            if (cmbProdBrand != null)
            {
                cmbProdBrand.ItemsSource = brands;
                cmbProdBrand.DisplayMemberPath = "BrandName";
                cmbProdBrand.SelectedValuePath = "BrandID";
            }

            // Purchase Order Form Dropdowns (With Smart Recommendations)
            var recommendedSuppliers = GetSuppliersWithRecommendations();
            if (cmbPOSupplier != null)
            {
                cmbPOSupplier.ItemsSource = recommendedSuppliers;
                cmbPOSupplier.DisplayMemberPath = "DisplayName";
                cmbPOSupplier.SelectedValuePath = "SupplierID";
            }
            if (cmbEditPOSupplier != null)
            {
                cmbEditPOSupplier.ItemsSource = recommendedSuppliers;
                cmbEditPOSupplier.DisplayMemberPath = "DisplayName";
                cmbEditPOSupplier.SelectedValuePath = "SupplierID";
            }

            if (cmbProdSupplier != null)
            {
                cmbProdSupplier.ItemsSource = suppliers;
                cmbProdSupplier.DisplayMemberPath = "SupplierName";
                cmbProdSupplier.SelectedValuePath = "SupplierID";
            }

            if (cmbStockInSupplier != null)
            {
                cmbStockInSupplier.ItemsSource = suppliers;
                cmbStockInSupplier.DisplayMemberPath = "SupplierName";
                cmbStockInSupplier.SelectedValuePath = "SupplierID";
            }

            // Stock In / Out / PO Product Selectors
            var activeProducts = _allProducts?.Where(p => p.IsActive).ToList() ?? new List<ProductItem>();
            if (cmbStockInProduct != null)
            {
                cmbStockInProduct.ItemsSource = activeProducts;
                cmbStockInProduct.DisplayMemberPath = "ProductName";
                cmbStockInProduct.SelectedValuePath = "ProductID";
            }

            if (cmbStockOutProduct != null)
            {
                cmbStockOutProduct.ItemsSource = activeProducts;
                cmbStockOutProduct.DisplayMemberPath = "ProductName";
                cmbStockOutProduct.SelectedValuePath = "ProductID";
            }

            if (cmbPOProduct != null)
            {
                cmbPOProduct.ItemsSource = activeProducts;
                cmbPOProduct.DisplayMemberPath = "ProductName";
                cmbPOProduct.SelectedValuePath = "ProductID";
            }

            if (cmbEditPOProduct != null)
            {
                cmbEditPOProduct.ItemsSource = activeProducts;
                cmbEditPOProduct.DisplayMemberPath = "ProductName";
                cmbEditPOProduct.SelectedValuePath = "ProductID";
            }
        }

        // =========================================================================
        // ── TAB NAVIGATION ───────────────────────────────────────────────────────
        // =========================================================================
        private void TabNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            if (tabContentProducts != null) tabContentProducts.Visibility = Visibility.Collapsed;
            if (tabContentStockIn != null) tabContentStockIn.Visibility = Visibility.Collapsed;
            if (tabContentStockOut != null) tabContentStockOut.Visibility = Visibility.Collapsed;
            if (tabContentPO != null) tabContentPO.Visibility = Visibility.Collapsed;
            if (tabContentAnalytics != null) tabContentAnalytics.Visibility = Visibility.Collapsed;
            if (tabContentMonitoring != null) tabContentMonitoring.Visibility = Visibility.Collapsed;

            switch (rb.Name)
            {
                case "tabBtnProducts":
                    if (tabContentProducts != null) tabContentProducts.Visibility = Visibility.Visible;
                    break;
                case "tabBtnStockIn":
                    if (tabContentStockIn != null) tabContentStockIn.Visibility = Visibility.Visible;
                    LoadStockInLogs();
                    break;
                case "tabBtnStockOut":
                    if (tabContentStockOut != null) tabContentStockOut.Visibility = Visibility.Visible;
                    LoadStockOutLogs();
                    break;
                case "tabBtnPO":
                    if (tabContentPO != null) tabContentPO.Visibility = Visibility.Visible;
                    LoadPurchaseOrders();
                    break;
                case "tabBtnAnalytics":
                    if (tabContentAnalytics != null) tabContentAnalytics.Visibility = Visibility.Visible;
                    LoadAnalytics();
                    break;
                case "tabBtnMonitoring":
                    if (tabContentMonitoring != null) tabContentMonitoring.Visibility = Visibility.Visible;
                    LoadMonitoring();
                    break;
            }
        }

        // =========================================================================
        // ── PRODUCT MASTERLIST & FILTERS ─────────────────────────────────────────
        // =========================================================================
        private void LoadProducts()
        {
            _allProducts = InventoryService.GetProducts() ?? new List<ProductItem>();

            ApplyProductFilters();

            // Refresh Product Selectors
            var activeProducts = _allProducts.Where(p => p.IsActive).ToList();
            if (cmbStockInProduct != null) cmbStockInProduct.ItemsSource = activeProducts;
            if (cmbStockOutProduct != null) cmbStockOutProduct.ItemsSource = activeProducts;
            if (cmbPOProduct != null) cmbPOProduct.ItemsSource = activeProducts;
            if (cmbEditPOProduct != null) cmbEditPOProduct.ItemsSource = activeProducts;
        }

        private void ApplyProductFilters()
        {
            if (_allProducts == null) return;

            string search = txtSearchProducts?.Text?.Trim().ToLower() ?? "";
            int selectedCatId = cmbFilterCategory?.SelectedValue is int catId ? catId : 0;
            int selectedBrandId = cmbFilterBrand?.SelectedValue is int brandId ? brandId : 0;
            string selectedStatus = (cmbFilterStockStatus?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Stock Status";
            string sortOrder = (cmbSortProducts?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Name (A-Z)";

            var filtered = _allProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(p =>
                    (p.ProductName != null && p.ProductName.ToLower().Contains(search)) ||
                    (p.SKU != null && p.SKU.ToLower().Contains(search)) ||
                    (p.Barcode != null && p.Barcode.ToLower().Contains(search)) ||
                    (p.BrandName != null && p.BrandName.ToLower().Contains(search)) ||
                    (p.CategoryName != null && p.CategoryName.ToLower().Contains(search)));
            }

            if (selectedCatId > 0)
            {
                filtered = filtered.Where(p => p.CategoryID == selectedCatId);
            }

            if (selectedBrandId > 0)
            {
                filtered = filtered.Where(p => p.BrandID == selectedBrandId);
            }

            if (!string.Equals(selectedStatus, "All Stock Status", StringComparison.OrdinalIgnoreCase))
            {
                if (selectedStatus.Contains("Out", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(p => p.StockQuantity <= 0);
                else if (selectedStatus.Contains("Low", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(p => p.StockQuantity > 0 && p.StockQuantity <= p.ReorderLevel);
                else if (selectedStatus.Contains("Good", StringComparison.OrdinalIgnoreCase) || selectedStatus.Contains("In Stock", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(p => p.StockQuantity > p.ReorderLevel);
            }

            // Sorting
            if (sortOrder.Contains("Fast-Moving", StringComparison.OrdinalIgnoreCase))
            {
                var salesMap = InventoryService.GetStockAnalytics()?.ToDictionary(x => x.ProductID, x => x.UnitsSoldLast30Days) ?? new Dictionary<int, int>();
                filtered = filtered.OrderByDescending(p => salesMap.ContainsKey(p.ProductID) ? salesMap[p.ProductID] : 0);
            }
            else if (sortOrder.Contains("Slowest", StringComparison.OrdinalIgnoreCase))
            {
                var salesMap = InventoryService.GetStockAnalytics()?.ToDictionary(x => x.ProductID, x => x.UnitsSoldLast30Days) ?? new Dictionary<int, int>();
                filtered = filtered.OrderBy(p => salesMap.ContainsKey(p.ProductID) ? salesMap[p.ProductID] : 0);
            }
            else if (sortOrder.Contains("Name (Z-A)", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.OrderByDescending(p => p.ProductName);
            else if (sortOrder.Contains("Lowest First", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.OrderBy(p => p.StockQuantity);
            else if (sortOrder.Contains("Highest First", StringComparison.OrdinalIgnoreCase) && !sortOrder.Contains("Price"))
                filtered = filtered.OrderByDescending(p => p.StockQuantity);
            else if (sortOrder.Contains("Price", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.OrderByDescending(p => p.UnitPrice);
            else
                filtered = filtered.OrderBy(p => p.ProductName);

            var result = filtered.ToList();
            if (dgProducts != null)
            {
                dgProducts.ItemsSource = result;
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyProductFilters();

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            if (txtSearchProducts != null) txtSearchProducts.Text = "";
            if (cmbFilterCategory != null) cmbFilterCategory.SelectedIndex = 0;
            if (cmbFilterBrand != null) cmbFilterBrand.SelectedIndex = 0;
            if (cmbFilterStockStatus != null) cmbFilterStockStatus.SelectedIndex = 0;
            if (cmbSortProducts != null) cmbSortProducts.SelectedIndex = 0;
            ApplyProductFilters();
        }

        private ProductItem _selectedProductForDetails;

        private void DgProducts_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Do not trigger modal if the click was on an action button (Edit/Delete) or scrollbar
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<Button>(dep) != null) return;
                if (FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) != null) return;
            }

            if (dgProducts?.SelectedItem is ProductItem item)
            {
                OpenProductDetailsModal(item);
            }
        }

        private void OpenProductDetailsModal(ProductItem item)
        {
            if (item == null) return;
            _selectedProductForDetails = item;

            // Populate all details
            if (lblDetailProdName != null) lblDetailProdName.Text = item.ProductName;
            if (lblDetailSKU != null) lblDetailSKU.Text = item.SKU;
            if (lblDetailCategory != null) lblDetailCategory.Text = string.IsNullOrWhiteSpace(item.CategoryName) ? "Uncategorized" : item.CategoryName;
            if (lblDetailBrand != null) lblDetailBrand.Text = string.IsNullOrWhiteSpace(item.BrandName) ? "Generic / OEM" : item.BrandName;
            if (lblDetailSupplier != null) lblDetailSupplier.Text = string.IsNullOrWhiteSpace(item.SupplierName) || item.SupplierName == "Not Assigned" ? "Not Assigned" : item.SupplierName;
            if (lblDetailStock != null)
            {
                lblDetailStock.Text = $"{item.StockQuantity:N0} units";
                lblDetailStock.Foreground = new System.Windows.Media.BrushConverter().ConvertFromString(item.StatusColor) as System.Windows.Media.Brush;
            }
            if (lblDetailReorder != null) lblDetailReorder.Text = $"{item.ReorderLevel:N0} units";
            string role = SessionHelper.CurrentUser?.Role ?? "Admin";
            bool isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                           role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                           role.Equals("Manager", StringComparison.OrdinalIgnoreCase);
            if (lblDetailPrice != null) lblDetailPrice.Text = $"₱{item.UnitPrice:N2}";
            if (lblDetailPotentialRev != null) lblDetailPotentialRev.Text = $"₱{item.PotentialRevenue:N2}";
            // 🛡️ Cost & Asset Value visible to both; Profit Margin strictly restricted to Admin
            if (lblDetailCost != null) lblDetailCost.Text = $"₱{item.CostPrice:N2}";
            if (lblDetailStockValue != null) lblDetailStockValue.Text = $"₱{item.TotalValue:N2}";
            if (isAdmin)
            {
                if (lblDetailProfit != null) lblDetailProfit.Text = $"₱{item.ProfitMargin:N2} ({item.Margin:F1}%)";
            }
            else
            {
                if (lblDetailProfit != null) lblDetailProfit.Text = "Protected";
            }
            if (lblDetailSuggestedReorder != null) lblDetailSuggestedReorder.Text = $"{item.SuggestedReorderQty:N0} units";
            if (lblDetailStatus != null && bdrDetailStatus != null)
            {
                lblDetailStatus.Text = item.StockStatus;
                lblDetailStatus.Foreground = new System.Windows.Media.BrushConverter().ConvertFromString(item.StatusFgColor) as System.Windows.Media.Brush;
                bdrDetailStatus.Background = new System.Windows.Media.BrushConverter().ConvertFromString(item.StatusBgColor) as System.Windows.Media.Brush;
            }

            // ⚡ Live 30-Day Movement Velocity Badge
            var statsItem = InventoryService.GetStockAnalytics()?.FirstOrDefault(x => x.ProductID == item.ProductID);
            if (statsItem != null && bdrDetailVelocity != null && lblDetailVelocity != null)
            {
                lblDetailVelocity.Text = $"{statsItem.VelocityCategory} ({statsItem.UnitsSoldLast30Days} sold / 30d)";
                lblDetailVelocity.Foreground = new System.Windows.Media.BrushConverter().ConvertFromString(statsItem.VelocityFgColor) as System.Windows.Media.Brush;
                bdrDetailVelocity.Background = new System.Windows.Media.BrushConverter().ConvertFromString(statsItem.VelocityBgColor) as System.Windows.Media.Brush;
                bdrDetailVelocity.Visibility = Visibility.Visible;
            }
            else if (bdrDetailVelocity != null)
            {
                bdrDetailVelocity.Visibility = Visibility.Collapsed;
            }

            if (modalProductDetails != null) modalProductDetails.Visibility = Visibility.Visible;
        }

        private void BtnCloseProductDetails_Click(object sender, RoutedEventArgs e)
        {
            if (modalProductDetails != null) modalProductDetails.Visibility = Visibility.Collapsed;
        }

        private void BtnViewSalesHistoryFromDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProductForDetails != null)
            {
                if (modalProductDetails != null) modalProductDetails.Visibility = Visibility.Collapsed;
                OpenProductSalesHistoryModal(_selectedProductForDetails);
            }
        }

        private void BtnCreatePOFromDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProductForDetails == null) return;

            // 1. Close Product Details modal
            if (modalProductDetails != null) modalProductDetails.Visibility = Visibility.Collapsed;

            // 2. Pre-calculate recommended restock quantity
            int orderQty = _selectedProductForDetails.SuggestedReorderQty > 0
                ? _selectedProductForDetails.SuggestedReorderQty
                : (_selectedProductForDetails.ReorderLevel > 0 ? _selectedProductForDetails.ReorderLevel * 2 : 10);

            // 3. Pre-add this product to PO draft items
            _createPOLineItems.Clear();
            _createPOLineItems.Add(new POLineItem
            {
                ProductID = _selectedProductForDetails.ProductID,
                ItemDescription = $"{_selectedProductForDetails.ProductName} ({_selectedProductForDetails.SKU})",
                Unit = "pcs",
                Quantity = orderQty,
                UnitPrice = _selectedProductForDetails.CostPrice,
                TotalPrice = orderQty * _selectedProductForDetails.CostPrice
            });

            // 4. Auto-recommend and select best matching supplier
            var recSuppliers = GetSuppliersWithRecommendations(_createPOLineItems);
            if (cmbPOSupplier != null)
            {
                cmbPOSupplier.ItemsSource = recSuppliers;
                var topRec = recSuppliers.FirstOrDefault(s => s.IsRecommended);
                if (topRec != null)
                {
                    cmbPOSupplier.SelectedItem = topRec;
                    cmbPOSupplier.SelectedValue = topRec.SupplierID;
                    cmbPOSupplier.Text = topRec.DisplayName;
                }
                else
                {
                    cmbPOSupplier.SelectedIndex = -1;
                    cmbPOSupplier.Text = "";
                }
            }

            // 5. Populate default notes & reset entry line
            if (cmbPOProduct != null)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(cmbPOProduct.ItemsSource);
                if (view != null) view.Filter = null;
                cmbPOProduct.SelectedIndex = -1;
                cmbPOProduct.Text = "";
            }
            txtPOQty.Text = "1";
            txtPOUnitPrice.Text = "0.00";
            if (cmbPOTerms != null) cmbPOTerms.SelectedIndex = 1; // Default to Net 30
            txtPONotes.Text = $"Restock order for {_selectedProductForDetails.ProductName} ({_selectedProductForDetails.SKU})";

            RefreshCreatePOGrid();

            // 6. Instantly display Create PO modal with everything ready
            if (modalPO != null) modalPO.Visibility = Visibility.Visible;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // =========================================================================
        // ── PURCHASE ORDERS MANAGEMENT ───────────────────────────────────────────
        // =========================================================================
        private void LoadPurchaseOrders()
        {
            var pos = InventoryService.GetPurchaseOrders() ?? new List<PurchaseOrderItem>();

            int pendingCount = pos.Count(p => p.IsPending);
            if (pnlAdminPOAlertBanner != null)
            {
                string role = SessionHelper.CurrentUser?.Role ?? "Admin";
                bool isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                               role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                               role.Equals("Manager", StringComparison.OrdinalIgnoreCase);

                pnlAdminPOAlertBanner.Visibility = (isAdmin && pendingCount > 0) ? Visibility.Visible : Visibility.Collapsed;
                if (lblPendingPOCount != null) lblPendingPOCount.Text = $"{pendingCount} Purchase Order(s) Awaiting Admin Approval";
            }

            if (dgPurchaseOrders != null) dgPurchaseOrders.ItemsSource = pos;
        }



        // =========================================================================
        // ── STOCK IN / OUT LOGS ──────────────────────────────────────────────────
        // =========================================================================
        private void LoadStockInLogs()
        {
            var logs = InventoryService.GetStockInLogs() ?? new List<StockTransactionItem>();
            if (dgStockIn != null) dgStockIn.ItemsSource = logs;
        }

        private void LoadStockOutLogs()
        {
            var logs = InventoryService.GetStockOutLogs() ?? new List<StockTransactionItem>();
            if (dgStockOut != null) dgStockOut.ItemsSource = logs;
        }

        // =========================================================================
        // ── STOCK ANALYTICS & MONITORING (WITH VELOCITY FILTERING) ───────────────
        // =========================================================================
        private List<StockAnalyticsItem> _allAnalytics = new List<StockAnalyticsItem>();
        private string _selectedVelocityFilter = "All";
        private void LoadAnalytics()
        {
            _allAnalytics = InventoryService.GetStockAnalytics() ?? new List<StockAnalyticsItem>();
            // Calculate live totals for the 4 summary cards
            int fast = _allAnalytics.Count(x => x.VelocityCategory.Contains("FAST"));
            int mod = _allAnalytics.Count(x => x.VelocityCategory.Contains("MODERATE"));
            int slow = _allAnalytics.Count(x => x.VelocityCategory.Contains("SLOW"));
            int dead = _allAnalytics.Count(x => x.VelocityCategory.Contains("DEAD"));
            if (lblFastCount != null) lblFastCount.Text = $"{fast} Items";
            if (lblModCount != null) lblModCount.Text = $"{mod} Items";
            if (lblSlowCount != null) lblSlowCount.Text = $"{slow} Items";
            if (lblDeadCount != null) lblDeadCount.Text = $"{dead} Items";
            ApplyAnalyticsFilter();
        }
        private void ApplyAnalyticsFilter()
        {
            if (_allAnalytics == null) return;
            var filtered = _allAnalytics.AsEnumerable();
            string search = txtSearchAnalytics?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(x =>
                    x.ProductName.ToLower().Contains(search) ||
                    x.SKU.ToLower().Contains(search) ||
                    x.CategoryName.ToLower().Contains(search));
            }
            if (_selectedVelocityFilter == "Fast")
                filtered = filtered.Where(x => x.VelocityCategory.Contains("FAST"));
            else if (_selectedVelocityFilter == "Moderate")
                filtered = filtered.Where(x => x.VelocityCategory.Contains("MODERATE"));
            else if (_selectedVelocityFilter == "Slow")
                filtered = filtered.Where(x => x.VelocityCategory.Contains("SLOW"));
            else if (_selectedVelocityFilter == "Dead")
                filtered = filtered.Where(x => x.VelocityCategory.Contains("DEAD"));
            if (dgStockAnalytics != null)
            {
                dgStockAnalytics.ItemsSource = filtered.ToList();
            }
        }
        private void TxtSearchAnalytics_TextChanged(object sender, TextChangedEventArgs e) => ApplyAnalyticsFilter();
        private void BtnFilterAnalytics_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                _selectedVelocityFilter = tag;
                ApplyAnalyticsFilter();
            }
        }
        private void CardFilter_Fast_Click(object sender, MouseButtonEventArgs e)
        {
            _selectedVelocityFilter = "Fast";
            ApplyAnalyticsFilter();
        }
        private void CardFilter_Mod_Click(object sender, MouseButtonEventArgs e)
        {
            _selectedVelocityFilter = "Moderate";
            ApplyAnalyticsFilter();
        }
        private void CardFilter_Slow_Click(object sender, MouseButtonEventArgs e)
        {
            _selectedVelocityFilter = "Slow";
            ApplyAnalyticsFilter();
        }
        private void CardFilter_Dead_Click(object sender, MouseButtonEventArgs e)
        {
            _selectedVelocityFilter = "Dead";
            ApplyAnalyticsFilter();
        }

        private void LoadMonitoring()
        {
            if (_allProducts == null || _allProducts.Count == 0)
                _allProducts = InventoryService.GetProducts() ?? new List<ProductItem>();

            var lowOrOut = _allProducts.Where(p => p.StockQuantity <= p.ReorderLevel).OrderBy(p => p.StockQuantity).ToList();
            if (dgStockMonitoring != null) dgStockMonitoring.ItemsSource = lowOrOut;
        }

        private void DgStockAnalytics_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<Button>(dep) != null) return;
                if (FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) != null) return;
            }

            if (dgStockAnalytics?.SelectedItem is StockAnalyticsItem item)
            {
                if (_allProducts == null || _allProducts.Count == 0)
                    _allProducts = InventoryService.GetProducts() ?? new List<ProductItem>();

                var product = _allProducts.FirstOrDefault(p => p.ProductID == item.ProductID)
                              ?? InventoryService.GetProducts()?.FirstOrDefault(p => p.ProductID == item.ProductID);

                if (product != null)
                {
                    OpenProductDetailsModal(product);
                }
            }
        }

        private void DgStockAnalytics_MouseDoubleClick(object sender, MouseButtonEventArgs e) => DgStockAnalytics_PreviewMouseLeftButtonUp(sender, e);

        private void DgStockMonitoring_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<Button>(dep) != null) return;
                if (FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) != null) return;
            }

            if (dgStockMonitoring?.SelectedItem is ProductItem item)
            {
                OpenProductDetailsModal(item);
            }
        }

        private void DgStockMonitoring_MouseDoubleClick(object sender, MouseButtonEventArgs e) => DgStockMonitoring_PreviewMouseLeftButtonUp(sender, e);

        // =========================================================================
        // ── PRODUCT 360° SALES & STOCK HISTORY MODAL ─────────────────────────────
        // =========================================================================
        private void OpenProductSalesHistoryModal(ProductItem item)
        {
            if (item == null) return;

            _activeProductHistory = InventoryService.GetProductSalesHistory(item.ProductID);
            if (_activeProductHistory == null)
            {
                MessageBox.Show("Could not load sales history for this product.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (lblHistoryProdTitle != null) lblHistoryProdTitle.Text = $"{item.ProductName} ({item.SKU})";
            if (lblHistCurrentStock != null) lblHistCurrentStock.Text = $"{_activeProductHistory.CurrentStock:N0} units";
            if (lblHistUnitsSold != null) lblHistUnitsSold.Text = $"{_activeProductHistory.TotalUnitsSold:N0} units";
            string role = SessionHelper.CurrentUser?.Role ?? "Admin";
            bool isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                           role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                           role.Equals("Manager", StringComparison.OrdinalIgnoreCase);
            if (lblHistRevenue != null) lblHistRevenue.Text = $"₱{_activeProductHistory.TotalRevenue:N2}";
            // 🛡️ Mask Profit & Hide Profit Column for Clerk
            if (isAdmin)
            {
                if (lblHistProfit != null) lblHistProfit.Text = $"₱{_activeProductHistory.TotalProfit:N2} ({_activeProductHistory.MarginPercent:F1}%)";
                if (colHistProfit != null) colHistProfit.Visibility = Visibility.Visible;
            }
            else
            {
                if (lblHistProfit != null) lblHistProfit.Text = "•••• (Admin Only)";
                if (colHistProfit != null) colHistProfit.Visibility = Visibility.Collapsed;
            }

            _allHistoryEntries = _activeProductHistory.Entries ?? new List<ProductSalesHistoryEntry>();

            if (lblHistEntryCount != null) lblHistEntryCount.Text = $"📋 Transaction Log ({_allHistoryEntries.Count} sales entries)";
            if (txtSearchHistory != null) txtSearchHistory.Text = "";

            if (dgSalesHistory != null) dgSalesHistory.ItemsSource = _allHistoryEntries;
            if (modalSalesHistory != null) modalSalesHistory.Visibility = Visibility.Visible;
        }

        private void ApplyHistoryFilters()
        {
            if (_allHistoryEntries == null) return;

            string search = txtSearchHistory?.Text?.Trim().ToLower() ?? "";
            var filtered = _allHistoryEntries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(e =>
                    (e.ReferenceNo != null && e.ReferenceNo.ToLower().Contains(search)) ||
                    (e.CustomerDetails != null && e.CustomerDetails.ToLower().Contains(search)) ||
                    (e.CashierOrUser != null && e.CashierOrUser.ToLower().Contains(search)));
            }

            var list = filtered.OrderByDescending(e => e.TransactionDate).ToList();
            if (dgSalesHistory != null) dgSalesHistory.ItemsSource = list;
            if (lblHistEntryCount != null) lblHistEntryCount.Text = $"📋 Transaction Log ({list.Count} sales entries)";
        }

        private void BtnCloseSalesHistory_Click(object sender, RoutedEventArgs e)
        {
            if (modalSalesHistory != null) modalSalesHistory.Visibility = Visibility.Collapsed;
        }

        private void DgSalesHistory_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                // Ignore clicks on scrollbar
                if (FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) != null) return;
            }

            if (dgSalesHistory?.SelectedItem is ProductSalesHistoryEntry entry)
            {
                if (string.IsNullOrWhiteSpace(entry.ReferenceNo)) return;

                var adminWindow = Window.GetWindow(this) as AdminDashboardWindow;
                if (adminWindow != null)
                {
                    if (modalSalesHistory != null) modalSalesHistory.Visibility = Visibility.Collapsed;
                    if (modalProductDetails != null) modalProductDetails.Visibility = Visibility.Collapsed;

                    // Navigate to Sales & Job Orders and view this specific order
                    adminWindow.NavigateToSalesOrder(entry.ReferenceNo, entry.TransactionType);
                }
            }
        }
        private void BtnExportHistoryToExcel_Click(object sender, RoutedEventArgs e)
        {
            if (dgSalesHistory?.ItemsSource is not List<ProductSalesHistoryEntry> entries || entries.Count == 0)
            {
                MessageBox.Show("No transaction entries to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"ProductSalesHistory_{_activeProductHistory?.SKU}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string role = SessionHelper.CurrentUser?.Role ?? "Admin";
                    bool isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                                   role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                                   role.Equals("Manager", StringComparison.OrdinalIgnoreCase);

                    using (var sw = new StreamWriter(sfd.FileName, false, new System.Text.UTF8Encoding(true)))
                    {
                        sw.WriteLine("\"Date\",\"Ref Number\",\"Type\",\"Customer / Notes\",\"Qty Sold\",\"Unit Price\",\"Total Revenue\",\"Profit\",\"Staff\"");
                        foreach (var en in entries)
                        {
                            string profitExport = isAdmin ? en.GrossProfit.ToString("F2") : "\"Protected\"";
                            sw.WriteLine($"\"{en.FormattedDate}\",\"{en.ReferenceNo}\",\"{en.TransactionType}\",\"{en.CustomerDetails}\",{en.QuantitySold},{en.UnitPrice:F2},{en.TotalRevenue:F2},{profitExport},\"{en.CashierOrUser}\"");
                        }
                    }
                    MessageBox.Show("Sales history exported successfully.", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void BtnExportProductsExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_allProducts == null || _allProducts.Count == 0)
            {
                MessageBox.Show("No products available to export.", "Export Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|Excel Spreadsheet (*.csv)|*.csv",
                FileName = $"BikeKings_InventoryMasterlist_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var sw = new StreamWriter(sfd.FileName, false, new System.Text.UTF8Encoding(true)))
                    {
                        sw.WriteLine("\"SKU\",\"Product Name\",\"Category\",\"Brand\",\"Cost Price\",\"Selling Price\",\"Quantity In Stock\",\"Reorder Level\",\"Status\"");
                        foreach (var p in _allProducts)
                        {
                            sw.WriteLine($"\"{p.SKU}\",\"{p.ProductName}\",\"{p.CategoryName}\",\"{p.BrandName}\",{p.CostPrice:F2},{p.UnitPrice:F2},{p.Quantity},{p.ReorderLevel},\"{p.StockStatus}\"");
                        }
                    }
                    MessageBox.Show("Inventory Masterlist exported successfully.", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // =========================================================================
        // ── PRODUCT CREATE & EDIT MODAL ──────────────────────────────────────────
        // =========================================================================
        private void AutoUpdateProductSKU()
        {
            if (_editingProductId.HasValue || _isManualSku) return;

            string catName = (cmbProdCategory?.SelectedItem as CategoryModel)?.CategoryName;
            if (string.IsNullOrWhiteSpace(catName)) catName = cmbProdCategory?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(catName)) catName = "PRD";

            string brandName = (cmbProdBrand?.SelectedItem as BrandModel)?.BrandName;
            if (string.IsNullOrWhiteSpace(brandName)) brandName = cmbProdBrand?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(brandName)) brandName = "GEN";

            string prodName = txtProdName?.Text?.Trim() ?? "";

            string autoSku = InventoryService.GenerateSKU(catName, brandName, prodName);
            if (txtProdBarcode != null && txtProdBarcode.Text != autoSku)
            {
                txtProdBarcode.Text = autoSku;
            }
        }
        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            _editingProductId = null;
            _isManualSku = false;
            if (lblModalProductTitle != null) lblModalProductTitle.Text = "Add New Inventory Item";
            ClearProductModal();
            AutoUpdateProductSKU(); // Pre-fills SKU automatically on open!
            if (modalProduct != null) modalProduct.Visibility = Visibility.Visible;
        }

        private void BtnEditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is ProductItem item)
            {
                _editingProductId = item.ProductID;
                _isManualSku = true; // Preserve existing product's SKU
                if (lblModalProductTitle != null) lblModalProductTitle.Text = "Edit Product Details";
                txtProdBarcode.Text = item.SKU;
                txtProdName.Text = item.ProductName;
                cmbProdCategory.SelectedValue = item.CategoryID;
                cmbProdBrand.SelectedValue = item.BrandID;
                if (cmbProdSupplier != null)
                {
                    if (item.SupplierID.HasValue && item.SupplierID.Value > 0)
                        cmbProdSupplier.SelectedValue = item.SupplierID.Value;
                    else
                    {
                        cmbProdSupplier.SelectedIndex = -1;
                        cmbProdSupplier.Text = "";
                    }
                }
                txtProdCost.Text = item.CostPrice.ToString("F2");
                txtProdPrice.Text = item.UnitPrice.ToString("F2");
                txtProdStock.Text = item.StockQuantity.ToString();
                txtProdReorder.Text = item.ReorderLevel.ToString();
                txtProdImagePath.Text = item.ImagePath ?? "";
                UpdateProductImagePreview(item.ImagePath ?? "");
                // Lock initial stock editing when editing existing product
                txtProdStock.IsEnabled = false;
                ClearProductValidationErrors();
                if (modalProduct != null) modalProduct.Visibility = Visibility.Visible;
            }
        }


        private void BtnAutoGenerateSKU_Click(object sender, RoutedEventArgs e)
        {
            _isManualSku = false;
            AutoUpdateProductSKU();
        }

        private void BtnCancelProduct_Click(object sender, RoutedEventArgs e)
        {
            if (modalProduct != null) modalProduct.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveProduct_Click(object sender, RoutedEventArgs e)
        {
            ClearProductValidationErrors();
            bool isValid = true;
            string sku = txtProdBarcode?.Text?.Trim() ?? "";
            string name = txtProdName?.Text?.Trim() ?? "";
            // Smart resolution: handles both dropdown click and typed text
            int catId = 0;
            if (cmbProdCategory.SelectedValue is int cId) catId = cId;
            else if (!string.IsNullOrWhiteSpace(cmbProdCategory.Text) && cmbProdCategory.ItemsSource is IEnumerable<CategoryModel> cats)
            {
                var cMatch = cats.FirstOrDefault(c => string.Equals(c.CategoryName, cmbProdCategory.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (cMatch != null) catId = cMatch.CategoryID;
            }
            int brandId = 0;
            if (cmbProdBrand.SelectedValue is int bId) brandId = bId;
            else if (!string.IsNullOrWhiteSpace(cmbProdBrand.Text) && cmbProdBrand.ItemsSource is IEnumerable<BrandModel> brands)
            {
                var bMatch = brands.FirstOrDefault(b => string.Equals(b.BrandName, cmbProdBrand.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (bMatch != null) brandId = bMatch.BrandID;
            }

            int? supplierId = null;
            if (cmbProdSupplier?.SelectedValue is int sId && sId > 0) supplierId = sId;
            else if (!string.IsNullOrWhiteSpace(cmbProdSupplier?.Text) && cmbProdSupplier.ItemsSource is IEnumerable<SupplierModel> sups)
            {
                var sMatch = sups.FirstOrDefault(s => string.Equals(s.SupplierName, cmbProdSupplier.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (sMatch != null) supplierId = sMatch.SupplierID;
            }
            // 1. SKU Format Validation
            if (string.IsNullOrWhiteSpace(sku) || !_skuRegex.IsMatch(sku))
            {
                ShowError(errProdBarcode, bdrProdBarcode, "Valid alphanumeric SKU is required (2-50 chars).");
                isValid = false;
            }
            // 🛡️ 2. SKU Duplicate Check (Handles both Add & Edit)
            else if (_allProducts != null)
            {
                bool isDuplicateSku = _editingProductId.HasValue
                    ? _allProducts.Any(p => p.ProductID != _editingProductId.Value && string.Equals(p.SKU, sku, StringComparison.OrdinalIgnoreCase))
                    : _allProducts.Any(p => string.Equals(p.SKU, sku, StringComparison.OrdinalIgnoreCase));
                if (isDuplicateSku)
                {
                    ShowError(errProdBarcode, bdrProdBarcode, "A product with this SKU / Barcode already exists.");
                    isValid = false;
                }
            }
            // 3. Name Format Validation
            if (string.IsNullOrWhiteSpace(name) || !_nameRegex.IsMatch(name))
            {
                ShowError(errProdName, bdrProdName, "Valid product name is required (2-200 chars).");
                isValid = false;
            }
            // 🛡️ 4. Product Name + Brand Duplicate Check
            else if (_allProducts != null && brandId > 0)
            {
                bool isDuplicateName = _editingProductId.HasValue
                    ? _allProducts.Any(p => p.ProductID != _editingProductId.Value && p.BrandID == brandId && string.Equals(p.ProductName, name, StringComparison.OrdinalIgnoreCase))
                    : _allProducts.Any(p => p.BrandID == brandId && string.Equals(p.ProductName, name, StringComparison.OrdinalIgnoreCase));
                if (isDuplicateName)
                {
                    ShowError(errProdName, bdrProdName, "A product with this exact name and brand already exists.");
                    isValid = false;
                }
            }
            if (catId <= 0)
            {
                ShowError(errProdCategory, bdrProdCategory, "Please select or type a valid registered category.");
                isValid = false;
            }
            if (brandId <= 0)
            {
                ShowError(errProdBrand, bdrProdBrand, "Please select or type a valid registered brand.");
                isValid = false;
            }
            if (!decimal.TryParse(txtProdCost?.Text?.Trim(), out decimal cost) || cost < 0)
            {
                ShowError(errProdCost, bdrProdCost, "Enter a valid positive cost price.");
                isValid = false;
            }
            if (!decimal.TryParse(txtProdPrice?.Text?.Trim(), out decimal price) || price < cost)
            {
                ShowError(errProdPrice, bdrProdPrice, price < cost ? "Selling price cannot be lower than cost price." : "Enter a valid selling price.");
                isValid = false;
            }
            if (!_editingProductId.HasValue)
            {
                if (!int.TryParse(txtProdStock?.Text?.Trim(), out int stock) || stock < 0)
                {
                    ShowError(errProdStock, bdrProdStock, "Enter a valid initial stock quantity (0 or greater).");
                    isValid = false;
                }
            }
            if (!int.TryParse(txtProdReorder?.Text?.Trim(), out int reorder) || reorder < 0)
            {
                ShowError(errProdReorder, bdrProdReorder, "Enter a valid reorder level (0 or greater).");
                isValid = false;
            }
            if (!isValid) return;
            int initialStock = _editingProductId.HasValue ? 0 : int.Parse(txtProdStock.Text.Trim());
            string errorMsg;
            bool success;
            string imagePath = txtProdImagePath?.Text?.Trim() ?? "";

            if (_editingProductId.HasValue)
            {
                success = InventoryService.UpdateProduct(_editingProductId.Value, sku, name, catId, brandId, cost, price, initialStock, reorder, out errorMsg, supplierId, imagePath);
            }
            else
            {
                success = InventoryService.AddProduct(sku, name, catId, brandId, cost, price, initialStock, reorder, out errorMsg, supplierId, imagePath);
            }
            if (success)
            {
                MessageBox.Show(_editingProductId.HasValue ? "Product updated successfully." : "Product created successfully.", "Inventory Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                if (modalProduct != null) modalProduct.Visibility = Visibility.Collapsed;
                LoadProducts();
            }
            else
            {
                MessageBox.Show($"Failed to save product: {errorMsg}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBrowseProductImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Select Product Photo",
                    Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All Files (*.*)|*.*"
                };

                if (ofd.ShowDialog() == true)
                {
                    string sourcePath = ofd.FileName;
                    string appImagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Products");
                    Directory.CreateDirectory(appImagesDir);

                    string safeSku = !string.IsNullOrWhiteSpace(txtProdBarcode?.Text)
                        ? Regex.Replace(txtProdBarcode.Text.Trim(), @"[^\w\-]", "_")
                        : "prod";
                    string destFileName = $"{safeSku}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
                    string destFilePath = Path.Combine(appImagesDir, destFileName);

                    // ⚡ PERFORMANCE OPTIMIZATION: Auto-resize large photos to max 400x400 (~50KB)
                    // Prevents high RAM usage and eliminates scrolling lag
                    try
                    {
                        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                            new Uri(sourcePath),
                            System.Windows.Media.Imaging.BitmapCreateOptions.None,
                            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

                        var frame = decoder.Frames[0];
                        double maxDim = 400.0;
                        double scale = Math.Min(maxDim / frame.PixelWidth, maxDim / frame.PixelHeight);
                        if (scale > 1.0) scale = 1.0; // Don't upscale small images

                        var resized = new System.Windows.Media.Imaging.TransformedBitmap(
                            frame,
                            new System.Windows.Media.ScaleTransform(scale, scale));

                        var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 85 };
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(resized));

                        using (var stream = new FileStream(destFilePath, FileMode.Create))
                        {
                            encoder.Save(stream);
                        }
                    }
                    catch
                    {
                        // Fallback to standard copy if format cannot be encoded
                        File.Copy(sourcePath, destFilePath, true);
                    }

                    txtProdImagePath.Text = destFilePath;
                    UpdateProductImagePreview(destFilePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load selected image: {ex.Message}", "Image Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnClearProductImage_Click(object sender, RoutedEventArgs e)
        {
            if (txtProdImagePath != null) txtProdImagePath.Text = "";
            UpdateProductImagePreview("");
        }

        private void TxtProdImagePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateProductImagePreview(txtProdImagePath?.Text?.Trim() ?? "");
        }

        private void UpdateProductImagePreview(string imagePath)
        {
            if (imgProdPreview == null) return;

            try
            {
                string resolved = ProductImageHelper.ResolveImagePath(
                    imagePath,
                    txtProdName?.Text?.Trim() ?? "",
                    (cmbProdCategory?.SelectedItem as CategoryModel)?.CategoryName ?? ""
                );

                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(resolved, UriKind.RelativeOrAbsolute);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    imgProdPreview.Source = bmp;
                }
                else
                {
                    imgProdPreview.Source = null;
                }

                if (lblProdImageStatus != null)
                {
                    if (string.IsNullOrWhiteSpace(imagePath))
                    {
                        lblProdImageStatus.Text = "Using default placeholder";
                        lblProdImageStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C95AD"));
                    }
                    else
                    {
                        lblProdImageStatus.Text = "✓ Custom image attached";
                        lblProdImageStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                    }
                }
            }
            catch
            {
                imgProdPreview.Source = null;
                if (lblProdImageStatus != null)
                {
                    lblProdImageStatus.Text = "Using default placeholder";
                    lblProdImageStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C95AD"));
                }
            }
        }

        private void BtnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is ProductItem item)
            {
                var res = MessageBox.Show($"Are you sure you want to deactivate product '{item.ProductName}' ({item.SKU})?\r\n\r\nThis will safely remove it from active POS sales and stock lists.", "Confirm Deactivation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    if (InventoryService.DeleteProduct(item.ProductID, out string err))
                    {
                        MessageBox.Show("Product deactivated successfully.", "Product Status", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadProducts();
                    }
                    else
                    {
                        MessageBox.Show($"Could not deactivate product: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ClearProductModal()
        {
            txtProdBarcode.Text = "";
            txtProdName.Text = "";
            cmbProdCategory.SelectedIndex = -1;
            cmbProdBrand.SelectedIndex = -1;
            if (cmbProdSupplier != null)
            {
                cmbProdSupplier.SelectedIndex = -1;
                cmbProdSupplier.Text = "";
            }
            if (txtProdImagePath != null) txtProdImagePath.Text = "";
            UpdateProductImagePreview("");
            txtProdCost.Text = "0.00";
            txtProdPrice.Text = "0.00";
            txtProdStock.Text = "0";
            txtProdStock.IsEnabled = true;
            txtProdReorder.Text = "5";
            ClearProductValidationErrors();
        }

        private void ClearProductValidationErrors()
        {
            HideError(errProdBarcode, bdrProdBarcode);
            HideError(errProdName, bdrProdName);
            HideError(errProdCategory, bdrProdCategory);
            HideError(errProdBrand, bdrProdBrand);
            HideError(errProdCost, bdrProdCost);
            HideError(errProdPrice, bdrProdPrice);
            HideError(errProdStock, bdrProdStock);
            HideError(errProdReorder, bdrProdReorder);
        }

        private void ShowError(TextBlock tb, Border bdr, string msg)
        {
            if (tb != null)
            {
                tb.Text = msg;
                tb.Visibility = Visibility.Visible;
            }
            if (bdr != null)
            {
                bdr.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#EF4444");
            }
        }

        private void HideError(TextBlock tb, Border bdr)
        {
            if (tb != null) tb.Visibility = Visibility.Collapsed;
            if (bdr != null) bdr.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#251F30");
        }

        // =========================================================================
        // ── STOCK IN MODAL ───────────────────────────────────────────────────────
        // =========================================================================
        private void BtnOpenStockIn_Click(object sender, RoutedEventArgs e)
        {
            cmbStockInProduct.SelectedIndex = -1;
            cmbStockInSupplier.SelectedIndex = -1;
            if (lblStockInCurrentQty != null) lblStockInCurrentQty.Text = "0 units";
            txtStockInQty.Text = "1";
            txtStockInRef.Text = $"IN-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
            txtStockInNotes.Text = "";
            if (modalStockIn != null) modalStockIn.Visibility = Visibility.Visible;
        }

        private void CmbStockInProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ProductItem p = cmbStockInProduct.SelectedItem as ProductItem;
            if (p == null && !string.IsNullOrWhiteSpace(cmbStockInProduct.Text) && _allProducts != null)
            {
                p = _allProducts.FirstOrDefault(x => string.Equals(x.ProductName, cmbStockInProduct.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(x.SKU, cmbStockInProduct.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (lblStockInCurrentQty != null)
            {
                lblStockInCurrentQty.Text = p != null ? $"{p.StockQuantity} units" : "0 units";
            }
        }

        private void BtnCancelStockIn_Click(object sender, RoutedEventArgs e)
        {
            if (modalStockIn != null) modalStockIn.Visibility = Visibility.Collapsed;
        }

        private void BtnConfirmStockIn_Click(object sender, RoutedEventArgs e)
        {
            int prodId = 0;
            if (cmbStockInProduct.SelectedValue is int pId) prodId = pId;
            else if (!string.IsNullOrWhiteSpace(cmbStockInProduct.Text) && _allProducts != null)
            {
                var match = _allProducts.FirstOrDefault(p =>
                    string.Equals(p.ProductName, cmbStockInProduct.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.SKU, cmbStockInProduct.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null) prodId = match.ProductID;
            }
            if (prodId <= 0)
            {
                MessageBox.Show("Please select a valid product to restock.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtStockInQty.Text.Trim(), out int qty) || qty <= 0)
            {
                MessageBox.Show("Enter a valid quantity greater than 0.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int? supId = null;
            if (cmbStockInSupplier.SelectedValue is int sId) supId = sId;
            else if (!string.IsNullOrWhiteSpace(cmbStockInSupplier.Text) && cmbStockInSupplier.ItemsSource is IEnumerable<SupplierModel> sups)
            {
                var sMatch = sups.FirstOrDefault(s => string.Equals(s.SupplierName, cmbStockInSupplier.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(s.DisplayName, cmbStockInSupplier.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (sMatch != null) supId = sMatch.SupplierID;
            }
            string refNum = txtStockInRef.Text?.Trim();
            if (string.IsNullOrWhiteSpace(refNum)) refNum = $"IN-{DateTime.Now:yyyyMMdd-HHmmss}";
            string notes = txtStockInNotes.Text?.Trim() ?? "Manual Stock-In";
            string recordedBy = SessionHelper.CurrentUser?.FullName ?? "Store Clerk";
            if (InventoryService.RecordStockIn(prodId, qty, refNum, supId, notes, recordedBy, out string err))
            {
                MessageBox.Show($"Successfully restocked {qty} units.", "Stock In Success", MessageBoxButton.OK, MessageBoxImage.Information);
                if (modalStockIn != null) modalStockIn.Visibility = Visibility.Collapsed;
                LoadInitialData();
            }
            else
            {
                MessageBox.Show($"Failed to record stock in: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================================
        // ── STOCK OUT MODAL ──────────────────────────────────────────────────────
        // =========================================================================
        private void BtnOpenStockOut_Click(object sender, RoutedEventArgs e)
        {
            cmbStockOutProduct.SelectedIndex = -1;
            if (lblStockOutCurrentQty != null) lblStockOutCurrentQty.Text = "0 units";
            txtStockOutQty.Text = "1";
            txtStockOutNotes.Text = "";
            if (modalStockOut != null) modalStockOut.Visibility = Visibility.Visible;
        }

        private void CmbStockOutProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ProductItem p = cmbStockOutProduct.SelectedItem as ProductItem;
            if (p == null && !string.IsNullOrWhiteSpace(cmbStockOutProduct.Text) && _allProducts != null)
            {
                p = _allProducts.FirstOrDefault(x => string.Equals(x.ProductName, cmbStockOutProduct.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(x.SKU, cmbStockOutProduct.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (lblStockOutCurrentQty != null)
            {
                lblStockOutCurrentQty.Text = p != null ? $"{p.StockQuantity} units" : "0 units";
            }
        }

        private void BtnCancelStockOut_Click(object sender, RoutedEventArgs e)
        {
            if (modalStockOut != null) modalStockOut.Visibility = Visibility.Collapsed;
        }

        private void BtnConfirmStockOut_Click(object sender, RoutedEventArgs e)
        {
            int prodId = 0;
            if (cmbStockOutProduct.SelectedValue is int pId) prodId = pId;
            else if (!string.IsNullOrWhiteSpace(cmbStockOutProduct.Text) && _allProducts != null)
            {
                var match = _allProducts.FirstOrDefault(p =>
                    string.Equals(p.ProductName, cmbStockOutProduct.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.SKU, cmbStockOutProduct.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null) prodId = match.ProductID;
            }
            if (prodId <= 0)
            {
                MessageBox.Show("Please select a valid product to deduct.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtStockOutQty.Text.Trim(), out int qty) || qty <= 0)
            {
                MessageBox.Show("Enter a valid quantity greater than 0.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var currentProd = _allProducts?.FirstOrDefault(p => p.ProductID == prodId);
            if (currentProd != null && qty > currentProd.StockQuantity)
            {
                MessageBox.Show($"Cannot deduct {qty} units. Current stock is only {currentProd.StockQuantity}.", "Insufficient Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string reason = (cmbStockOutReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? cmbStockOutReason.Text ?? "Inventory Audit Adjustment";
            string notes = txtStockOutNotes.Text?.Trim() ?? "Manual Stock-Out";
            string recordedBy = SessionHelper.CurrentUser?.FullName ?? "Store Clerk";
            if (InventoryService.RecordStockOut(prodId, qty, reason, notes, recordedBy, out string err))
            {
                MessageBox.Show($"Successfully deducted {qty} units.", "Stock Out Success", MessageBoxButton.OK, MessageBoxImage.Information);
                if (modalStockOut != null) modalStockOut.Visibility = Visibility.Collapsed;
                LoadInitialData();
            }
            else
            {
                MessageBox.Show($"Failed to record stock out: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================================
        // ── PURCHASE ORDER DRAFTING & SUBMISSION ──────────────────────────────────
        // =========================================================================
        private void BtnOpenCreatePO_Click(object sender, RoutedEventArgs e)
        {
            _createPOLineItems.Clear();
            cmbPOSupplier.SelectedIndex = -1;
            if (cmbPOProduct != null)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(cmbPOProduct.ItemsSource);
                if (view != null) view.Filter = null;
                cmbPOProduct.SelectedIndex = -1;
                cmbPOProduct.Text = "";
            }
            txtPOQty.Text = "1";
            txtPOUnitPrice.Text = "0.00";
            if (cmbPOTerms != null) cmbPOTerms.SelectedIndex = 1; // Default to Net 30
            txtPONotes.Text = "";
            RefreshCreatePOGrid();
            if (modalPO != null) modalPO.Visibility = Visibility.Visible;
        }

        private void CmbPOProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPOProduct.SelectedItem is ProductItem p)
            {
                txtPOUnitPrice.Text = p.CostPrice.ToString("F2");
            }
        }

        private void BtnPOAddItem_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPOProduct.SelectedItem is not ProductItem prod)
            {
                MessageBox.Show("Please select a product first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtPOQty.Text.Trim(), out int qty) || qty <= 0)
            {
                MessageBox.Show("Enter a valid quantity greater than 0.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtPOUnitPrice.Text.Trim(), out decimal cost) || cost < 0)
            {
                MessageBox.Show("Enter a valid unit cost.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = _createPOLineItems.FirstOrDefault(x => x.ProductID == prod.ProductID);
            if (existing != null)
            {
                existing.Quantity += qty;
                existing.UnitPrice = cost;
                existing.TotalPrice = existing.Quantity * existing.UnitPrice;
            }
            else
            {
                _createPOLineItems.Add(new POLineItem
                {
                    ProductID = prod.ProductID,
                    ItemDescription = $"{prod.ProductName} ({prod.SKU})",
                    Unit = "pcs",
                    Quantity = qty,
                    UnitPrice = cost,
                    TotalPrice = qty * cost
                });
            }

            RefreshCreatePOGrid();
        }

        private void BtnPORemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is POLineItem item)
            {
                _createPOLineItems.Remove(item);
                RefreshCreatePOGrid();
            }
        }

        private void TxtPOCreateLineQty_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is POLineItem item)
            {
                if (int.TryParse(tb.Text.Trim(), out int newQty) && newQty >= 0)
                {
                    item.Quantity = newQty;
                }
                decimal total = _createPOLineItems.Sum(x => x.TotalPrice);
                if (lblPOCreateGrandTotal != null) lblPOCreateGrandTotal.Text = $"₱{total:N2}";
            }
        }

        private void TxtPOCreateLineQty_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is POLineItem item)
            {
                if (!int.TryParse(tb.Text.Trim(), out int newQty) || newQty <= 0)
                {
                    item.Quantity = 1;
                    tb.Text = "1";
                }
                decimal total = _createPOLineItems.Sum(x => x.TotalPrice);
                if (lblPOCreateGrandTotal != null) lblPOCreateGrandTotal.Text = $"₱{total:N2}";
            }
        }

        private void RefreshCreatePOGrid()
        {
            if (dgPOCreateItems != null)
            {
                dgPOCreateItems.ItemsSource = null;
                dgPOCreateItems.ItemsSource = _createPOLineItems;
            }
            decimal total = _createPOLineItems.Sum(x => x.TotalPrice);
            if (lblPOCreateGrandTotal != null) lblPOCreateGrandTotal.Text = $"₱{total:N2}";
        }

        private void BtnCancelPO_Click(object sender, RoutedEventArgs e)
        {
            if (modalPO != null) modalPO.Visibility = Visibility.Collapsed;
        }

        private void BtnConfirmPO_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPOSupplier.SelectedValue == null)
            {
                MessageBox.Show("Select a supplier for this PO.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_createPOLineItems.Count == 0)
            {
                MessageBox.Show("Add at least one line item to the PO.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int supId = Convert.ToInt32(cmbPOSupplier.SelectedValue);
            string requester = SessionHelper.CurrentUser?.FullName ?? "Store Clerk";
            string selectedTerms = (cmbPOTerms?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Net 30";
            if (InventoryService.CreatePurchaseOrder(supId, null, requester, selectedTerms, "Standard Delivery", txtPONotes?.Text?.Trim() ?? "", _createPOLineItems, out string poNum, out string err))
            {
                MessageBox.Show($"Purchase Order '{poNum}' submitted successfully for Administrator approval.", "PO Submitted", MessageBoxButton.OK, MessageBoxImage.Information);
                if (modalPO != null) modalPO.Visibility = Visibility.Collapsed;
                LoadPurchaseOrders();
            }
            else
            {
                MessageBox.Show($"Failed to submit PO: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================================
        // ── PURCHASE ORDER OFFICIAL DOCUMENT & ACTIONS ───────────────────────────
        // =========================================================================
        private void BtnViewPO_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is PurchaseOrderItem po)
            {
                _selectedPO = po;
                DisplayPODocument(po);
            }
        }

        private void DgPurchaseOrders_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // If the user clicked an action button (Approve or Reject), let that button handle it
            DependencyObject obj = e.OriginalSource as DependencyObject;
            while (obj != null && obj != sender)
            {
                if (obj is Button) return;
                obj = VisualTreeHelper.GetParent(obj);
            }

            if (sender is DataGrid grid && grid.SelectedItem is PurchaseOrderItem po)
            {
                _selectedPO = po;
                DisplayPODocument(po);
            }
        }

        private void DgPurchaseOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e) => DgPurchaseOrders_PreviewMouseLeftButtonUp(sender, e);

        private void DisplayPODocument(PurchaseOrderItem po)
        {
            if (po == null) return;

            if (docPONumber != null) docPONumber.Text = po.PONumber;
            if (docPODate != null) docPODate.Text = po.PODate.ToString("yyyy-MM-dd");
            if (docPOStatus != null)
            {
                docPOStatus.Text = (po.Status ?? "DRAFT").ToUpper();
                docPOStatus.Foreground = (Brush)new BrushConverter().ConvertFrom(po.StatusColor);
            }

            if (docVendorName != null) docVendorName.Text = po.SupplierName;
            if (docVendorContact != null) docVendorContact.Text = $"Attn: {po.SupplierContact}";
            if (docVendorAddress != null) docVendorAddress.Text = string.IsNullOrWhiteSpace(po.SupplierAddress) ? "Metro Manila, PH" : po.SupplierAddress;
            if (docVendorPhone != null) docVendorPhone.Text = $"Phone: {po.SupplierPhone}";
            if (docVendorEmail != null) docVendorEmail.Text = po.SupplierEmail;

            if (docRequester != null) docRequester.Text = po.RequesterName;
            if (docTerms != null) docTerms.Text = po.Terms;
            if (docNotes != null) docNotes.Text = po.Notes;

            var items = po.Items ?? new List<POLineItem>();
            if (icDocItems != null) icDocItems.ItemsSource = items;

            decimal total = items.Sum(i => i.TotalPrice);
            if (docSubTotal != null) docSubTotal.Text = $"Sub Total: ₱{total:N2}";
            if (docGrandTotal != null) docGrandTotal.Text = $"TOTAL: ₱{total:N2}";

            ApplyRoleRestrictions();
            if (modalPODetails != null) modalPODetails.Visibility = Visibility.Visible;
        }

        private void BtnClosePODetails_Click(object sender, RoutedEventArgs e)
        {
            if (modalPODetails != null) modalPODetails.Visibility = Visibility.Collapsed;
        }

        private void BtnPrintPO_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    pd.PrintVisual(pnlPrintablePODocument, $"PurchaseOrder_{_selectedPO?.PONumber}");
                    MessageBox.Show("Purchase order document sent to printer.", "Print Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnModalSendEmail_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPO == null) return;
            string subject = Uri.EscapeDataString($"Purchase Order {_selectedPO.PONumber} - Bike Kings Cycle & Motor");
            string body = Uri.EscapeDataString($"Dear {_selectedPO.SupplierName},\n\nPlease find our official purchase order {_selectedPO.PONumber} totaling ₱{_selectedPO.TotalAmount:N2}.\n\nThank you,\nBike Kings Cycle & Motor Repair");
            string mailto = $"mailto:{_selectedPO.SupplierEmail}?subject={subject}&body={body}";

            try
            {
                Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show($"Default email client could not open. Supplier Email: {_selectedPO.SupplierEmail}", "Email Client Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // =========================================================================
        // ── SMART SUPPLIER RECOMMENDATION HELPER ─────────────────────────────────
        // =========================================================================
        // =========================================================================
        // ── SMART SUPPLIER RECOMMENDATION HELPER ─────────────────────────────────
        // =========================================================================
        private List<SupplierModel> GetSuppliersWithRecommendations(List<POLineItem> currentItems = null)
        {
            var baseSuppliers = InventoryService.GetSuppliers() ?? new List<SupplierModel>();
            var allPos = InventoryService.GetPurchaseOrders() ?? new List<PurchaseOrderItem>();

            // Collect target product IDs from current line items (or active details modal)
            var prodIds = currentItems?.Where(x => x.ProductID.HasValue).Select(x => x.ProductID.Value).ToHashSet()
                          ?? new HashSet<int>();

            if (prodIds.Count == 0 && _selectedProductForDetails != null)
            {
                prodIds.Add(_selectedProductForDetails.ProductID);
            }

            var allProdsList = _allProducts != null && _allProducts.Count > 0
                ? _allProducts
                : (InventoryService.GetProducts() ?? new List<ProductItem>());

            var targetProducts = allProdsList.Where(p => prodIds.Contains(p.ProductID)).ToList();
            if (targetProducts.Count == 0 && _selectedProductForDetails != null)
            {
                targetProducts.Add(_selectedProductForDetails);
            }

            var brandNames = targetProducts.Select(p => p.BrandName.Trim().ToLower()).Where(s => !string.IsNullOrEmpty(s)).ToHashSet();

            // Track supplier scores and recommendation reasons
            var supplierScores = new Dictionary<int, (int score, string reason)>();

            foreach (var s in baseSuppliers)
            {
                int score = 0;
                string reason = "";
                string sName = s.SupplierName.ToLower();

                // ── 0. DIRECT ASSIGNED SUPPLIER: Supplier explicitly selected for this product ──
                if (targetProducts.Any(p => p.SupplierID.HasValue && p.SupplierID.Value == s.SupplierID))
                {
                    score = 200000;
                    reason = "Assigned Catalog Supplier";
                }

                // ── 1. PRIMARY: Quantity of THIS EXACT PRODUCT previously ordered from this supplier ──
                int productQty = 0;
                int poCount = 0;
                foreach (var po in allPos.Where(p => p.SupplierID == s.SupplierID))
                {
                    if (po.Items != null)
                    {
                        var matchingLines = po.Items.Where(i => i.ProductID.HasValue && prodIds.Contains(i.ProductID.Value)).ToList();
                        if (matchingLines.Count > 0)
                        {
                            productQty += matchingLines.Sum(i => i.Quantity);
                            poCount++;
                        }
                    }
                }

                if (productQty > 0)
                {
                    // Highest priority: Exact product order volume
                    score = 100000 + (productQty * 10);
                    reason = $"Most Ordered: {productQty} pcs ({poCount} POs)";
                }

                // ── 2. SECONDARY: Quantity ordered for the EXACT SAME BRAND (Category grouping removed) ──
                if (score == 0 && brandNames.Count > 0)
                {
                    int brandQty = 0;
                    int brandPoCount = 0;
                    foreach (var po in allPos.Where(p => p.SupplierID == s.SupplierID))
                    {
                        if (po.Items != null)
                        {
                            foreach (var it in po.Items)
                            {
                                if (it.ProductID.HasValue)
                                {
                                    var prod = allProdsList.FirstOrDefault(p => p.ProductID == it.ProductID.Value);
                                    if (prod != null && brandNames.Contains(prod.BrandName.Trim().ToLower()))
                                    {
                                        brandQty += it.Quantity;
                                        brandPoCount++;
                                    }
                                }
                            }
                        }
                    }

                    if (brandQty > 0)
                    {
                        score = 50000 + brandQty;
                        reason = $"Top Brand Supplier: {brandQty} pcs";
                    }
                }

                // ── 3. TERTIARY: Catalog & Brand Distributor Mapping (When no order history exists) ──
                if (score == 0)
                {
                    bool isMotul = brandNames.Any(b => b.Contains("motul"));
                    bool isYamaha = brandNames.Any(b => b.Contains("yamaha") || b.Contains("yamalube"));
                    bool isBattery = brandNames.Any(b => b.Contains("yuasa") || b.Contains("outdo") || b.Contains("koso"));
                    bool isParts = brandNames.Any(b => b.Contains("ngk") || b.Contains("bando") || b.Contains("brembo") || b.Contains("rcb") || b.Contains("jvt") || b.Contains("uma") || b.Contains("maxxis") || b.Contains("fdr") || b.Contains("quick"));

                    if (isYamaha && sName.Contains("yamaha"))
                    {
                        score = 20000;
                        reason = "Official Yamaha / Yamalube Specialist";
                    }
                    else if (isMotul && sName.Contains("metro"))
                    {
                        score = 20000;
                        reason = "Authorized Motul Distributor";
                    }
                    else if (isMotul && sName.Contains("speedtech"))
                    {
                        score = 15000;
                        reason = "Lubricants & Racing Specialist";
                    }
                    else if (isBattery && sName.Contains("power"))
                    {
                        score = 20000;
                        reason = "Wholesale Battery & Electrical Vendor";
                    }
                    else if (isParts && sName.Contains("metro"))
                    {
                        score = 20000;
                        reason = "Authorized Parts Distributor";
                    }
                    else if (sName.Contains("parts") || sName.Contains("distributor"))
                    {
                        score = 5000;
                        reason = "General Parts Distributor";
                    }
                }

                supplierScores[s.SupplierID] = (score, reason);
            }

            // Find top score among all suppliers
            int topScore = supplierScores.Values.Select(v => v.score).DefaultIfEmpty(0).Max();

            var result = new List<SupplierModel>();
            foreach (var s in baseSuppliers)
            {
                var (score, reason) = supplierScores.ContainsKey(s.SupplierID) ? supplierScores[s.SupplierID] : (0, "");
                // Only mark as recommended if it actually scored points (never default blindly)
                bool isTop = score > 0 && score == topScore;

                var clone = new SupplierModel
                {
                    SupplierID = s.SupplierID,
                    SupplierName = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    Phone = s.Phone,
                    Email = s.Email,
                    Address = s.Address,
                    IsActive = s.IsActive,
                    IsRecommended = isTop,
                    RecommendationTag = isTop ? reason : ""
                };
                result.Add(clone);
            }

            // Recommended suppliers automatically float to the top of the dropdown!
            return result.OrderByDescending(s => s.IsRecommended).ThenBy(s => s.SupplierName).ToList();
        }

        // =========================================================================
        // ── PO APPROVAL & REJECTION (ADMINISTRATOR POWERS) ────────────────────────
        // =========================================================================
        private void BtnQuickApprovePO_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is PurchaseOrderItem po)
            {
                var res = MessageBox.Show($"Approve Purchase Order '{po.PONumber}' for {po.SupplierName} (₱{po.TotalAmount:N2})?", "Confirm Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    if (InventoryService.ApprovePurchaseOrder(po.POID, out string err))
                    {
                        MessageBox.Show($"Purchase Order '{po.PONumber}' approved.", "Approved", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadPurchaseOrders();
                    }
                    else
                    {
                        MessageBox.Show($"Failed to approve PO: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnQuickRejectPO_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is PurchaseOrderItem po)
            {
                var res = MessageBox.Show($"Reject Purchase Order '{po.PONumber}'?", "Confirm Rejection", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    if (InventoryService.RejectPurchaseOrder(po.POID, out string err))
                    {
                        MessageBox.Show($"Purchase Order '{po.PONumber}' rejected.", "Rejected", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadPurchaseOrders();
                    }
                    else
                    {
                        MessageBox.Show($"Failed to reject PO: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnModalApprovePO_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPO == null) return;
            var res = MessageBox.Show($"Approve Purchase Order '{_selectedPO.PONumber}'?", "Confirm Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                if (InventoryService.ApprovePurchaseOrder(_selectedPO.POID, out string err))
                {
                    MessageBox.Show("Purchase order approved successfully.", "Approved", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (modalPODetails != null) modalPODetails.Visibility = Visibility.Collapsed;
                    LoadPurchaseOrders();
                }
                else
                {
                    MessageBox.Show($"Failed to approve PO: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnModalRejectPO_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPO == null) return;
            var res = MessageBox.Show($"Reject Purchase Order '{_selectedPO.PONumber}'?", "Confirm Rejection", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                if (InventoryService.RejectPurchaseOrder(_selectedPO.POID, out string err))
                {
                    MessageBox.Show("Purchase order rejected.", "Rejected", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (modalPODetails != null) modalPODetails.Visibility = Visibility.Collapsed;
                    LoadPurchaseOrders();
                }
                else
                {
                    MessageBox.Show($"Failed to reject PO: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // =========================================================================
        // ── PO EDITING (BEFORE SENDING OUT) ──────────────────────────────────────
        // =========================================================================
        private void BtnModalEditPO_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPO == null) return;

            if (!_selectedPO.IsPending)
            {
                MessageBox.Show("Only pending purchase orders can be edited prior to approval.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (lblEditPOTitle != null) lblEditPOTitle.Text = $"Edit Purchase Order: {_selectedPO.PONumber}";
            txtEditPONotes.Text = _selectedPO.Notes;

            _editPOLineItems.Clear();
            if (_selectedPO.Items != null)
            {
                foreach (var it in _selectedPO.Items)
                {
                    _editPOLineItems.Add(new POLineItem
                    {
                        POItemID = it.POItemID,
                        POID = it.POID,
                        ProductID = it.ProductID,
                        ItemDescription = it.ItemDescription,
                        Unit = it.Unit,
                        Quantity = it.Quantity,
                        UnitPrice = it.UnitPrice,
                        TotalPrice = it.TotalPrice
                    });
                }
            }

            // ── Update Supplier List with Recommendations for these items ──
            if (cmbEditPOSupplier != null)
            {
                cmbEditPOSupplier.ItemsSource = GetSuppliersWithRecommendations(_editPOLineItems);
                cmbEditPOSupplier.DisplayMemberPath = "DisplayName";
                cmbEditPOSupplier.SelectedValuePath = "SupplierID";
                cmbEditPOSupplier.SelectedValue = _selectedPO.SupplierID;
            }

            if (cmbEditPOProduct != null)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(cmbEditPOProduct.ItemsSource);
                if (view != null) view.Filter = null;
                cmbEditPOProduct.SelectedIndex = -1;
                cmbEditPOProduct.Text = "";
            }

            RefreshEditPOGrid();
            if (modalEditPO != null) modalEditPO.Visibility = Visibility.Visible;
        }

        private void CmbEditPOProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbEditPOProduct.SelectedItem is ProductItem p)
            {
                txtEditPOUnitPrice.Text = p.CostPrice.ToString("F2");
            }
        }

        private void BtnEditPOAddItem_Click(object sender, RoutedEventArgs e)
        {
            if (cmbEditPOProduct.SelectedItem is not ProductItem prod)
            {
                MessageBox.Show("Select a product to add.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtEditPOQty.Text.Trim(), out int qty) || qty <= 0)
            {
                MessageBox.Show("Enter a valid quantity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtEditPOUnitPrice.Text.Trim(), out decimal cost) || cost < 0)
            {
                MessageBox.Show("Enter a valid unit cost.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = _editPOLineItems.FirstOrDefault(x => x.ProductID == prod.ProductID);
            if (existing != null)
            {
                existing.Quantity += qty;
                existing.UnitPrice = cost;
                existing.TotalPrice = existing.Quantity * existing.UnitPrice;
            }
            else
            {
                _editPOLineItems.Add(new POLineItem
                {
                    ProductID = prod.ProductID,
                    ItemDescription = $"{prod.ProductName} ({prod.SKU})",
                    Unit = "pcs",
                    Quantity = qty,
                    UnitPrice = cost,
                    TotalPrice = qty * cost
                });
            }

            RefreshEditPOGrid();
        }

        private void BtnEditPORemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is POLineItem item)
            {
                _editPOLineItems.Remove(item);
                RefreshEditPOGrid();
            }
        }

        private void DgEditPOItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            decimal total = _editPOLineItems.Sum(x => x.TotalPrice);
            if (lblEditPOGrandTotal != null) lblEditPOGrandTotal.Text = $"₱{total:N2}";
        }

        private void DgEditPOItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var item in _editPOLineItems)
                {
                    item.TotalPrice = item.Quantity * item.UnitPrice;
                }
                RefreshEditPOGrid();
            }));
        }

        private void RefreshEditPOGrid()
        {
            if (dgEditPOItems != null)
            {
                dgEditPOItems.ItemsSource = null;
                dgEditPOItems.ItemsSource = _editPOLineItems;
            }
            decimal total = _editPOLineItems.Sum(x => x.TotalPrice);
            if (lblEditPOGrandTotal != null) lblEditPOGrandTotal.Text = $"₱{total:N2}";
        }

        private void BtnCancelEditPO_Click(object sender, RoutedEventArgs e)
        {
            if (modalEditPO != null) modalEditPO.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveEditPO_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPO == null) return;
            if (cmbEditPOSupplier.SelectedValue == null)
            {
                MessageBox.Show("Select a supplier.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_editPOLineItems.Count == 0)
            {
                MessageBox.Show("Add at least one line item.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int supId = Convert.ToInt32(cmbEditPOSupplier.SelectedValue);

            if (InventoryService.UpdatePurchaseOrder(_selectedPO.POID, supId, _editPOLineItems, txtEditPONotes?.Text?.Trim() ?? "", out string err))
            {
                MessageBox.Show("Purchase Order updated successfully.", "Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                if (modalEditPO != null) modalEditPO.Visibility = Visibility.Collapsed;

                // Live update the displayed official document preview if open
                _selectedPO = InventoryService.GetPurchaseOrders()?.FirstOrDefault(x => x.POID == _selectedPO.POID);
                if (_selectedPO != null)
                {
                    DisplayPODocument(_selectedPO);
                }
                else
                {
                    if (modalPODetails != null) modalPODetails.Visibility = Visibility.Collapsed;
                }

                LoadPurchaseOrders();
            }
            else
            {
                MessageBox.Show($"Failed to update PO: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================================
        // ── PO DELIVERY RECEIVING (CLERK ONLY) ────────────────────────────────────
        // =========================================================================
        private void BtnModalReceivePO_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPO == null) return;
            var res = MessageBox.Show($"Confirm delivery reception for Purchase Order '{_selectedPO.PONumber}'?\r\n\r\nThis will automatically restock product inventory quantities.", "Confirm Delivery", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                if (InventoryService.ReceivePurchaseOrder(_selectedPO.POID, out string err))
                {
                    MessageBox.Show("Purchase Order received and stock levels updated.", "Delivery Processed", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (modalPODetails != null) modalPODetails.Visibility = Visibility.Collapsed;
                    LoadInitialData();
                }
                else
                {
                    MessageBox.Show($"Failed to receive PO: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // =========================================================================
        // ── NUMERIC / INPUT VALIDATION HELPERS ────────────────────────────────────
        // =========================================================================
        private void TxtNumeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _numFilter.IsMatch(e.Text);
        }

        private void TxtDecimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _numFilter.IsMatch(e.Text);
        }

        private void TxtInteger_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _intFilter.IsMatch(e.Text);
        }

        // =========================================================================
        // ── COMPATIBILITY & REAL-TIME INPUT EVENT HANDLERS ───────────────────────
        // =========================================================================
        private void DgProducts_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled && scrollViewerMain != null)
            {
                scrollViewerMain.ScrollToVerticalOffset(scrollViewerMain.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private void BtnOpenStockInModal_Click(object sender, RoutedEventArgs e) => BtnOpenStockIn_Click(sender, e);
        private void BtnOpenStockOutModal_Click(object sender, RoutedEventArgs e) => BtnOpenStockOut_Click(sender, e);
        private void BtnOpenCreatePOModal_Click(object sender, RoutedEventArgs e) => BtnOpenCreatePO_Click(sender, e);
        private void BtnJumpToPOTab_Click(object sender, RoutedEventArgs e) => ShowPurchaseOrdersTab();
        private void BtnSaveStockIn_Click(object sender, RoutedEventArgs e) => BtnConfirmStockIn_Click(sender, e);
        private void BtnSaveStockOut_Click(object sender, RoutedEventArgs e) => BtnConfirmStockOut_Click(sender, e);
        private void TxtSearchProducts_TextChanged(object sender, TextChangedEventArgs e) => ApplyProductFilters();
        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyProductFilters();
        private void DgProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e) => DgProducts_PreviewMouseLeftButtonUp(sender, e);
        private void TxtSearchHistory_TextChanged(object sender, TextChangedEventArgs e) => ApplyHistoryFilters();
        private void CmbProdCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (errProdCategory != null && errProdCategory.Visibility == Visibility.Visible)
                HideError(errProdCategory, bdrProdCategory);

            // ⚡ Auto-updates SKU live when category changes!
            AutoUpdateProductSKU();
        }

        private void CmbProdBrand_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (errProdBrand != null && errProdBrand.Visibility == Visibility.Visible)
                HideError(errProdBrand, bdrProdBrand);

            // ⚡ Auto-updates SKU live when brand changes!
            AutoUpdateProductSKU();
        }

        private void TxtProdName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (errProdName != null && errProdName.Visibility == Visibility.Visible && _nameRegex.IsMatch(txtProdName?.Text?.Trim() ?? ""))
                HideError(errProdName, bdrProdName);

            // ⚡ Auto-updates SKU live as you type product name!
            AutoUpdateProductSKU();
        }

        private void TxtProdCost_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (errProdCost != null && errProdCost.Visibility == Visibility.Visible && decimal.TryParse(txtProdCost?.Text?.Trim() ?? "", out decimal cost) && cost >= 0)
                HideError(errProdCost, bdrProdCost);
        }

        private void TxtProdPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (errProdPrice != null && errProdPrice.Visibility == Visibility.Visible && decimal.TryParse(txtProdPrice?.Text?.Trim() ?? "", out decimal price) && price >= 0)
                HideError(errProdPrice, bdrProdPrice);
        }

        private void TxtProdBarcode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (errProdBarcode != null && errProdBarcode.Visibility == Visibility.Visible && _skuRegex.IsMatch(txtProdBarcode?.Text?.Trim() ?? ""))
                HideError(errProdBarcode, bdrProdBarcode);

            // If the user erased the barcode, unlock manual mode and regenerate automatically
            if (string.IsNullOrWhiteSpace(txtProdBarcode?.Text))
            {
                _isManualSku = false;
                AutoUpdateProductSKU();
            }
        }

        private void TxtProdStock_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (errProdStock != null && errProdStock.Visibility == Visibility.Visible && int.TryParse(txtProdStock?.Text?.Trim() ?? "", out int stock) && stock >= 0)
                HideError(errProdStock, bdrProdStock);
        }

        private void TxtProdReorder_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (errProdReorder != null && errProdReorder.Visibility == Visibility.Visible && int.TryParse(txtProdReorder?.Text?.Trim() ?? "", out int reorder) && reorder >= 0)
                HideError(errProdReorder, bdrProdReorder);
        }

        // =========================================================================
        // ── LIVE SEARCHABLE COMBOBOX EVENT HANDLERS ──────────────────────────────
        // =========================================================================
        private bool _isFilteringCmb = false;
        private bool _isUserTyping = false;

        private void CmbSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Allow arrow keys, enter, tab, and escape to navigate the list without filtering
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab)
            {
                _isUserTyping = false;
                return;
            }

            _isUserTyping = true;
        }

        private void CmbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFilteringCmb) return;
            if (sender is not ComboBox cb || !cb.IsEditable) return;

            // Only filter and open if the user physically typed on the keyboard!
            // When opening the window or clicking items, this is false so it stays closed.
            if (!_isUserTyping) return;

            var tb = cb.Template?.FindName("PART_EditableTextBox", cb) as TextBox;
            string filterText = tb != null ? tb.Text : cb.Text;

            if (cb.ItemsSource == null) return;
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(cb.ItemsSource);
            if (view == null) return;

            _isFilteringCmb = true;
            try
            {
                if (string.IsNullOrWhiteSpace(filterText))
                {
                    view.Filter = null;
                }
                else
                {
                    view.Filter = item =>
                    {
                        if (item == null) return false;
                        if (item is ProductItem p)
                        {
                            return (p.ProductName != null && p.ProductName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                   (p.SKU != null && p.SKU.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                   (p.BrandName != null && p.BrandName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
                        }
                        if (item is SupplierModel s)
                        {
                            return (s.SupplierName != null && s.SupplierName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                   (s.DisplayName != null && s.DisplayName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
                        }
                        return item.ToString().IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
                    };
                }

                // Show the dropdown with matching results while typing
                cb.IsDropDownOpen = true;

                // Keep cursor at the end of the typed text
                if (tb != null)
                {
                    tb.SelectionStart = tb.Text.Length;
                    tb.SelectionLength = 0;
                }
            }
            finally
            {
                _isFilteringCmb = false;
                _isUserTyping = false;
            }
        }

        private void CmbSearch_DropDownOpened(object sender, EventArgs e)
        {
            // When the user clicks the drop-down arrow to browse, clear any filter
            // so ALL items are visible and can be scrolled through!
            if (sender is ComboBox cb && cb.ItemsSource != null && !_isFilteringCmb)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(cb.ItemsSource);
                if (view != null && view.Filter != null)
                {
                    _isFilteringCmb = true;
                    view.Filter = null;
                    _isFilteringCmb = false;
                }
            }
        }

        private void CmbSearch_DropDownClosed(object sender, EventArgs e)
        {
            // Restore full list when closed so the next time it's opened, all items are available
            if (sender is ComboBox cb && cb.ItemsSource != null && !_isFilteringCmb)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(cb.ItemsSource);
                if (view != null && view.Filter != null)
                {
                    _isFilteringCmb = true;
                    view.Filter = null;
                    _isFilteringCmb = false;
                }
            }
        }
    }
}