using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using BikeKings_System.Database;
using BikeKings_System.Helpers;
using System.Threading.Tasks;

namespace BikeKings_System.Views.Cashier
{
    public partial class CashierDashboardWindow : Window
    {
        private List<POSSearchItem> _allPosCatalogItems = new List<POSSearchItem>();
        private string _posActiveCategory = "All";
        private readonly ObservableCollection<POSCartItem> _cart = new ObservableCollection<POSCartItem>();
        private List<LaborServiceModel> _activeServices = new List<LaborServiceModel>();
        private List<TransactionItem> _allTransactions = new List<TransactionItem>();
        private List<ProductItem> _allProducts = new List<ProductItem>();
        private ReceiptFullDetails? _activeReceipt = null;
        private DispatcherTimer? _clockTimer;
        private bool _isReady = false;
        private bool _isUpdatingLedgerDates = false;       
        private DispatcherTimer? _ledgerSearchTimer;        

        public CashierDashboardWindow()
        {
            InitializeComponent();
            _isReady = true;

            // 1. Live Clock
            StartClock();

            // 2. Profile Setup
            SetupCashierProfile();

            // 3. Bind Cart
            icCartItems.ItemsSource = _cart;
            _cart.CollectionChanged += (s, e) => UpdateCartUI();

            // 4. Load Dynamic Quick Services directly from Database
            LoadDynamicServices();

            // 5. Load Mechanics from Database
            LoadMechanics();

            // 6. Past Transactions Ledger
            ApplyLedgerFilters();

            LoadPosCatalogBoxes();
        }

        private void SetupCashierProfile()
        {
            string name = SessionHelper.CurrentUser?.FullName ?? "Ruby";
            string uname = SessionHelper.CurrentUser?.Username ?? "cashier";

            lblCashierName.Text = name;
            lblAvatarInitial.Text = !string.IsNullOrWhiteSpace(name) ? name[0].ToString().ToUpper() : "C";

            lblProfileFullName.Text = name;
            lblProfileUsername.Text = uname;
        }

        private void StartClock()
        {
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) =>
            {
                lblLiveClock.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy 'at' h:mm:ss tt");
            };
            _clockTimer.Start();
            lblLiveClock.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy 'at' h:mm:ss tt");
        }

        // ══════════════════════════════════════════════════════
        // 1. DYNAMIC DATA FROM DATABASE (SERVICES & MECHANICS)
        // ══════════════════════════════════════════════════════
        private void LoadDynamicServices()
        {
            _activeServices = CashierService.GetActiveServices();
        }

        private void LoadMechanics()
        {
            var mechanics = CashierService.GetMechanics();
            cmbMechanic.ItemsSource = mechanics;
            if (mechanics.Count > 0) cmbMechanic.SelectedIndex = 0;
        }

