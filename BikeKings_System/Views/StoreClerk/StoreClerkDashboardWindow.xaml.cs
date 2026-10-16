#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BikeKings_System.Database;
using BikeKings_System.Helpers;
using BikeKings_System.Views;

namespace BikeKings_System.Views.StoreClerk
{
    public partial class StoreClerkDashboardWindow : Window
    {
        private DispatcherTimer _clockTimer;

        public StoreClerkDashboardWindow()
        {
            InitializeComponent();
            InitDashboard();
            StartClock();
        }

        private void InitDashboard()
        {
            if (SessionHelper.CurrentUser != null)
            {
                lblClerkName.Text = SessionHelper.CurrentUser.FullName;
                txtProfileName.Text = SessionHelper.CurrentUser.FullName;
                txtProfileUsername.Text = SessionHelper.CurrentUser.Username;
            }

            // Make inventory view visible at runtime
            pnlInventoryView.Visibility = Visibility.Visible;
            UpdateAlertsBadge();
        }

        private void StartClock()
        {
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) =>
            {
                lblDateTime.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy | h:mm:ss tt");
            };
            _clockTimer.Start();
            lblDateTime.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy | h:mm:ss tt");
        }
        private void UpdateAlertsBadge()
        {
            try
            {
                var stats = InventoryService.GetInventoryStatsSummary();
                int alertCount = stats != null ? stats.ReplenishmentNeededCount : 0;
                if (alertCount > 0)
                {
                    pnlStockAlertBadge.Background = (Brush)new BrushConverter().ConvertFromString("#3A151D");
                    if (lblStockStatus != null)
                    {
                        lblStockStatus.Text = $"⚠️ {alertCount} Low / Out";
                        lblStockStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF7D91");
                    }
                    lblNotifBadge.Text = $"{alertCount} ALERTS";
                    lblNotifBadge.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF7D91");
                }
                else
                {
                    pnlStockAlertBadge.Background = (Brush)new BrushConverter().ConvertFromString("#1D2A20");
                    if (lblStockStatus != null)
                    {
                        lblStockStatus.Text = "✅ Normal Levels";
                        lblStockStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#2ECC71");
                    }
                    lblNotifBadge.Text = "0 ALERTS";
                    lblNotifBadge.Foreground = (Brush)new BrushConverter().ConvertFromString("#2ECC71");
                }
            }
            catch { }
        }
        private void BtnTopExport_Click(object sender, RoutedEventArgs e)
        {
            pnlInventoryView?.BtnExportProductsExcel_Click(sender, e);
        }

        // ── NOTIFICATION DROPDOWN CONTROLS ──
        private void NotificationBadge_Click(object sender, MouseButtonEventArgs e)
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

        private void BtnCloseNotifDropdown_Click(object sender, RoutedEventArgs e)
        {
            dropdownNotification.Visibility = Visibility.Collapsed;
        }

        private void NotifItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag != null)
            {
                string barcode = elem.Tag.ToString()!;
                dropdownNotification.Visibility = Visibility.Collapsed;
                navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                pnlInventoryView.FocusProduct(barcode);
            }
        }

        private void BtnNotifGoToMasterlist_Click(object sender, RoutedEventArgs e)
        {
            dropdownNotification.Visibility = Visibility.Collapsed;
            navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlInventoryView.FilterByStockStatus("Low Stock");
        }

        private void BtnNotifCreatePO_Click(object sender, RoutedEventArgs e)
        {
            dropdownNotification.Visibility = Visibility.Collapsed;
            navInventory.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            pnlInventoryView.ShowPurchaseOrdersTab();
        }

        // ── SIDEBAR NAVIGATION CLICK ──
        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            dropdownNotification.Visibility = Visibility.Collapsed;

            if (sender is Button clickedBtn && clickedBtn.Tag != null)
            {
                ResetNavButtonStyles();

                var border = clickedBtn.Template.FindName("btnBdr", clickedBtn) as Border;
                var textBlock = clickedBtn.Template.FindName("btnTxt", clickedBtn) as TextBlock;
                if (border != null) border.Background = (Brush)new BrushConverter().ConvertFromString("#E63956");
                if (textBlock != null) textBlock.Foreground = Brushes.White;

                pnlInventoryView.Visibility = Visibility.Collapsed;
                pnlSuppliersView.Visibility = Visibility.Collapsed;
                pnlProfileView.Visibility = Visibility.Collapsed;

                string tab = clickedBtn.Tag.ToString();
                switch (tab)
                {
                    case "Inventory":
                        lblHeaderTitle.Text = "Inventory";
                        pnlInventoryView.Visibility = Visibility.Visible;
                        pnlInventoryView.LoadData();
                        UpdateAlertsBadge();
                        break;

                    case "Suppliers":
                        lblHeaderTitle.Text = "Suppliers";
                        pnlSuppliersView.Visibility = Visibility.Visible;
                        pnlSuppliersView.LoadData();
                        break;

                    case "Profile":
                        lblHeaderTitle.Text = "My Profile";
                        pnlProfileView.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void ResetNavButtonStyles()
        {
            Button[] buttons = { navInventory, navSuppliers, navProfile };
            foreach (var btn in buttons)
            {
                if (btn != null)
                {
                    var bdr = btn.Template.FindName("btnBdr", btn) as Border;
                    var txt = btn.Template.FindName("btnTxt", btn) as TextBlock;
                    if (bdr != null) bdr.Background = Brushes.Transparent;
                    if (txt != null) txt.Foreground = (Brush)new BrushConverter().ConvertFromString("#9C95AD");
                }
            }
        }

        // ── PROFILE: CHANGE PASSWORD ──
        private void BtnUpdatePassword_Click(object sender, RoutedEventArgs e)
        {
            string currentPw = pbCurrentPassword.Password.Trim();
            string newPw = pbNewPassword.Password.Trim();
            string confirmPw = pbConfirmPassword.Password.Trim();

            if (string.IsNullOrEmpty(currentPw) || string.IsNullOrEmpty(newPw) || string.IsNullOrEmpty(confirmPw))
            {
                MessageBox.Show("Please fill in all password fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPw.Length < 6)
            {
                MessageBox.Show("New password must be at least 6 characters long.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPw != confirmPw)
            {
                MessageBox.Show("New password and confirmation do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int userId = SessionHelper.CurrentUser?.UserID ?? 3;
            string username = SessionHelper.CurrentUser?.Username ?? "clerk";

            if (AuthService.ChangePassword(userId, currentPw, newPw))
            {
                AuditLogService.Log("CHANGE_PASSWORD", $"Inventory Manager '{username}' updated their password.", userId, username, "StoreClerk");
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

        // ── LOGOUT ──
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _clockTimer?.Stop();
            AuditLogService.Log("LOGOUT", $"User {SessionHelper.CurrentUser?.Username ?? "clerk"} logged out.");
            SessionHelper.Clear();
            LoginWindow login = new LoginWindow();
            login.Show();
            this.Close();
        }
    }
}