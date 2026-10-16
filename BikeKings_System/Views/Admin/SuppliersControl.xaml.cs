#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using BikeKings_System.Database;

namespace BikeKings_System.Views.Admin
{
    public partial class SuppliersControl : UserControl
    {
        private int _editingSupplierId = 0;
        private SupplierRecord _selectedDetailSupplier = null;
        private bool _isInitialized = false;

        // ── REGEX PATTERNS ──
        private static readonly Regex _phoneInputRegex = new Regex(@"^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _emailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex _nameRegex = new Regex(@"^[a-zA-Z0-9\s.,&'\-()/#+]{2,100}$", RegexOptions.Compiled);
        private static readonly Regex _contactPersonRegex = new Regex(@"^[\p{L}\s.,'\-]{2,60}$", RegexOptions.Compiled);

        public SuppliersControl()
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this) || Application.Current == null)
                return;

            _isInitialized = true;
            LoadData();
        }

        public void LoadData()
        {
            if (!_isInitialized || dgSuppliers == null) return;

            string search = txtSearch?.Text?.Trim() ?? "";
            var suppliers = SupplierService.GetSuppliers(search) ?? new List<SupplierRecord>();
            dgSuppliers.ItemsSource = suppliers;

            // ── Calculate Supplier KPI Stats ──
            int activeCount = suppliers.Count(s => s.IsActive);
            if (lblActiveSuppliers != null) lblActiveSuppliers.Text = $"{activeCount} Active Partners";

            var pos = InventoryService.GetPurchaseOrders() ?? new List<PurchaseOrderItem>();
            int pendingCount = pos.Count(p => p.IsPending);
            decimal pendingValue = pos.Where(p => p.IsPending).Sum(p => p.TotalAmount);
            if (lblActivePOCount != null) lblActivePOCount.Text = $"{pendingCount} Pending Orders";
            if (lblActivePOValue != null) lblActivePOValue.Text = $"₱{pendingValue:N2} pending procurement";

            var allProds = InventoryService.GetProducts() ?? new List<ProductItem>();
            if (lblTotalCatalogItems != null) lblTotalCatalogItems.Text = $"{allProds.Count} Catalog SKUs";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            LoadData();
        }