        private void DynamicQuickService_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is LaborServiceModel srv)
            {
                AddServiceToCart(srv.ServiceID, srv.ServiceName, srv.LaborFee);
            }
        }

        // ══════════════════════════════════════════════════════
        // 2. SIDEBAR NAVIGATION TABS
        // ══════════════════════════════════════════════════════
        private void NavTabSales_Click(object sender, MouseButtonEventArgs e)
        {
            SwitchView(viewSalesJobOrders);
            SetNavActive(navTabSales, txtNavSales);
            lblHeaderTitle.Text = "Sales & Job Orders (POS)";
            LoadDynamicServices(); // Re-sync if admin updated any service
            LoadPosCatalogBoxes();
        }

        private void NavTabHistory_Click(object sender, MouseButtonEventArgs e)
        {
            SwitchView(viewHistory);
            SetNavActive(navTabHistory, txtNavHistory);
            lblHeaderTitle.Text = "Transaction History";
            ApplyLedgerFilters();
        }

        private void NavTabInventory_Click(object sender, MouseButtonEventArgs e)
        {
            SwitchView(viewInventory);
            SetNavActive(navTabInventory, txtNavInventory);
            lblHeaderTitle.Text = "Stock Inventory (Inquiry)";
            LoadInventoryCatalog();
        }

        private void NavTabProfile_Click(object sender, MouseButtonEventArgs e)
        {
            SwitchView(viewProfile);
            SetNavActive(navTabProfile, txtNavProfile);
            lblHeaderTitle.Text = "Cashier Profile";
            SetupCashierProfile();
        }

        private void SwitchView(UIElement targetView)
        {
            viewSalesJobOrders.Visibility = Visibility.Collapsed;
            viewHistory.Visibility = Visibility.Collapsed;
            viewInventory.Visibility = Visibility.Collapsed;
            viewProfile.Visibility = Visibility.Collapsed;

            targetView.Visibility = Visibility.Visible;
        }

        private void LoadPosCatalogBoxes()
        {
            _allPosCatalogItems.Clear();

            // 1. Add all active products
            var products = BikeKings_System.Database.InventoryService.GetProducts("", "All Stocks");
            foreach (var p in products)
            {
                _allPosCatalogItems.Add(new POSSearchItem
                {
                    ID = p.ProductID,
                    ItemType = "Product",
                    Name = p.ProductName,
                    Code = p.SKU,
                    Price = p.UnitPrice,
                    Stock = p.Quantity,
                    ImagePath = p.ImagePath,
                    CategoryName = p.CategoryName
                });
            }

            // 2. Add all active services
            var services = CashierService.GetActiveServices();
            foreach (var s in services)
            {
                _allPosCatalogItems.Add(new POSSearchItem
                {
                    ID = s.ServiceID,
                    ItemType = "Service",
                    Name = s.ServiceName,
                    Code = "SRV",
                    Price = s.LaborFee,
                    Stock = 999,
                    ImagePath = "",
                    CategoryName = "Labor Services"
                });
            }
            LoadPosCategoryChips();
            FilterPosCatalog();
        }

        private void FilterPosCatalog()
        {
            if (icPosCatalogBoxes == null) return;

            string search = txtSearchPos?.Text?.Trim().ToLower() ?? "";
            string cat = _posActiveCategory ?? "All";

            var filtered = _allPosCatalogItems.Where(i =>
            {
                // Category Filter
                bool matchesCategory = true;
                if (cat == "Services")
                {
                    matchesCategory = i.ItemType == "Service";
                }
                else if (cat != "All")
                {
                    string target = cat.ToLower();
                    matchesCategory = i.ItemType == "Product" &&
                                     (i.CategoryName.ToLower().Contains(target) || i.Name.ToLower().Contains(target));
                }

                // Search Filter
                bool matchesSearch = string.IsNullOrEmpty(search) ||
                                     i.Name.ToLower().Contains(search) ||
                                     i.Code.ToLower().Contains(search) ||
                                     i.CategoryName.ToLower().Contains(search);

                return matchesCategory && matchesSearch;
            }).ToList();

            icPosCatalogBoxes.ItemsSource = filtered;
            if (pnlNoPosMatches != null)
                pnlNoPosMatches.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtSearchPos_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSearchPosPlaceholder != null)
                txtSearchPosPlaceholder.Visibility = string.IsNullOrEmpty(txtSearchPos?.Text) ? Visibility.Visible : Visibility.Collapsed;

            FilterPosCatalog();
        }

        private void LoadPosCategoryChips()
        {
            if (spPosCategoryChips == null) return;
            spPosCategoryChips.Children.Clear();

            // 1. "All Items" Chip
            var btnAll = CreateCategoryChipButton("All Items", "All", _posActiveCategory == "All");
            spPosCategoryChips.Children.Add(btnAll);

            // 2. Dynamic Categories directly from Database
            var dbCategories = BikeKings_System.Database.InventoryService.GetCategories() ?? new List<CategoryModel>();
            foreach (var cat in dbCategories)
            {
                if (string.IsNullOrWhiteSpace(cat.CategoryName)) continue;
                bool isSelected = string.Equals(_posActiveCategory, cat.CategoryName, StringComparison.OrdinalIgnoreCase);
                var btnCat = CreateCategoryChipButton(cat.CategoryName, cat.CategoryName, isSelected);
                spPosCategoryChips.Children.Add(btnCat);
            }

            // 3. "🔧 Labor Services" Chip at the end
            bool isServicesSelected = _posActiveCategory == "Services";
            var btnServices = CreateCategoryChipButton("🔧 Labor Services", "Services", isServicesSelected, isSpecialService: true);
            spPosCategoryChips.Children.Add(btnServices);
        }

        private Button CreateCategoryChipButton(string content, string tag, bool isSelected, bool isSpecialService = false)
        {
            var btn = new Button
            {
                Content = content,
                Tag = tag,
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 6, 0),
                FontWeight = isSelected || isSpecialService ? FontWeights.Bold : FontWeights.SemiBold,
                FontSize = 10.5,
                Cursor = Cursors.Hand
            };

            var style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(6)));
            btn.Resources.Add(typeof(Border), style);

            if (isSelected)
            {
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E63956"));
                btn.Foreground = Brushes.White;
                btn.BorderThickness = new Thickness(0);
            }
            else
            {
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1929"));
                btn.Foreground = isSpecialService
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C95AD"));
                btn.BorderBrush = isSpecialService
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F3B59"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#352C44"));
                btn.BorderThickness = new Thickness(1);
            }

            btn.Click += BtnCatFilter_Click;
            return btn;
        }

        private void BtnCatFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedBtn && clickedBtn.Tag is string catTag)
            {
                _posActiveCategory = catTag;

                if (spPosCategoryChips != null)
                {
                    foreach (var child in spPosCategoryChips.Children)
                    {
                        if (child is Button b)
                        {
                            bool isThis = b == clickedBtn;
                            bool isService = (b.Tag as string) == "Services";

                            if (isThis)
                            {
                                b.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E63956"));
                                b.Foreground = Brushes.White;
                                b.BorderThickness = new Thickness(0);
                            }
                            else
                            {
                                b.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1929"));
                                b.Foreground = isService
                                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA"))
                                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C95AD"));
                                b.BorderBrush = isService
                                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F3B59"))
                                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#352C44"));
                                b.BorderThickness = new Thickness(1);
                            }
                        }
                    }
                }

                FilterPosCatalog();
            }
        }

        private void PosCatalogBox_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is POSSearchItem item)
            {
                AddToCart(item);
            }
        }

        private void BtnAddPosCatalogBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is POSSearchItem item)
            {
                AddToCart(item);
            }
        }
        private void SetNavActive(Border activeTab, TextBlock activeText)
        {
            var transparent = Brushes.Transparent;
            var pink = (Brush)new BrushConverter().ConvertFromString("#E63956")!;
            var inactiveText = (Brush)new BrushConverter().ConvertFromString("#9C95AD")!;

            navTabSales.Background = transparent;
            txtNavSales.Foreground = inactiveText;
            txtNavSales.FontWeight = FontWeights.SemiBold;

            navTabHistory.Background = transparent;
            txtNavHistory.Foreground = inactiveText;
            txtNavHistory.FontWeight = FontWeights.SemiBold;

            navTabInventory.Background = transparent;
            txtNavInventory.Foreground = inactiveText;
            txtNavInventory.FontWeight = FontWeights.SemiBold;

            navTabProfile.Background = transparent;
            txtNavProfile.Foreground = inactiveText;
            txtNavProfile.FontWeight = FontWeights.SemiBold;

            activeTab.Background = pink;
            activeText.Foreground = Brushes.White;
            activeText.FontWeight = FontWeights.SemiBold;
        }

        // ══════════════════════════════════════════════════════
        // 3. VIEW-ONLY INVENTORY CATALOG (PRODUCTS & SERVICES)
        // ══════════════════════════════════════════════════════
        private List<ProductItem> _cachedCatalogProducts = new List<ProductItem>();
        private List<LaborServiceModel> _cachedCatalogServices = new List<LaborServiceModel>();
        private void SetupInventoryDropdowns()
        {
            try
            {
                var categories = MaintenanceService.GetCategoriesList();
                categories.Insert(0, new CategoryModel { CategoryID = 0, CategoryName = "All Categories" });
                cmbFilterCategory.ItemsSource = categories;
                cmbFilterCategory.SelectedIndex = 0;
                var brands = MaintenanceService.GetBrandsList();
                brands.Insert(0, new BrandModel { BrandID = 0, BrandName = "All Brands" });
                cmbFilterBrand.ItemsSource = brands;
                cmbFilterBrand.SelectedIndex = 0;
            }
            catch { }
        }
        private void LoadInventoryCatalog()
        {
            if (!_isReady) return;
            if (cmbFilterCategory.ItemsSource == null) SetupInventoryDropdowns();
            string search = txtSearchProducts?.Text?.Trim().ToLower() ?? "";
            int selectedCatId = (cmbFilterCategory?.SelectedValue is int catVal) ? catVal : 0;
            int selectedBrandId = (cmbFilterBrand?.SelectedValue is int brandVal) ? brandVal : 0;
            string stockFilter = (cmbFilterStockStatus?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Stock Levels";
            string sortOrder = (cmbSortProducts?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Sort: SKU (A-Z)";
            var all = InventoryService.GetProducts("", "All");
            var filtered = all.Where(p =>
                (string.IsNullOrEmpty(search) ||
                 (p.SKU ?? "").ToLower().Contains(search) ||
                 (p.ProductName ?? "").ToLower().Contains(search) ||
                 (p.CategoryName ?? "").ToLower().Contains(search) ||
                 (p.BrandName ?? "").ToLower().Contains(search))
                && (selectedCatId == 0 || p.CategoryID == selectedCatId)
                && (selectedBrandId == 0 || p.BrandID == selectedBrandId)
                && (stockFilter == "All Stock Levels" ||
                    (stockFilter == "In Stock Only" && p.Quantity > p.ReorderLevel) ||
                    (stockFilter == "Low Stock Alert" && p.Quantity > 0 && p.Quantity <= p.ReorderLevel) ||
                    (stockFilter == "Out of Stock" && p.Quantity <= 0))
            ).ToList();
            // Apply Sorting
            if (sortOrder.Contains("Product Name")) filtered = filtered.OrderBy(p => p.ProductName).ToList();
            else if (sortOrder.Contains("Lowest First")) filtered = filtered.OrderBy(p => p.Quantity).ToList();
            else if (sortOrder.Contains("Highest First") && sortOrder.Contains("In-Stock")) filtered = filtered.OrderByDescending(p => p.Quantity).ToList();
            else if (sortOrder.Contains("Price")) filtered = filtered.OrderByDescending(p => p.UnitPrice).ToList();
            else filtered = filtered.OrderBy(p => p.SKU).ToList();
            _cachedCatalogProducts = filtered;
            icProductsCatalog.ItemsSource = _cachedCatalogProducts;
            if (pnlEmptyProducts != null)
                pnlEmptyProducts.Visibility = _cachedCatalogProducts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        private void LoadServicesCatalog()
        {
            string search = txtSearchServices?.Text?.Trim().ToLower() ?? "";
            var allServices = CashierService.GetActiveServices();
            var filtered = string.IsNullOrEmpty(search)
                ? allServices
                : allServices.Where(s => s.ServiceName.ToLower().Contains(search)).ToList();
            _cachedCatalogServices = filtered;
            icServicesCatalog.ItemsSource = _cachedCatalogServices;
            if (pnlEmptyServices != null)
                pnlEmptyServices.Visibility = _cachedCatalogServices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        private void InvFilter_Changed(object sender, EventArgs e)
        {
            if (txtSearchProductsPlaceholder != null)
                txtSearchProductsPlaceholder.Visibility = string.IsNullOrEmpty(txtSearchProducts.Text) ? Visibility.Visible : Visibility.Collapsed;
            LoadInventoryCatalog();
        }
        private void TxtSearchServices_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSearchServicesPlaceholder != null)
                txtSearchServicesPlaceholder.Visibility = string.IsNullOrEmpty(txtSearchServices.Text) ? Visibility.Visible : Visibility.Collapsed;
            LoadServicesCatalog();
        }
        private void BtnClearInvFilters_Click(object sender, RoutedEventArgs e)
        {
            if (txtSearchProducts != null) txtSearchProducts.Text = "";
            if (cmbFilterCategory != null) cmbFilterCategory.SelectedIndex = 0;
            if (cmbFilterBrand != null) cmbFilterBrand.SelectedIndex = 0;
            if (cmbFilterStockStatus != null) cmbFilterStockStatus.SelectedIndex = 0;
            if (cmbSortProducts != null) cmbSortProducts.SelectedIndex = 0;
            LoadInventoryCatalog();
        }
        private void TabInvProducts_Click(object sender, RoutedEventArgs e)
        {
            tabBtnProducts.Background = (Brush)new BrushConverter().ConvertFromString("#E63956")!;
            tabBtnProducts.Foreground = Brushes.White;
            tabBtnServices.Background = Brushes.Transparent;
            tabBtnServices.Foreground = (Brush)new BrushConverter().ConvertFromString("#9C95AD")!;
            pnlProductsTab.Visibility = Visibility.Visible;
            pnlServicesTab.Visibility = Visibility.Collapsed;
            LoadInventoryCatalog();
        }
        private void TabInvServices_Click(object sender, RoutedEventArgs e)
        {
            tabBtnServices.Background = (Brush)new BrushConverter().ConvertFromString("#E63956")!;
            tabBtnServices.Foreground = Brushes.White;
            tabBtnProducts.Background = Brushes.Transparent;
            tabBtnProducts.Foreground = (Brush)new BrushConverter().ConvertFromString("#9C95AD")!;
            pnlServicesTab.Visibility = Visibility.Visible;
            pnlProductsTab.Visibility = Visibility.Collapsed;
            LoadServicesCatalog();
        }
        private void BtnQuickAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ProductItem prod)
            {
                if (prod.Quantity <= 0)
                {
                    MessageBox.Show($"Product '{prod.ProductName}' is currently OUT OF STOCK.", "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                AddToCart(new POSSearchItem
                {
                    ID = prod.ProductID,
                    ItemType = "Product",
                    Name = prod.ProductName,
                    Code = prod.SKU,
                    Price = prod.UnitPrice,
                    Stock = prod.Quantity
                });
                // Jump directly to POS register
                SwitchView(viewSalesJobOrders);
                SetNavActive(navTabSales, txtNavSales);
                lblHeaderTitle.Text = "Sales & Job Orders (POS)";
            }
        }

        private void ProductCard_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is BikeKings_System.Database.ProductItem prod)
            {
                if (prod.Quantity <= 0)
                {
                    MessageBox.Show($"Product '{prod.ProductName}' is currently OUT OF STOCK.", "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AddToCart(new POSSearchItem
                {
                    ID = prod.ProductID,
                    ItemType = "Product",
                    Name = prod.ProductName,
                    Code = prod.SKU,
                    Price = prod.UnitPrice,
                    Stock = prod.Quantity
                });
            }
        }

        private void BtnQuickLabor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var parts = tag.Split('|');
                if (parts.Length == 2 && decimal.TryParse(parts[1], out decimal price))
                {
                    AddServiceToCart(0, parts[0], price);
                    CalculateTotals();
                }
            }
        }
        private void BtnQuickAddLaborToCart_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is LaborServiceModel srv)
            {
                AddServiceToCart(srv.ServiceID, srv.ServiceName, srv.LaborFee);
                // Jump directly to POS register
                SwitchView(viewSalesJobOrders);
                SetNavActive(navTabSales, txtNavSales);
                lblHeaderTitle.Text = "Sales & Job Orders (POS)";
            }
        }
        private void BtnExportInvCsv_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                FileName = $"BikeKings_InventoryInquiry_{DateTime.Now:yyyyMMdd}.csv",
                DefaultExt = ".csv",
                Filter = "CSV Spreadsheet (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (saveDialog.ShowDialog() == true)
            {
                using var sw = new StreamWriter(saveDialog.FileName, false, new System.Text.UTF8Encoding(true));
                if (pnlProductsTab.Visibility == Visibility.Visible)
                {
                    sw.WriteLine("SKU,ProductName,Category,Brand,InStock,SellingPrice,Status");
                    foreach (var p in _cachedCatalogProducts)
                    {
                        sw.WriteLine($"\"{p.SKU}\",\"{p.ProductName}\",\"{p.CategoryName}\",\"{p.BrandName}\",{p.Quantity},{p.UnitPrice},\"{p.StockStatus}\"");
                    }
                }
                else
                {
                    sw.WriteLine("ServiceID,ServiceName,Category,LaborFee,Availability");
                    foreach (var s in _cachedCatalogServices)
                    {
                        sw.WriteLine($"\"SRV-{s.ServiceID:D3}\",\"{s.ServiceName}\",\"Labor & Repair\",{s.LaborFee},\"Active\"");
                    }
                }
                MessageBox.Show("Inventory inquiry exported to CSV successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ══════════════════════════════════════════════════════
        // 4. MY PROFILE & CHANGE PASSWORD (WITH VALIDATIONS)
        // ══════════════════════════════════════════════════════
        private void BtnUpdatePassword_Click(object sender, RoutedEventArgs e)
        {
            string currentPw = pbCurrentPassword.Password.Trim();
            string newPw = pbNewPassword.Password.Trim();
            string confirmPw = pbConfirmPassword.Password.Trim();

            if (string.IsNullOrEmpty(currentPw) || string.IsNullOrEmpty(newPw) || string.IsNullOrEmpty(confirmPw))
            {
                MessageBox.Show("Please fill in all password fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPw.Length < 6)
            {
                MessageBox.Show("New password must be at least 6 characters long for security.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPw == currentPw)
            {
                MessageBox.Show("New password cannot be the same as your current password.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPw != confirmPw)
            {
                MessageBox.Show("New Password and Confirmation Password do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int userId = SessionHelper.CurrentUser?.UserID ?? 2;
            string username = SessionHelper.CurrentUser?.Username ?? "cashier";

            bool success = AuthService.ChangePassword(userId, currentPw, newPw);
            if (success)
            {
                AuditLogService.Log("CHANGE_PASSWORD", $"Cashier '{username}' updated their account password.", userId, username, "Cashier");
                MessageBox.Show("✅ Password updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                pbCurrentPassword.Clear();
                pbNewPassword.Clear();
                pbConfirmPassword.Clear();
            }
            else
            {
                MessageBox.Show("Current password is incorrect.", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

       
        private void TxtSearchPos_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Pressing Enter adds the first matching card directly into the cart
                if (icPosCatalogBoxes?.ItemsSource is System.Collections.IEnumerable list)
                {
                    var enumerator = list.GetEnumerator();
                    if (enumerator.MoveNext() && enumerator.Current is POSSearchItem firstMatch)
                    {
                        AddToCart(firstMatch);
                        txtSearchPos.Text = "";
                    }
                }
                e.Handled = true;
            }
        }

        private void AddToCart(POSSearchItem item)
        {
            txtSearchPos.Text = "";
            if (item.ItemType == "Product")
            {
                // 🛡️ Always query live stock from database
                int liveStock = CashierService.GetProductStock(item.ID);
                if (liveStock <= 0)
                {
                    MessageBox.Show($"Product '{item.Name}' is currently OUT OF STOCK (0 units available in inventory).", "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var existing = _cart.FirstOrDefault(c => c.ItemID == item.ID && c.ItemType == "Product");
                if (existing != null)
                {
                    if (existing.Quantity >= liveStock)
                    {
                        MessageBox.Show($"Cannot add more. Available stock in inventory for '{item.Name}' is only {liveStock} unit(s).", "Stock Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    existing.StockAvailable = liveStock;
                    existing.Quantity++;
                }
                else
                {
                    _cart.Add(new POSCartItem
                    {
                        ItemID = item.ID,
                        ItemType = "Product",
                        Barcode = item.Code,
                        Description = item.Name,
                        UnitPrice = item.Price,
                        Quantity = 1,
                        StockAvailable = liveStock,
                        ImagePath = item.ImagePath
                    });
                }
            }
            else
            {
                AddServiceToCart(item.ID, item.Name, item.Price);
            }
            CalculateTotals();
        }

        private void AddServiceToCart(int serviceId, string serviceName, decimal fee)
        {
            var existing = _cart.FirstOrDefault(c => c.Description == serviceName && c.ItemType == "Service");
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                _cart.Add(new POSCartItem
                {
                    ItemID = serviceId,
                    ItemType = "Service",
                    Barcode = "LABOR",
                    Description = serviceName,
                    UnitPrice = fee,
                    Quantity = 1,
                    StockAvailable = 999
                });
            }

            CalculateTotals();
        }

        // ══════════════════════════════════════════════════════
        // 6. CART OPERATIONS & UI UPDATE
        // ══════════════════════════════════════════════════════
        private void BtnQtyMinus_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is POSCartItem item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    _cart.Remove(item);
                }
                CalculateTotals();
            }
        }

        private void BtnQtyPlus_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is POSCartItem item)
            {
                if (item.ItemType == "Product")
                {
                    // 🛡️ Real-time inventory check: prevent exceeding available stock
                    int liveStock = CashierService.GetProductStock(item.ItemID);
                    item.StockAvailable = liveStock;

                    if (item.Quantity >= liveStock)
                    {
                        MessageBox.Show($"Cannot add more. Available stock in inventory for '{item.Description}' is {liveStock} unit(s).", "Stock Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                item.Quantity++;
                CalculateTotals();
            }
        }

        private void BtnRemoveCartItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is POSCartItem item)
            {
                _cart.Remove(item);
                CalculateTotals();
            }
        }

        private void BtnClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0) return;

            var confirm = MessageBox.Show("Are you sure you want to clear the cart?", "Clear Cart", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                _cart.Clear();
                CalculateTotals();
            }
        }

        private void UpdateCartUI()
        {
            lblCartTitle.Text = $"TRANSACTION CART ({_cart.Count} ITEMS)";
            pnlEmptyCart.Visibility = _cart.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CalculateTotals();
        }

        private void CalculateTotals()
        {
            if (!_isReady) return;
            // Automatically show Customer & Job Details ONLY if any Labor Service is in the cart
            bool hasServices = _cart.Any(x => x.ItemType == "Service");
            if (cardCustomerJobDetails != null)
            {
                cardCustomerJobDetails.Visibility = hasServices ? Visibility.Visible : Visibility.Collapsed;
            }
            decimal subtotal = _cart.Sum(x => x.Subtotal);
            lblSubtotal.Text = $"₱{subtotal:N2}";

            decimal.TryParse(txtDiscount?.Text?.Trim(), out decimal discount);

            // Restriction: Discount cannot exceed subtotal or be negative
            if (discount < 0) discount = 0;
            if (discount > subtotal && subtotal > 0)
            {
                discount = subtotal;
            }

            decimal grandTotal = Math.Max(0, subtotal - discount);
            lblGrandTotal.Text = $"₱{grandTotal:N2}";

            string paymentMethod = (cmbPaymentMethod?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";

            // If E-Wallet, auto-fill tendered with grandTotal and lock
            if (paymentMethod == "E-Wallet" && txtAmountTendered != null)
            {
                txtAmountTendered.Text = grandTotal.ToString("F2");
                txtAmountTendered.IsReadOnly = true;
            }
            else if (txtAmountTendered != null)
            {
                txtAmountTendered.IsReadOnly = false;
            }

            decimal.TryParse(txtAmountTendered?.Text?.Trim(), out decimal tendered);
            decimal change = paymentMethod == "E-Wallet" ? 0 : Math.Max(0, tendered - grandTotal);
            lblChangeDue.Text = $"₱{change:N2}";
        }

        private void TxtDiscount_TextChanged(object sender, TextChangedEventArgs e) => CalculateTotals();
        private void TxtAmountTendered_TextChanged(object sender, TextChangedEventArgs e) => CalculateTotals();

        private void CmbPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady) return;
            string method = (cmbPaymentMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";

            // Toggle Reference Number visibility
            if (pnlReferenceNo != null)
            {
                pnlReferenceNo.Visibility = method == "E-Wallet" ? Visibility.Visible : Visibility.Collapsed;
                if (method != "E-Wallet" && txtReferenceNo != null)
                {
                    txtReferenceNo.Clear();
                }
            }

            CalculateTotals();
        }

        // ══════════════════════════════════════════════════════
        // 7. INPUT RESTRICTIONS & REGEX EVENT HANDLERS
        // ══════════════════════════════════════════════════════
        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow 0-9
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
        }

        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits and one decimal dot
            string currentText = (sender as TextBox)?.Text ?? "";
            if (e.Text == ".")
            {
                e.Handled = currentText.Contains(".");
            }
            else
            {
                e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
            }
        }

        private void AlphanumericDashOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only letters, numbers, spaces, and hyphens (standard for E-Wallets)
            e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Z0-9\-\s]+$");
        }

        private void TxtReferenceNo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtReferenceNo == null) return;
            int caret = txtReferenceNo.CaretIndex;
            txtReferenceNo.Text = txtReferenceNo.Text.ToUpper();
            txtReferenceNo.CaretIndex = caret;
        }

        private void TxtPlateNo_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Auto uppercase plate numbers
            int caret = txtPlateNo.CaretIndex;
            txtPlateNo.Text = txtPlateNo.Text.ToUpper();
            txtPlateNo.CaretIndex = caret;
        }

        private void AutoCapitalizeWords_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && !string.IsNullOrEmpty(tb.Text))
            {
                int caret = tb.CaretIndex;
                char[] chars = tb.Text.ToCharArray();
                bool newWord = true;
                bool changed = false;

                for (int i = 0; i < chars.Length; i++)
                {
                    if (char.IsWhiteSpace(chars[i]) || chars[i] == '-' || chars[i] == '.')
                    {
                        newWord = true;
                    }
                    else if (newWord)
                    {
                        if (char.IsLower(chars[i]))
                        {
                            chars[i] = char.ToUpper(chars[i]);
                            changed = true;
                        }
                        newWord = false;
                    }
                }

                if (changed)
                {
                    tb.Text = new string(chars);
                    tb.CaretIndex = Math.Min(caret, tb.Text.Length);
                }
            }
        }

        private void TxtContactNo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && !string.IsNullOrEmpty(tb.Text))
            {
                // Strip any pasted non-numeric characters automatically
                string cleaned = Regex.Replace(tb.Text, @"[^0-9]", "");
                if (tb.Text != cleaned)
                {
                    int caret = tb.CaretIndex;
                    tb.Text = cleaned;
                    tb.CaretIndex = Math.Min(caret, cleaned.Length);
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 8. COMPLETE TRANSACTION (CHECKOUT WITH VALIDATIONS)
        // ══════════════════════════════════════════════════════
        private void BtnCompleteTransaction_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0)
            {
                MessageBox.Show("Cannot complete transaction. The cart is currently empty.", "Empty Cart", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 🛡️ Pre-Checkout Stock Gate: verify live inventory for all products in cart
            foreach (var cartItem in _cart.Where(x => x.ItemType == "Product"))
            {
                int liveStock = CashierService.GetProductStock(cartItem.ItemID);
                if (cartItem.Quantity > liveStock)
                {
                    MessageBox.Show($"Cannot complete transaction.\n\nRequested quantity for '{cartItem.Description}' ({cartItem.Quantity} units) exceeds currently available stock ({liveStock} units in inventory).\n\nPlease adjust the cart quantity.", "Insufficient Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            decimal subtotal = _cart.Sum(x => x.Subtotal);
            decimal.TryParse(txtDiscount.Text.Trim(), out decimal discount);

            if (discount < 0)
            {
                MessageBox.Show("Discount amount cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtDiscount.Focus();
                return;
            }

            if (discount > subtotal)
            {
                MessageBox.Show($"Discount amount (₱{discount:N2}) cannot exceed the subtotal (₱{subtotal:N2}).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtDiscount.Focus();
                return;
            }

            decimal grandTotal = Math.Max(0, subtotal - discount);
            string paymentMethod = (cmbPaymentMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
            decimal.TryParse(txtAmountTendered.Text.Trim(), out decimal tendered);

            // 🛡️ Enforce & Validate Reference Number for E-Wallet (Regex check)
            string rawRef = txtReferenceNo?.Text?.Trim() ?? "";
            string refNo = rawRef.Replace(" ", ""); // Strip spaces from GCash/Maya SMS pastes

            if (paymentMethod == "E-Wallet")
            {
                if (string.IsNullOrWhiteSpace(refNo))
                {
                    MessageBox.Show("Please enter the E-Wallet reference number to complete this transaction.", "Reference Number Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtReferenceNo?.Focus();
                    return;
                }

                // Minimum 6 to 30 alphanumeric characters / hyphens (case-insensitive for GCash/Maya)
                if (!Regex.IsMatch(refNo, @"^[a-zA-Z0-9\-]{6,30}$"))
                {
                    MessageBox.Show("Invalid reference number format.\n\nE-Wallet reference numbers must be between 6 and 30 letters/numbers (e.g. 102938475612 or MP-981234).", "Invalid Reference Number", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtReferenceNo?.Focus();
                    return;
                }
            } 

            if (paymentMethod == "Cash" && tendered < grandTotal)
            {
                MessageBox.Show($"Insufficient cash tendered.\nGrand Total: ₱{grandTotal:N2}\nAmount Tendered: ₱{tendered:N2}", "Payment Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtAmountTendered.Focus();
                return;
            }

            if (paymentMethod != "Cash" && tendered <= 0)
            {
                tendered = grandTotal;
            }

            // Validate Contact Number format if entered
            string contactNo = txtContactNo.Text.Trim();
            if (!string.IsNullOrWhiteSpace(contactNo) && !Regex.IsMatch(contactNo, @"^09\d{9}$"))
            {
                MessageBox.Show("Invalid contact number format. Please enter a valid 11-digit mobile number starting with '09' (e.g. 09171234567).", "Invalid Contact Number", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtContactNo.Focus();
                return;
            }

            // Validate Customer Name (Supports international and Filipino letters with ñ, spaces, and hyphens)
            string custName = string.IsNullOrWhiteSpace(txtCustomerName.Text.Trim()) ? "Walk-in Customer" : txtCustomerName.Text.Trim();
            if (custName != "Walk-in Customer" && !Regex.IsMatch(custName, @"^[\p{L}\s.\-']{2,100}$"))
            {
                MessageBox.Show("Please enter a valid customer name (letters, spaces, and hyphens only).", "Invalid Customer Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCustomerName.Focus();
                return;
            }

            // Validate Motorcycle Plate Number if entered (LTO formats: 123-ABC, AB 1234, 123ABC)
            string plateNo = txtPlateNo.Text.Trim().ToUpper();
            if (!string.IsNullOrWhiteSpace(plateNo) && !Regex.IsMatch(plateNo, @"^[A-Z0-9\s\-]{4,10}$"))
            {
                MessageBox.Show("Invalid plate number format.\n\nPlease enter a valid vehicle plate (e.g. 123-ABC, AB 1234, or 123ABC).", "Invalid Plate Number", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPlateNo.Focus();
                return;
            }

            string mcBrand = txtMcBrand.Text.Trim();
            string mcModel = txtMcModel.Text.Trim();
            string mechanic = cmbMechanic.SelectedItem?.ToString() ?? "Unassigned";

            int cashierId = SessionHelper.CurrentUser?.UserID ?? 2;
            string cashierName = SessionHelper.CurrentUser?.FullName ?? "Ruby";
            string cashierRole = SessionHelper.CurrentUser?.Role ?? "Cashier";



            // Format Payment Method to include Ref No for records & receipts
            string finalPaymentMethod = paymentMethod == "E-Wallet" && !string.IsNullOrWhiteSpace(refNo)
                ? $"E-Wallet ({refNo})"
                : paymentMethod;

            var result = CashierService.ProcessCheckout(
                _cart.ToList(),
                custName,
                contactNo,
                mcBrand,
                mcModel,
                plateNo,
                mechanic,
                discount,
                finalPaymentMethod,
                tendered,
                cashierId,
                cashierName,
                cashierRole
            );

            if (result.Success)
            {
                MessageBox.Show($"✅ Transaction #{result.ReferenceNo} completed successfully!", "Transaction Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Show Official Receipt Modal immediately
                ShowReceiptModal(result.ReferenceNo, result.TransType);

                // Reset Form
                _cart.Clear();
                txtCustomerName.Text = "Walk-in Customer";
                txtContactNo.Text = "";
                txtMcBrand.Text = "";
                txtMcModel.Text = "";
                txtPlateNo.Text = "";
                txtDiscount.Text = "0.00";
                txtAmountTendered.Text = "0.00";
                if (txtReferenceNo != null) txtReferenceNo.Text = "";
                if (pnlReferenceNo != null) pnlReferenceNo.Visibility = Visibility.Collapsed;
                cmbPaymentMethod.SelectedIndex = 0;
                cmbMechanic.SelectedIndex = 0;

                // Refresh Ledger
                ApplyLedgerFilters();
            }
            else
            {
                MessageBox.Show($"Checkout Error:\n{result.ErrorMessage}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════
        // 9. PAST TRANSACTIONS LEDGER & FILTERS (ASYNC & VIRTUALIZED)
        // ══════════════════════════════════════════════════════
        private async void ApplyLedgerFilters()
        {
            if (!_isReady || icLedger == null) return;

            string search = txtLedgerSearch?.Text?.Trim() ?? "";
            string type = (cmbLedgerType?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Types";
            string status = (cmbLedgerStatus?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Statuses";

            DateTime? fromDate = null;
            DateTime? toDate = null;

            if (cmbLedgerDate?.SelectedItem is ComboBoxItem datePreset)
            {
                string choice = datePreset.Content.ToString()!;
                DateTime today = DateTime.Today;

                if (choice == "Today")
                {
                    fromDate = today;
                    toDate = today;
                }
                else if (choice == "This Week")
                {
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    fromDate = today.AddDays(-1 * diff);
                    toDate = today;
                }
                else if (choice == "This Month")
                {
                    fromDate = new DateTime(today.Year, today.Month, 1);
                    toDate = today;
                }
                else if (choice == "Custom Range")
                {
                    fromDate = dpLedgerFromDate?.SelectedDate;
                    toDate = dpLedgerToDate?.SelectedDate;
                }
            }

            // ⚡ Run database query in the background so the UI never freezes!
            var transactions = await Task.Run(() => SalesService.GetTransactions(search, type, status, fromDate, toDate));
            _allTransactions = transactions;
            icLedger.ItemsSource = _allTransactions;

            if (pnlEmptyLedger != null)
            {
                pnlEmptyLedger.Visibility = _allTransactions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void LedgerFilter_Changed(object sender, EventArgs e)
        {
            if (!_isReady) return;

            // Debounce search keystrokes (wait 300ms after user pauses typing)
            if (sender == txtLedgerSearch)
            {
                if (_ledgerSearchTimer == null)
                {
                    _ledgerSearchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    _ledgerSearchTimer.Tick += (s, args) =>
                    {
                        _ledgerSearchTimer.Stop();
                        ApplyLedgerFilters();
                    };
                }
                _ledgerSearchTimer.Stop();
                _ledgerSearchTimer.Start();
                return;
            }

            ApplyLedgerFilters();
        }

        private void LedgerDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLedgerDates || !_isReady) return;

            if (cmbLedgerDate != null && cmbLedgerDate.SelectedIndex != 4)
            {
                _isUpdatingLedgerDates = true;
                cmbLedgerDate.SelectedIndex = 4; // Auto-switch to "Custom Range"
                _isUpdatingLedgerDates = false;
            }
            ApplyLedgerFilters();
        }

        private void BtnResetLedgerFilter_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingLedgerDates = true;
            if (txtLedgerSearch != null) txtLedgerSearch.Text = "";
            if (cmbLedgerType != null) cmbLedgerType.SelectedIndex = 0;
            if (cmbLedgerStatus != null) cmbLedgerStatus.SelectedIndex = 0;
            if (cmbLedgerDate != null) cmbLedgerDate.SelectedIndex = 0; // All Time
            if (dpLedgerFromDate != null) dpLedgerFromDate.SelectedDate = null;
            if (dpLedgerToDate != null) dpLedgerToDate.SelectedDate = null;
            _isUpdatingLedgerDates = false;
            ApplyLedgerFilters();
        }

        private void LedgerRow_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as Border)?.Tag is TransactionItem item)
            {
                ShowReceiptModal(item.InvoiceNo, item.TransactionType);
            }
        }

        private void BtnExportLedger_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                FileName = $"BikeKings_Transactions_{DateTime.Now:yyyyMMdd}.csv",
                DefaultExt = ".csv",
                Filter = "CSV Spreadsheet (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (saveDialog.ShowDialog() == true)
            {
                using (var sw = new StreamWriter(saveDialog.FileName, false, new System.Text.UTF8Encoding(true)))
                {
                    sw.WriteLine("InvoiceNo,Type,Date,Customer,Cashier,Mechanic,PaymentMethod,TotalAmount,Status");
                    foreach (var item in _allTransactions)
                    {
                        sw.WriteLine($"\"{item.InvoiceNo}\",\"{item.TransactionType}\",\"{item.FormattedDate}\",\"{item.CustomerName}\",\"{item.CashierName}\",\"{item.MechanicName}\",\"{item.PaymentMethod}\",{item.TotalAmount},\"{item.Status}\"");
                    }
                }

                MessageBox.Show($"✅ Exported {_allTransactions.Count} transactions successfully to:\n{saveDialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ══════════════════════════════════════════════════════
        // 10. OFFICIAL RECEIPT REPRINT MODAL
        // ══════════════════════════════════════════════════════
        private void BtnReprintReceipt_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as TransactionItem ?? (sender as Button)?.DataContext as TransactionItem;
            if (item != null)
            {
                ShowReceiptModal(item.InvoiceNo, item.TransactionType);
            }
        }


        private void ShowReceiptModal(string invoiceNo, string type)
        {
            _activeReceipt = SalesService.GetReceiptDetails(invoiceNo, type);
            if (_activeReceipt == null) return;

            lblReceiptInvoiceNo.Text = $"NO: {_activeReceipt.InvoiceNo}";
            lblReceiptDate.Text = $"Date: {_activeReceipt.FormattedDate}";
            lblReceiptCustomer.Text = _activeReceipt.CustomerName;
            lblReceiptContact.Text = string.IsNullOrWhiteSpace(_activeReceipt.ContactNumber) ? "N/A" : _activeReceipt.ContactNumber;

            lblReceiptType.Text = _activeReceipt.TransactionType == "JobOrder" ? "Job Order (Service & Parts)" : "Retail Sale (Counter Purchase)";
            lblReceiptCashier.Text = _activeReceipt.CashierName;

            if (_activeReceipt.TransactionType == "JobOrder")
            {
                lblReceiptVehicle.Text = $"{_activeReceipt.MotorcycleUnit} ({_activeReceipt.PlateNumber})";
                lblReceiptMechanic.Text = _activeReceipt.MechanicName;
            }
            else
            {
                lblReceiptVehicle.Text = "Walk-in Retail Parts";
                lblReceiptMechanic.Text = "N/A (Retail Store)";
            }

            if (!string.IsNullOrWhiteSpace(_activeReceipt.PaymentRef) && !_activeReceipt.PaymentRef.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                lblReceiptPayment.Text = $"[X] {_activeReceipt.PaymentMethod}  (Ref: {_activeReceipt.PaymentRef})";
            }
            else
            {
                lblReceiptPayment.Text = $"[X] {_activeReceipt.PaymentMethod}";
            }

            if (_activeReceipt.Status.Equals("Voided", StringComparison.OrdinalIgnoreCase))
            {
                if (pnlVoidWatermark != null) pnlVoidWatermark.Visibility = Visibility.Visible;
                if (lblReceiptStatus != null)
                {
                    lblReceiptStatus.Text = "VOIDED / TRANSACTION CANCELLED";
                    lblReceiptStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#DC2626")!;
                }
            }
            else
            {
                if (pnlVoidWatermark != null) pnlVoidWatermark.Visibility = Visibility.Collapsed;
                if (lblReceiptStatus != null)
                {
                    lblReceiptStatus.Text = "COMPLETED / FULLY PAID";
                    lblReceiptStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#16A34A")!;
                }
            }

            if (lblReceiptSubtotal != null) lblReceiptSubtotal.Text = _activeReceipt.FormattedSubtotal;
            if (lblReceiptDiscount != null) lblReceiptDiscount.Text = _activeReceipt.FormattedDiscount;
            if (lblReceiptGrandTotal != null) lblReceiptGrandTotal.Text = _activeReceipt.FormattedTotal;

            if (icReceiptItems != null) icReceiptItems.ItemsSource = _activeReceipt.Items;

            modalReceiptOverlay.Visibility = Visibility.Visible;
        }

        private void CloseReceiptModal_Click(object sender, RoutedEventArgs e)
        {
            modalReceiptOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnPrintSavePdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintVisual(pnlPrintSlip, $"BikeKings_Invoice_{_activeReceipt?.InvoiceNo ?? "Receipt"}");
                    MessageBox.Show("✅ Sent to printer / saved as PDF successfully!", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print Error: {ex.Message}", "Print Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportSingleInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (_activeReceipt == null) return;

            var saveDialog = new SaveFileDialog
            {
                FileName = $"BikeKings_Invoice_{_activeReceipt.InvoiceNo}.csv",
                DefaultExt = ".csv",
                Filter = "CSV Spreadsheet (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (saveDialog.ShowDialog() == true)
            {
                using (var sw = new StreamWriter(saveDialog.FileName, false, new System.Text.UTF8Encoding(true)))   
                {
                    sw.WriteLine("BIKE KINGS CYCLE & MOTOR REPAIR SHOP");
                    sw.WriteLine($"Invoice Ref,{_activeReceipt.InvoiceNo}");
                    sw.WriteLine($"Date,{_activeReceipt.FormattedDate}");
                    sw.WriteLine($"Customer,{_activeReceipt.CustomerName}");
                    sw.WriteLine($"Motorcycle,{_activeReceipt.MotorcycleUnit} ({_activeReceipt.PlateNumber})");
                    sw.WriteLine($"Cashier,{_activeReceipt.CashierName}");
                    sw.WriteLine($"Mechanic,{_activeReceipt.MechanicName}");
                    sw.WriteLine($"Payment Method,{_activeReceipt.PaymentMethod}");
                    sw.WriteLine();
                    sw.WriteLine("Description,Quantity,Unit Price,Amount");
                    foreach (var itm in _activeReceipt.Items)
                    {
                        sw.WriteLine($"\"{itm.Description}\",{itm.Quantity},{itm.UnitPrice},{itm.Subtotal}");
                    }
                    sw.WriteLine();
                    sw.WriteLine($"Subtotal,,,{_activeReceipt.Subtotal}");
                    sw.WriteLine($"Discount,,,{_activeReceipt.Discount}");
                    sw.WriteLine($"Grand Total,,,{_activeReceipt.TotalAmount}");
                }

                MessageBox.Show($"✅ Exported invoice to:\n{saveDialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnVoid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is TransactionItem item)
            {
                if (item.Status == "Voided")
                {
                    MessageBox.Show("This transaction has already been voided.", "Void Transaction", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var confirm = MessageBox.Show($"Are you sure you want to VOID transaction #{item.InvoiceNo} ({item.FormattedTotal})?\n\nThis will reverse the sale and record an audit log.", "Confirm Void", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    int uid = SessionHelper.CurrentUser?.UserID ?? 2;
                    string uname = SessionHelper.CurrentUser?.Username ?? "cashier";
                    string role = SessionHelper.CurrentUser?.Role ?? "Cashier";

                    if (SalesService.VoidTransaction(item.InvoiceNo, item.TransactionType, "Voided at POS Register", uid, uname, role))
                    {
                        MessageBox.Show($"Transaction #{item.InvoiceNo} successfully voided.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        ApplyLedgerFilters();
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 11. REGISTER & AUDIT BUTTONS (NO SALE, CASH DROP, X/Z)
        // ══════════════════════════════════════════════════════
        private void BtnNoSale_Click(object sender, RoutedEventArgs e)
        {
            int uid = SessionHelper.CurrentUser?.UserID ?? 2;
            string uname = SessionHelper.CurrentUser?.Username ?? "cashier";
            string role = SessionHelper.CurrentUser?.Role ?? "Cashier";

            AuditLogService.Log("NO_SALE", "Cash drawer opened for change/inspection.", uid, uname, role);
            MessageBox.Show("💵 Cash Drawer Opened ('No Sale' logged to audit history).", "No Sale", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCashDrop_Click(object sender, RoutedEventArgs e)
        {
            int uid = SessionHelper.CurrentUser?.UserID ?? 2;
            string uname = SessionHelper.CurrentUser?.Username ?? "cashier";
            string role = SessionHelper.CurrentUser?.Role ?? "Cashier";

            AuditLogService.Log("CASH_DROP", "Cash Drop executed to secure excess register cash.", uid, uname, role);
            MessageBox.Show("💸 Cash Drop recorded in audit history.", "Cash Drop", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnXReport_Click(object sender, RoutedEventArgs e) => ShowAuditModal("X-Report (Mid-Day Audit)");
        private void BtnZReport_Click(object sender, RoutedEventArgs e) => ShowAuditModal("Z-Report (End-of-Day Settlement)");

        private void ShowAuditModal(string reportType)
        {
            string cashierName = SessionHelper.CurrentUser?.FullName ?? "Ruby";
            var report = CashierService.GenerateDailyAudit(reportType, cashierName);

            lblAuditTitle.Text = report.ReportType;
            lblAuditGenerated.Text = $"Generated: {report.FormattedDate}";
            lblAuditCashier.Text = $"Cashier: {report.CashierName}";
            lblAuditOrdersCount.Text = report.TotalTransactions.ToString();
            lblAuditRetailCount.Text = report.RetailSalesCount.ToString();
            lblAuditJobCount.Text = report.JobOrdersCount.ToString();
            lblAuditVoidCount.Text = report.VoidedCount.ToString();
            lblAuditCash.Text = report.FormattedCashSales;
            lblAuditGCash.Text = report.FormattedGCashSales;
            lblAuditMaya.Text = report.FormattedMayaSales;
            lblAuditNetRev.Text = report.FormattedNetRevenue;

            modalAuditOverlay.Visibility = Visibility.Visible;
        }

        private void CloseAuditModal_Click(object sender, RoutedEventArgs e)
        {
            modalAuditOverlay.Visibility = Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════════════
        // 12. LOGOUT
        // ══════════════════════════════════════════════════════
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show("Are you sure you want to log out of the POS terminal?", "Confirm Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                SessionHelper.Clear();
                var login = new LoginWindow();
                login.Show();
                this.Close();
            }
        }
    }
}