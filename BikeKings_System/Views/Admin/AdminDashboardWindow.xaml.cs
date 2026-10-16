using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using BikeKings_System.Database;
using BikeKings_System.Helpers;

namespace BikeKings_System.Views.Admin
{
    public partial class AdminDashboardWindow : Window
    {
        private DispatcherTimer? _timer;
        private string _currentPeriod = "Daily";
        private DateTime? _customFrom = null;
        private DateTime? _customTo = null;
        private int _currentCriticalProdId = 0;
        private string _currentSelectedMech = "";

        public AdminDashboardWindow()
        {
            InitializeComponent();
            StartClock();

            dpFromDate.SelectedDate = DateTime.Now.Date.AddDays(-7);
            dpToDate.SelectedDate = DateTime.Now.Date;

            LoadLiveDashboardData();
        }

        private void StartClock()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                lblDateTime.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy | h:mm:ss tt");
            };
            _timer.Start();

            if (SessionHelper.CurrentUser != null)
            {
                lblUserName.Text = SessionHelper.CurrentUser.FullName;
                if (!string.IsNullOrEmpty(SessionHelper.CurrentUser.FullName))
                {
                    lblAvatarInitial.Text = SessionHelper.CurrentUser.FullName.Substring(0, 1).ToUpper();
                }
            }
        }

        public void RefreshCurrentUserHeader()
        {
            if (SessionHelper.CurrentUser != null)
            {
                lblUserName.Text = SessionHelper.CurrentUser.FullName;
                if (!string.IsNullOrEmpty(SessionHelper.CurrentUser.FullName))
                {
                    lblAvatarInitial.Text = SessionHelper.CurrentUser.FullName.Substring(0, 1).ToUpper();
                }
            }
        }

        public void LoadLiveDashboardData()
        {
            var stats = DashboardService.GetDashboardStats(_currentPeriod, _customFrom, _customTo);

            // 1. KPI Cards
            lblTotalRevenue.Text = $"₱{stats.TotalRevenue:N2}";
            lblGrossProfit.Text = $"₱{stats.GrossProfit:N2}";
            lblMargin.Text = $"{stats.ProfitMargin:F1}% Margin";
            lblTransactions.Text = stats.TotalTransactions.ToString();

            // 2. Stock Health & Notification Badge
            lblLowStockCount.Text = stats.LowStockCount.ToString();
            lblNotifBadge.Text = $"{stats.LowStockCount} ALERTS";

            if (stats.LowStockCount > 0)
            {
                pnlStockAlertBadge.Background = (Brush)new BrushConverter().ConvertFromString("#3A151D")!;
                lblStockStatus.Text = $"⚠️ {stats.LowStockCount} Low / Out";
                lblStockStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF7D91")!;
                lblNotifBadge.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF7D91")!;
            }
            else
            {
                pnlStockAlertBadge.Background = (Brush)new BrushConverter().ConvertFromString("#1D2A20")!;
                lblStockStatus.Text = "✅ Normal Levels";
                lblStockStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#2ECC71")!;
                lblNotifBadge.Foreground = (Brush)new BrushConverter().ConvertFromString("#2ECC71")!;
            }

            // 3. Dynamic Top Categories Breakdown
            icTopCategories.ItemsSource = stats.TopCategories;

            // 4. Dynamic Sales Chart Bars
            string periodDisplay = _customFrom.HasValue && _customTo.HasValue
                ? $"{_customFrom.Value:MMM dd} - {_customTo.Value:MMM dd}"
                : _currentPeriod;
            lblChartStatus.Text = $"{stats.TotalTransactions} Completed Orders ({periodDisplay})";
            chartBarsControl.ItemsSource = stats.DailyTrend;

            // 5. Dynamic Stock Alerts (Live from Inventory Table)
            _currentCriticalProdId = stats.AlertProductID;
            icStockAlerts.ItemsSource = stats.StockAlertsList;

            // 6. Dynamic Mechanic Productivity (All Active Mechanics in System)
            icMechanicProductivity.ItemsSource = stats.MechanicsList;
        }

        // ── PERIOD SWITCHER (DAILY, WEEKLY, MONTHLY, ANNUALLY) ──
        public void Period_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag != null)
            {
                _currentPeriod = border.Tag.ToString()!;
                _customFrom = null;
                _customTo = null;

                ResetPeriodButtonStyles();

                border.Background = (Brush)new BrushConverter().ConvertFromString("#E63956")!;
                if (border.Child is TextBlock tb) tb.Foreground = Brushes.White;

                lblPeriodSub.Text = $"Here are {_currentPeriod.ToLower()}'s sales & service stats for Bike Kings Cycle Center";
                lblRevenueSub.Text = $"{_currentPeriod} Revenue";
                lblChartPeriod.Text = $"Period: {_currentPeriod.ToUpper()} (Parts & Labor Revenue)";

                LoadLiveDashboardData();
            }
        }

        private void ResetPeriodButtonStyles()
        {
            btnPeriodDaily.Background = (Brush)new BrushConverter().ConvertFromString("#1D1826")!;
            txtDaily.Foreground = (Brush)new BrushConverter().ConvertFromString("#8C849C")!;
            btnPeriodWeekly.Background = (Brush)new BrushConverter().ConvertFromString("#1D1826")!;
            txtWeekly.Foreground = (Brush)new BrushConverter().ConvertFromString("#8C849C")!;
            btnPeriodMonthly.Background = (Brush)new BrushConverter().ConvertFromString("#1D1826")!;
            txtMonthly.Foreground = (Brush)new BrushConverter().ConvertFromString("#8C849C")!;
            btnPeriodAnnual.Background = (Brush)new BrushConverter().ConvertFromString("#1D1826")!;
            txtAnnual.Foreground = (Brush)new BrushConverter().ConvertFromString("#8C849C")!;
        }

        // ── CUSTOM DATE RANGE FILTER HANDLERS ──
        public void BtnApplyDate_Click(object sender, RoutedEventArgs e)
        {
            if (!dpFromDate.SelectedDate.HasValue || !dpToDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Please choose both From Date and To Date.", "Select Date Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpFromDate.SelectedDate.Value > dpToDate.SelectedDate.Value)
            {
                MessageBox.Show("From Date cannot be later than To Date.", "Invalid Date Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _customFrom = dpFromDate.SelectedDate.Value;
            _customTo = dpToDate.SelectedDate.Value;
            _currentPeriod = "Custom";

            ResetPeriodButtonStyles();

            lblPeriodSub.Text = $"Showing sales & service stats from {_customFrom.Value:MMM dd, yyyy} to {_customTo.Value:MMM dd, yyyy}";
            lblRevenueSub.Text = "Custom Revenue";
            lblChartPeriod.Text = $"Period: {_customFrom.Value:MM/dd/yyyy} - {_customTo.Value:MM/dd/yyyy}";

            LoadLiveDashboardData();
        }

        public void BtnResetDate_Click(object sender, RoutedEventArgs e)
        {
            _customFrom = null;
            _customTo = null;
            _currentPeriod = "Daily";

            ResetPeriodButtonStyles();
            btnPeriodDaily.Background = (Brush)new BrushConverter().ConvertFromString("#E63956")!;
            txtDaily.Foreground = Brushes.White;

            lblPeriodSub.Text = "Here are today's sales & service stats for Bike Kings Cycle Center";
            lblRevenueSub.Text = "Daily Revenue";
            lblChartPeriod.Text = "Period: DAILY (Parts & Labor Revenue)";

            dpFromDate.SelectedDate = DateTime.Now.Date.AddDays(-7);
            dpToDate.SelectedDate = DateTime.Now.Date;

            LoadLiveDashboardData();
        }

        // ── NAVIGATION MENU HANDLERS ──
        private void HideAllViews()
        {
            if (pnlDashboardView != null) pnlDashboardView.Visibility = Visibility.Collapsed;
            if (pnlMaintenanceView != null) pnlMaintenanceView.Visibility = Visibility.Collapsed;
            if (pnlInventoryView != null) pnlInventoryView.Visibility = Visibility.Collapsed;
            if (pnlLogsView != null) pnlLogsView.Visibility = Visibility.Collapsed;
            if (pnlSuppliersView != null) pnlSuppliersView.Visibility = Visibility.Collapsed;
            if (pnlSalesView != null) pnlSalesView.Visibility = Visibility.Collapsed;
            if (pnlBackupView != null) pnlBackupView.Visibility = Visibility.Collapsed;
            if (pnlProfileView != null) pnlProfileView.Visibility = Visibility.Collapsed;
        }

        public void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                foreach (var child in pnlNavMenu.Children)
                {
                    if (child is Button b && b.Template != null)
                    {
                        var bdr = b.Template.FindName("btnBdr", b) as Border;
                        var txt = b.Template.FindName("btnTxt", b) as TextBlock;
                        if (bdr != null) bdr.Background = Brushes.Transparent;
                        if (txt != null) txt.Foreground = (Brush)new BrushConverter().ConvertFromString("#9C95AD")!;
                    }
                }

                var activeBdr = btn.Template.FindName("btnBdr", btn) as Border;
                var activeTxt = btn.Template.FindName("btnTxt", btn) as TextBlock;
                if (activeBdr != null) activeBdr.Background = (Brush)new BrushConverter().ConvertFromString("#E63956")!;
                if (activeTxt != null) activeTxt.Foreground = Brushes.White;

                HideAllViews();

                switch (btn.Name)
                {
                    case "navDashboard":
                        lblHeaderTitle.Text = "Dashboard";
                        pnlDashboardView.Visibility = Visibility.Visible;
                        LoadLiveDashboardData();
                        break;
                    case "navSales":
                        lblHeaderTitle.Text = "Sales & Job Orders";
                        pnlSalesView.Visibility = Visibility.Visible;
                        pnlSalesView.LoadData();
                        break;
                    case "navInventory":
                        lblHeaderTitle.Text = "Inventory";
                        pnlInventoryView.Visibility = Visibility.Visible;
                        pnlInventoryView.LoadData();
                        break;
                    case "navSuppliers":
                        lblHeaderTitle.Text = "Suppliers";
                        pnlSuppliersView.Visibility = Visibility.Visible;
                        pnlSuppliersView.LoadData();
                        break;
                    case "navLogs":
                        lblHeaderTitle.Text = "History Logs";
                        pnlLogsView.Visibility = Visibility.Visible;
                        pnlLogsView.LoadLogs();
                        break;
                    case "navMaintenance":
                        lblHeaderTitle.Text = "Maintenance";
                        pnlMaintenanceView.Visibility = Visibility.Visible;
                        pnlMaintenanceView.LoadAllData();
                        break;
                    case "navBackup":
                        lblHeaderTitle.Text = "Backup & Recovery";
                        if (pnlBackupView != null) pnlBackupView.Visibility = Visibility.Visible;
                        break;
                    case "navProfile":
                        lblHeaderTitle.Text = "Profile";
                        if (pnlProfileView != null)
                        {
                            pnlProfileView.Visibility = Visibility.Visible;
                            pnlProfileView.LoadProfileData();
                        }
                        break;
                }
            }
        }

        // ── IN-APP MODAL & JUMP HANDLERS ──
        public void NavigateToInventoryProduct(string barcode)
        {
            navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlInventoryView.FocusProduct(barcode);
        }

        public void NavigateToSalesOrder(string invoiceNo, string type)
        {
            navSales.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlSalesView.OpenOrder(invoiceNo, type);
        }
        public void OpenProductModalFromItem_Click(object sender, MouseButtonEventArgs e)
        {
            string sku = "";
            if (sender is FrameworkElement b && b.Tag != null)
            {
                string tagStr = b.Tag.ToString()!;
                var match = Regex.Match(tagStr, @"SKU:\s*([A-Za-z0-9\-_]+)");
                if (match.Success) sku = match.Groups[1].Value.Trim();
                else sku = tagStr.Trim();
            }

            if (!string.IsNullOrWhiteSpace(sku) && sku != "N/A")
            {
                navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                pnlInventoryView.FocusProduct(sku);
            }
            else
            {
                navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                pnlInventoryView.FilterByStockStatus("Low Stock");
            }
        }

        public void OpenProductModal_Click(object sender, MouseButtonEventArgs e)
        {
            OpenProductModalFromItem_Click(sender, e);
        }

        public void BtnGoToInventory_Click(object sender, RoutedEventArgs e)
        {
            string sku = lblModalProdSku.Text.Trim();
            CloseModal_Click(sender, e);
            navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!string.IsNullOrWhiteSpace(sku) && sku != "N/A")
            {
                pnlInventoryView.FocusProduct(sku);
            }
        }

        private MechanicModalDetails _currentMechDetails = null;

        public void OpenMechanicModal_Click(object sender, MouseButtonEventArgs e)
        {
            string mechName = "Mechanic";
            if (sender is FrameworkElement b && b.Tag != null)
            {
                mechName = b.Tag.ToString()!;
            }

            _currentSelectedMech = mechName;
            var details = DashboardService.GetMechanicModalDetails(mechName, _currentPeriod, _customFrom, _customTo);
            _currentMechDetails = details;

            lblModalMechTitle.Text = $"Mechanic Earnings: {details.MechanicName}";

            // Format Cutoff Period display
            if (_customFrom.HasValue && _customTo.HasValue)
                lblModalMechPeriod.Text = $"Cutoff: {_customFrom:MMM dd, yyyy} to {_customTo:MMM dd, yyyy}";
            else
                lblModalMechPeriod.Text = $"Cutoff Period: {_currentPeriod} ({DateTime.Now:MMM dd, yyyy})";

            lblModalMechJobs.Text = $"{details.JobsCompleted} jobs";
            lblModalMechLabor.Text = $"₱{details.TotalLabor:N2}";
            lblModalMechComm.Text = $"₱{details.Commission:N2}";
            lblModalMechShopShare.Text = $"₱{details.ShopShare:N2}";
            lblModalMechPayoutTitle.Text = $"MECHANIC EARNINGS ({details.CommissionRate:0}% PAYOUT)";
            lblModalShopShareTitle.Text = $"SHOP SHARE ({100 - details.CommissionRate:0}% RETAINED)";

            icModalMechJobs.ItemsSource = details.Jobs;
            txtModalMechNoJobs.Visibility = details.Jobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            modalOverlay.Visibility = Visibility.Visible;
            modalMechanic.Visibility = Visibility.Visible;
            modalProduct.Visibility = Visibility.Collapsed;
        }

        public void BtnExportMechPaySlip_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMechDetails == null || _currentMechDetails.Jobs.Count == 0)
            {
                MessageBox.Show("No completed jobs to export for this mechanic in the selected period.", "Pay Slip Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"PaySlip_{Regex.Replace(_currentMechDetails.MechanicName, @"\s+", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    // Helper to format values as text formulas so Excel never shows "########" or converts numbers/dates
                    string AsText(string val) => $"\"=\"\"{val.Replace("\"", "\"\"")}\"\"\"";

                    // Clean duplicated label from Cutoff Period
                    string cutoffText = (lblModalMechPeriod.Text ?? "")
                        .Replace("Cutoff Period: ", "")
                        .Replace("Cutoff: ", "")
                        .Trim();

                    using (var sw = new StreamWriter(sfd.FileName, false, new System.Text.UTF8Encoding(true)))
                    {
                        // 1. Report Header (Balanced 4-Column Layout)
                        sw.WriteLine("\"BIKE KINGS CYCLE & MOTOR REPAIR - MECHANIC COMMISSION PAY SLIP\"");
                        sw.WriteLine();
                        sw.WriteLine($"\"Mechanic Name:\",{AsText(_currentMechDetails.MechanicName)},\"Cutoff Period:\",{AsText(cutoffText)}");
                        sw.WriteLine($"\"Date Exported:\",{AsText(DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"))},\"Total Completed Jobs:\",\"{_currentMechDetails.JobsCompleted}\"");
                        sw.WriteLine($"\"Total Labor Generated:\",{AsText($"₱{_currentMechDetails.TotalLabor:N2}")},\"Commission Split:\",{AsText($"{_currentMechDetails.CommissionRate:0}% Mechanic / {100 - _currentMechDetails.CommissionRate:0}% Shop")}");
                        sw.WriteLine($"\"Shop Retained Share:\",{AsText($"₱{_currentMechDetails.ShopShare:N2}")},\"NET COMMISSION PAYOUT:\",{AsText($"₱{_currentMechDetails.Commission:N2}")}");
                        sw.WriteLine();

                        // 2. Itemized Table
                        sw.WriteLine("\"Job Order #\",\"Date & Time\",\"Customer & Vehicle\",\"Labor Fee (PHP)\",\"Mechanic Share (PHP)\"");

                        foreach (var job in _currentMechDetails.Jobs)
                        {
                            sw.WriteLine($"{AsText(job.JobOrderNo)},{AsText(job.FormattedDate)},\"{job.CustomerVehicle.Replace("\"", "\"\"")}\",{AsText($"₱{job.LaborCost:N2}")},{AsText($"₱{job.Commission:N2}")}");
                        }

                        // 3. Bottom Summary Total Row
                        sw.WriteLine();
                        sw.WriteLine($"\"TOTAL EARNINGS PAYOUT\",,\"\",{AsText($"₱{_currentMechDetails.TotalLabor:N2}")},{AsText($"₱{_currentMechDetails.Commission:N2}")}");
                    }

                    MessageBox.Show($"Pay slip for {_currentMechDetails.MechanicName} exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export pay slip: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void ModalJobOrder_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag != null)
            {
                string jobNo = elem.Tag.ToString()!;
                CloseModal_Click(sender, e);
                navSales.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                pnlSalesView.SetFilters(jobNo, "Job Orders", "Completed");
            }
        }

        public void BtnViewMechInSales_Click(object sender, RoutedEventArgs e)
        {
            CloseModal_Click(sender, e);
            navSales.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlSalesView.SetFilters(_currentSelectedMech, "Job Orders", "Completed", _customFrom ?? GetPeriodFromDate(), _customTo ?? DateTime.Now.Date);
        }

        public void CategoryItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag != null)
            {
                string catName = elem.Tag.ToString()!;
                if (catName.Contains("Labor", StringComparison.OrdinalIgnoreCase) || catName.Contains("Service", StringComparison.OrdinalIgnoreCase))
                {
                    navSales.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    pnlSalesView.SetFilters(null, "Job Orders", "Completed", _customFrom ?? GetPeriodFromDate(), _customTo ?? DateTime.Now.Date);
                }
                else
                {
                    navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    pnlInventoryView.FilterByCategory(catName);
                }
            }
        }

        public void CloseModal_Click(object sender, RoutedEventArgs e)
        {
            modalOverlay.Visibility = Visibility.Collapsed;
            modalProduct.Visibility = Visibility.Collapsed;
            modalMechanic.Visibility = Visibility.Collapsed;
        }

        // ── KPI CARD CLICK SHORTCUTS ──
        private DateTime GetPeriodFromDate()
        {
            DateTime now = DateTime.Now;
            switch (_currentPeriod.ToLower())
            {
                case "weekly":
                    int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    return now.Date.AddDays(-diff);
                case "monthly":
                    return new DateTime(now.Year, now.Month, 1);
                case "annually":
                case "annual":
                    return new DateTime(now.Year, 1, 1);
                default:
                    return now.Date;
            }
        }

        public void KpiRevenue_Click(object sender, MouseButtonEventArgs e)
        {
            navSales.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlSalesView.SetFilters(null, "All Types", "Completed", _customFrom ?? GetPeriodFromDate(), _customTo ?? DateTime.Now.Date);
        }

        public void KpiProfit_Click(object sender, MouseButtonEventArgs e)
        {
            navSales.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlSalesView.SetFilters(null, "All Types", "Completed", _customFrom ?? GetPeriodFromDate(), _customTo ?? DateTime.Now.Date);
        }

        public void KpiTransactions_Click(object sender, MouseButtonEventArgs e)
        {
            navSales.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlSalesView.SetFilters(null, "All Types", "Completed", _customFrom ?? GetPeriodFromDate(), _customTo ?? DateTime.Now.Date);
        }

        public void KpiStock_Click(object sender, MouseButtonEventArgs e)
        {
            navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlInventoryView.FilterByStockStatus("Low Stock");
        }

        // ── NOTIFICATION DROPDOWN CONTROLS ──
        public void NotificationBadge_Click(object sender, MouseButtonEventArgs e)
        {
            ToggleNotificationDropdown();
        }

        private void ToggleNotificationDropdown()
        {
            if (dropdownNotification.Visibility == Visibility.Visible)
            {
                dropdownNotification.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                var allProds = InventoryService.GetProducts() ?? new List<ProductItem>();
                var lowStockList = allProds
                    .Where(p => p.StockQuantity <= p.ReorderLevel)
                    .OrderBy(p => p.StockQuantity)
                    .ToList();

                icDropdownAlerts.ItemsSource = lowStockList;
                pnlNotifEmpty.Visibility = lowStockList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                lblNotifDropdownSubtitle.Text = lowStockList.Count > 0
                    ? $"{lowStockList.Count} product(s) require replenishment"
                    : "All inventory items well stocked";

                dropdownNotification.Visibility = Visibility.Visible;
            }
            catch { }
        }

        public void BtnCloseNotifDropdown_Click(object sender, RoutedEventArgs e)
        {
            dropdownNotification.Visibility = Visibility.Collapsed;
        }

        public void NotifItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag != null)
            {
                string barcode = elem.Tag.ToString()!;
                dropdownNotification.Visibility = Visibility.Collapsed;
                NavigateToInventoryProduct(barcode);
            }
        }

        public void BtnNotifGoToMasterlist_Click(object sender, RoutedEventArgs e)
        {
            dropdownNotification.Visibility = Visibility.Collapsed;
            navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlInventoryView.FilterByStockStatus("Low Stock");
        }

        public void BtnNotifCreatePO_Click(object sender, RoutedEventArgs e)
        {
            dropdownNotification.Visibility = Visibility.Collapsed;
            navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlInventoryView.ShowPurchaseOrdersTab();
        }

        public void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "CSV Spreadsheet (*.csv)|*.csv",
                    FileName = $"BikeKings_Dashboard_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() == true)
                {
                    var stats = DashboardService.GetDashboardStats(_currentPeriod, _customFrom, _customTo);

                    // UTF8Encoding(true) writes the UTF-8 BOM so Excel displays the ₱ symbol cleanly!
                    using var writer = new StreamWriter(sfd.FileName, false, new System.Text.UTF8Encoding(true));

                    // Title Header Block
                    writer.WriteLine("BIKE KINGS CYCLE & MOTOR REPAIR - DASHBOARD REPORT");
                    writer.WriteLine($"\"Export Date: {DateTime.Now:MMMM dd, yyyy hh:mm tt}\"");
                    writer.WriteLine($"\"Reporting Period: {_currentPeriod}\"");
                    writer.WriteLine("");

                    // Section 1: Financial & Store Metrics Table
                    writer.WriteLine("1. FINANCIAL & STORE SUMMARY");
                    writer.WriteLine("Metric,Value");
                    writer.WriteLine($"Total Revenue,₱{stats.TotalRevenue:N2}");
                    writer.WriteLine($"Gross Profit,₱{stats.GrossProfit:N2}");
                    writer.WriteLine($"Profit Margin,{stats.ProfitMargin:F1}%");
                    writer.WriteLine($"Total Transactions,{stats.TotalTransactions}");
                    writer.WriteLine($"Low Stock Items,{stats.LowStockCount}");
                    writer.WriteLine($"Out of Stock Items,{stats.OutOfStockCount}");
                    writer.WriteLine("");

                    // Section 2: Category Breakdown Table
                    writer.WriteLine("2. REVENUE BREAKDOWN BY CATEGORY");
                    writer.WriteLine("Category Name,Revenue (PHP),Share of Total (%)");
                    foreach (var cat in stats.TopCategories)
                    {
                        // Enclosed in quotes so names with commas don't split columns
                        writer.WriteLine($"\"{cat.CategoryName}\",₱{cat.Revenue:N2},{cat.Percentage:F1}%");
                    }
                    writer.WriteLine("");

                    // Section 3: Mechanic Productivity Table
                    writer.WriteLine("3. MECHANIC PERFORMANCE & COMMISSIONS");
                    writer.WriteLine("Mechanic Name,Jobs Completed,Total Labor Revenue (PHP),Mechanic Commission (PHP)");
                    foreach (var mech in stats.MechanicsList)
                    {
                        writer.WriteLine($"\"{mech.MechanicName}\",{mech.JobsCompleted},₱{mech.TotalLabor:N2},₱{mech.Commission:N2}");
                    }

                    MessageBox.Show("Dashboard summary successfully exported to CSV!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UserProfile_Click(object sender, MouseButtonEventArgs e)
        {
            navProfile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        public void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to log out of Bike Kings Management System?",
                "Confirm Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                if (SessionHelper.CurrentUser != null)
                {
                    AuditLogService.Log("LOGOUT", $"User {SessionHelper.CurrentUser.Username} logged out.",
                        SessionHelper.CurrentUser.UserID, SessionHelper.CurrentUser.Username, SessionHelper.CurrentUser.Role);
                }
                SessionHelper.Clear();
                var loginWin = new LoginWindow();
                loginWin.Show();
                this.Close();
            }
        }
    }
}