        // =================================================================
        // ── INTERACTIVE ROW CLICK: SINGLE CLICK OR DOUBLE CLICK ──────────
        // =================================================================
        private void DgSuppliers_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while (dep != null && dep != dgSuppliers)
            {
                if (dep is Button) return;
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dgSuppliers != null && dgSuppliers.SelectedItem is SupplierRecord s)
            {
                OpenSupplierDetailsModal(s);
            }
        }

        private void DgSuppliers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgSuppliers != null && dgSuppliers.SelectedItem is SupplierRecord s)
            {
                OpenSupplierDetailsModal(s);
            }
        }

        private void OpenSupplierDetailsModal(SupplierRecord s)
        {
            if (s == null || modalSupplierDetails == null) return;
            _selectedDetailSupplier = s;

            detailSupplierTitle.Text = s.SupplierName;
            detailStatusText.Text = s.StatusText;
            detailStatusBadge.Background = (Brush)new BrushConverter().ConvertFrom(s.StatusBgColor);
            detailStatusText.Foreground = (Brush)new BrushConverter().ConvertFrom(s.StatusTextColor);

            // Clean fallback handling (avoids raw '-' displaying)
            detailContactPerson.Text = string.IsNullOrWhiteSpace(s.ContactPerson) || s.ContactPerson.Trim() == "-" ? "Not Specified" : s.ContactPerson;
            detailPhone.Text = s.PhoneFormatted;
            detailEmail.Text = string.IsNullOrWhiteSpace(s.Email) || s.Email.Trim() == "-" ? "No email provided" : s.Email;
            detailAddress.Text = string.IsNullOrWhiteSpace(s.Address) || s.Address.Trim() == "-" ? "No address registered" : s.Address;

            // Load Purchase Orders associated with this supplier
            var allPOs = InventoryService.GetPurchaseOrders() ?? new List<PurchaseOrderItem>();
            var supplierPOs = allPOs.Where(p => p.SupplierID == s.SupplierID || string.Equals(p.SupplierName?.Trim(), s.SupplierName?.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            detailTotalPOs.Text = $"{supplierPOs.Count} Orders";
            decimal totalSpend = supplierPOs.Sum(p => p.TotalAmount);
            detailTotalSpend.Text = $"₱{totalSpend:N2}";
            int pendingPOs = supplierPOs.Count(p => p.IsPending);
            detailPendingPOs.Text = $"{pendingPOs} Awaiting Review";

            // Clean toggle between DataGrid and empty notice
            if (supplierPOs.Count > 0)
            {
                dgSupplierPOs.ItemsSource = supplierPOs;
                dgSupplierPOs.Visibility = Visibility.Visible;
                if (pnlNoPOsNotice != null) pnlNoPOsNotice.Visibility = Visibility.Collapsed;
            }
            else
            {
                dgSupplierPOs.ItemsSource = null;
                dgSupplierPOs.Visibility = Visibility.Collapsed;
                if (pnlNoPOsNotice != null) pnlNoPOsNotice.Visibility = Visibility.Visible;
            }

            modalSupplierDetails.Visibility = Visibility.Visible;
        }

        private void BtnCloseSupplierDetails_Click(object sender, RoutedEventArgs e)
        {
            if (modalSupplierDetails != null) modalSupplierDetails.Visibility = Visibility.Collapsed;
        }

        private void BtnEditFromDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDetailSupplier != null)
            {
                modalSupplierDetails.Visibility = Visibility.Collapsed;
                OpenEditSupplierModal(_selectedDetailSupplier);
            }
        }

        // =================================================================
        // ── ADD / EDIT SUPPLIER FORM & VALIDATION ────────────────────────
        // =================================================================
        private void ClearModalErrors()
        {
            var defaultBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2236"));
            if (bdrModalName != null) bdrModalName.BorderBrush = defaultBorder;
            if (bdrModalContact != null) bdrModalContact.BorderBrush = defaultBorder;
            if (bdrModalPhone != null) bdrModalPhone.BorderBrush = defaultBorder;
            if (bdrModalEmail != null) bdrModalEmail.BorderBrush = defaultBorder;

            if (errModalName != null) errModalName.Visibility = Visibility.Collapsed;
            if (errModalContact != null) errModalContact.Visibility = Visibility.Collapsed;
            if (errModalPhone != null) errModalPhone.Visibility = Visibility.Collapsed;
            if (errModalEmail != null) errModalEmail.Visibility = Visibility.Collapsed;
        }

        private void TxtModalInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var defaultBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2236"));

            if (sender == txtModalName && errModalName != null && errModalName.Visibility == Visibility.Visible)
            {
                if (!string.IsNullOrWhiteSpace(txtModalName.Text) && _nameRegex.IsMatch(txtModalName.Text.Trim()))
                {
                    errModalName.Visibility = Visibility.Collapsed;
                    bdrModalName.BorderBrush = defaultBorder;
                }
            }
            else if (sender == txtModalContact && errModalContact != null && errModalContact.Visibility == Visibility.Visible)
            {
                if (string.IsNullOrWhiteSpace(txtModalContact.Text) || _contactPersonRegex.IsMatch(txtModalContact.Text.Trim()))
                {
                    errModalContact.Visibility = Visibility.Collapsed;
                    bdrModalContact.BorderBrush = defaultBorder;
                }
            }
            else if (sender == txtModalPhone && errModalPhone != null && errModalPhone.Visibility == Visibility.Visible)
            {
                string raw = txtModalPhone.Text?.Trim() ?? "";
                string digits = Regex.Replace(raw, @"\D", "");
                bool isAllSameDigit = digits.Length > 0 && digits.Distinct().Count() == 1;

                if (!isAllSameDigit && (digits.Length == 11 && digits.StartsWith("09") || (digits.Length >= 8 && digits.Length <= 10 && digits.StartsWith("0"))))
                {
                    errModalPhone.Visibility = Visibility.Collapsed;
                    bdrModalPhone.BorderBrush = defaultBorder;
                }
            }
            else if (sender == txtModalEmail && errModalEmail != null && errModalEmail.Visibility == Visibility.Visible)
            {
                if (string.IsNullOrWhiteSpace(txtModalEmail.Text) || _emailRegex.IsMatch(txtModalEmail.Text.Trim()))
                {
                    errModalEmail.Visibility = Visibility.Collapsed;
                    bdrModalEmail.BorderBrush = defaultBorder;
                }
            }
        }

        private void TxtPhone_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // STRICTLY NUMBERS ONLY: Blocks dashes, spaces, symbols, and letters as you type
            e.Handled = !_phoneInputRegex.IsMatch(e.Text);
        }

        private void BtnAddSupplier_Click(object sender, RoutedEventArgs e)
        {
            _editingSupplierId = 0;
            lblSupplierModalTitle.Text = "Add Supplier Record";
            txtModalName.Clear();
            txtModalContact.Clear();
            txtModalPhone.Clear();
            txtModalEmail.Clear();
            txtModalAddress.Clear();
            ClearModalErrors();
            modalSupplier.Visibility = Visibility.Visible;
        }

        private void BtnEditSupplier_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is SupplierRecord s)
            {
                OpenEditSupplierModal(s);
            }
        }

        private void OpenEditSupplierModal(SupplierRecord s)
        {
            if (s == null) return;
            _editingSupplierId = s.SupplierID;
            lblSupplierModalTitle.Text = $"Edit Supplier: {s.SupplierName}";
            txtModalName.Text = s.SupplierName;
            txtModalContact.Text = s.ContactPerson == "-" ? "" : s.ContactPerson;

            // Load pure numbers into phone input box
            string digitsOnly = Regex.Replace(s.Phone ?? "", @"\D", "");
            txtModalPhone.Text = !string.IsNullOrEmpty(digitsOnly) ? digitsOnly : (s.Phone ?? "");

            txtModalEmail.Text = s.Email == "-" ? "" : s.Email;
            txtModalAddress.Text = s.Address == "-" ? "" : s.Address;
            ClearModalErrors();
            modalSupplier.Visibility = Visibility.Visible;
        }

        private void BtnCloseSupplierModal_Click(object sender, RoutedEventArgs e)
        {
            modalSupplier.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveSupplier_Click(object sender, RoutedEventArgs e)
        {
            ClearModalErrors();
            bool hasError = false;

            string name = txtModalName.Text?.Trim() ?? "";
            string contact = txtModalContact.Text?.Trim() ?? "";
            string phone = txtModalPhone.Text?.Trim() ?? "";
            string email = txtModalEmail.Text?.Trim() ?? "";
            string address = txtModalAddress.Text?.Trim() ?? "";

            // 1. Validate Company Name (Required, 2-100 characters)
            if (string.IsNullOrWhiteSpace(name) || !_nameRegex.IsMatch(name))
            {
                errModalName.Text = "Enter a valid company name (2-100 characters).";
                errModalName.Visibility = Visibility.Visible;
                bdrModalName.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                hasError = true;
            }
            else if (_editingSupplierId == 0)
            {
                var existing = SupplierService.GetSuppliers("") ?? new List<SupplierRecord>();
                if (existing.Any(s => string.Equals(s.SupplierName.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                {
                    errModalName.Text = "A supplier with this company name already exists.";
                    errModalName.Visibility = Visibility.Visible;
                    bdrModalName.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    hasError = true;
                }
            }

            // 2. Validate Contact Person (Optional, letters & spaces only)
            if (!string.IsNullOrWhiteSpace(contact) && !_contactPersonRegex.IsMatch(contact))
            {
                errModalContact.Text = "Enter a valid person's name (letters, spaces, periods only).";
                errModalContact.Visibility = Visibility.Visible;
                bdrModalContact.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                hasError = true;
            }

            // 3. Validate Phone Number (Required, pure numbers: 11-digit mobile 09XXXXXXXXX or 8-10 digit landline)
            string digitsOnly = Regex.Replace(phone, @"\D", "");
            bool isAllSameDigit = digitsOnly.Length > 0 && digitsOnly.Distinct().Count() == 1;
            bool isValidPhone = !isAllSameDigit && (
                (digitsOnly.Length == 11 && digitsOnly.StartsWith("09")) ||
                (digitsOnly.Length >= 8 && digitsOnly.Length <= 10 && digitsOnly.StartsWith("0"))
            );

            if (string.IsNullOrWhiteSpace(phone) || !isValidPhone)
            {
                errModalPhone.Text = "Please enter an 11-digit mobile number (e.g., 09770193750).";
                errModalPhone.Visibility = Visibility.Visible;
                bdrModalPhone.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                hasError = true;
            }

            // 4. Validate Email Address (Optional, valid format)
            if (!string.IsNullOrWhiteSpace(email) && !_emailRegex.IsMatch(email))
            {
                errModalEmail.Text = "Enter a valid email address (e.g., supply@company.com).";
                errModalEmail.Visibility = Visibility.Visible;
                bdrModalEmail.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                hasError = true;
            }

            if (hasError)
            {
                MessageBox.Show("Please correct the highlighted fields with valid information.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var item = new SupplierRecord
            {
                SupplierID = _editingSupplierId,
                SupplierName = name,
                ContactPerson = string.IsNullOrWhiteSpace(contact) ? "-" : contact,
                Phone = digitsOnly,
                Email = string.IsNullOrWhiteSpace(email) ? "-" : email,
                Address = string.IsNullOrWhiteSpace(address) ? "-" : address
            };

            if (SupplierService.SaveSupplier(item, out string err))
            {
                string action = _editingSupplierId == 0 ? "Added new" : "Updated";
                AuditLogService.Log("SUPPLIERS", $"{action} supplier '{item.SupplierName}'.");

                modalSupplier.Visibility = Visibility.Collapsed;
                LoadData();
            }
            else
            {
                MessageBox.Show($"Error saving supplier: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SupplierRecord s)
            {
                string action = s.IsActive ? "deactivate" : "activate";
                if (MessageBox.Show($"Are you sure you want to {action} '{s.SupplierName}'?", "Confirm Status Change", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (SupplierService.ToggleStatus(s.SupplierID, s.IsActive))
                    {
                        AuditLogService.Log("SUPPLIERS", $"Changed status of supplier '{s.SupplierName}' to {(!s.IsActive ? "ACTIVE" : "INACTIVE")}.");
                        LoadData();
                    }
                }
            }
        }

        // =================================================================
        // ── SUPPLIED ITEMS CATALOG MODAL ─────────────────────────────────
        // =================================================================
        private void BtnViewSuppliedItems_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is SupplierRecord s)
            {
                lblSuppliedSupplierName.Text = $"Supplier Partner: {s.SupplierName}";
                var allProds = InventoryService.GetProducts() ?? new List<ProductItem>();

                // Match products by their real SupplierID in the database
                var matched = allProds.Where(p => p.SupplierID == s.SupplierID).ToList();
                if (matched.Count == 0)
                {
                    matched = allProds.Where(p =>
                        (!string.IsNullOrWhiteSpace(p.BrandName) && s.SupplierName.Contains(p.BrandName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(s.SupplierName) && p.ProductName.Contains(s.SupplierName, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                }
                icSuppliedProducts.ItemsSource = matched;
                modalSuppliedItems.Visibility = Visibility.Visible;
            }
        }

        private void SuppliedItemRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is ProductItem product)
            {
                modalSuppliedItems.Visibility = Visibility.Collapsed;

                var dashboard = Window.GetWindow(this) as AdminDashboardWindow;
                if (dashboard != null)
                {
                    dashboard.NavigateToInventoryProduct(product.Barcode);
                }
            }
        }

        private void BtnCloseSuppliedItems_Click(object sender, RoutedEventArgs e)
        {
            modalSuppliedItems.Visibility = Visibility.Collapsed;
        }

        // =================================================================
        // ── EXPORT SUPPLIER DIRECTORY ────────────────────────────────────
        // =================================================================
        private void BtnExportSuppliers_Click(object sender, RoutedEventArgs e)
        {
            var list = SupplierService.GetSuppliers(txtSearch?.Text?.Trim() ?? "") ?? new List<SupplierRecord>();
            if (list.Count == 0)
            {
                MessageBox.Show("No supplier records available to export.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                FileName = $"BikeKings_Suppliers_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using var sw = new StreamWriter(sfd.FileName, false, new System.Text.UTF8Encoding(true));
                    sw.WriteLine("\"Company Name\",\"Contact Person\",\"Phone\",\"Email\",\"Address\",\"Status\"");
                    foreach (var s in list)
                    {
                        sw.WriteLine($"\"{s.SupplierName}\",\"{s.ContactPerson}\",\"{s.PhoneFormatted}\",\"{s.Email}\",\"{s.Address}\",\"{s.StatusText}\"");
                    }
                    MessageBox.Show("Supplier directory exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}