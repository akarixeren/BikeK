using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using BikeKings_System.Database;
using BikeKings_System.Helpers;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace BikeKings_System.Views.Admin
{
    public partial class SalesOrdersControl : UserControl
    {
        private List<TransactionItem> _allTransactions = new List<TransactionItem>();
        private ReceiptFullDetails? _activeReceipt = null;
        private bool _isReady = false;
        private bool _isUpdatingDates = false;
        private DispatcherTimer? _searchDebounceTimer;

        public SalesOrdersControl()
        {
            InitializeComponent();
            _isReady = true;
            ApplyFilters();
        }

        public void LoadData()
        {
            ApplyFilters();
        }

        public void SetFilters(string? search = null, string? type = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            _isUpdatingDates = true;

            if (txtSearch != null && search != null) txtSearch.Text = search;

            if (cmbType != null && type != null)
            {
                foreach (ComboBoxItem item in cmbType.Items)
                {
                    if (item.Content.ToString()!.Equals(type, StringComparison.OrdinalIgnoreCase))
                    {
                        cmbType.SelectedItem = item;
                        break;
                    }
                }
            }

            if (cmbStatus != null && status != null)
            {
                foreach (ComboBoxItem item in cmbStatus.Items)
                {
                    if (item.Content.ToString()!.Equals(status, StringComparison.OrdinalIgnoreCase))
                    {
                        cmbStatus.SelectedItem = item;
                        break;
                    }
                }
            }

            if (dpFromDate != null && fromDate.HasValue) dpFromDate.SelectedDate = fromDate.Value;
            if (dpToDate != null && toDate.HasValue) dpToDate.SelectedDate = toDate.Value;

            _isUpdatingDates = false;
            ApplyFilters();
        }

        // ⚡ ASYNC & NON-BLOCKING UI
        private async void ApplyFilters()
        {
            if (!_isReady || icSales == null || lblTotalRevenue == null) return;

            string search = txtSearch?.Text?.Trim() ?? "";
            string type = (cmbType?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Types";
            string status = (cmbStatus?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Statuses";

            DateTime? fromDate = dpFromDate?.SelectedDate;
            DateTime? toDate = dpToDate?.SelectedDate;

            // Run database queries on a background worker thread so the laptop never freezes!
            var transactions = await Task.Run(() => SalesService.GetTransactions(search, type, status, fromDate, toDate));

            _allTransactions = transactions;
            icSales.ItemsSource = _allTransactions;

            // ── Dynamic 4 KPI Calculations ──
            var validTransactions = _allTransactions.Where(t => t.Status != "Voided").ToList();
            decimal totalRev = validTransactions.Sum(t => t.TotalAmount);
            int totalOrders = validTransactions.Count;

            var jobOrders = validTransactions.Where(t => t.TransactionType == "JobOrder").ToList();
            int totalJobs = jobOrders.Count;
            decimal jobRev = jobOrders.Sum(t => t.TotalAmount);

            decimal avgTicket = totalOrders > 0 ? (totalRev / totalOrders) : 0;

            lblTotalRevenue.Text = $"₱{totalRev:N2}";
            lblOrderCount.Text = $"{totalOrders} Orders";
            lblJobOrderCount.Text = $"{totalJobs} Jobs";
            lblJobRevenueSub.Text = $"₱{jobRev:N2} Labor";
            lblAvgTicket.Text = $"₱{avgTicket:N2}";
            lblAvgSub.Text = totalOrders > 0 ? $"Across {totalOrders} orders" : "No orders in filter";
        }

        // ⚡ DEBOUNCED SEARCH: Only executes 300ms after you stop typing
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isReady) return;

            if (_searchDebounceTimer == null)
            {
                _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _searchDebounceTimer.Tick += (s, args) =>
                {
                    _searchDebounceTimer.Stop();
                    ApplyFilters();
                };
            }

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady) return;
            ApplyFilters();
        }

        private void CmbDatePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady || dpFromDate == null || dpToDate == null) return;

            if (cmbDatePreset?.SelectedItem is ComboBoxItem selected)
            {
                string choice = selected.Content.ToString()!;
                DateTime today = DateTime.Today;

                _isUpdatingDates = true;

                switch (choice)
                {
                    case "Today":
                        dpFromDate.SelectedDate = today;
                        dpToDate.SelectedDate = today;
                        break;
                    case "This Week":
                        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                        dpFromDate.SelectedDate = today.AddDays(-1 * diff);
                        dpToDate.SelectedDate = today;
                        break;
                    case "This Month":
                        dpFromDate.SelectedDate = new DateTime(today.Year, today.Month, 1);
                        dpToDate.SelectedDate = today;
                        break;
                    case "All Time":
                        dpFromDate.SelectedDate = null;
                        dpToDate.SelectedDate = null;
                        break;
                }

                _isUpdatingDates = false;
                ApplyFilters();
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingDates)
            {
                ApplyFilters();
            }
        }

        private void BtnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingDates = true;
            if (txtSearch != null) txtSearch.Text = "";
            if (cmbType != null) cmbType.SelectedIndex = 0;
            if (cmbStatus != null) cmbStatus.SelectedIndex = 0;
            if (cmbDatePreset != null) cmbDatePreset.SelectedIndex = 0;
            if (dpFromDate != null) dpFromDate.SelectedDate = null;
            if (dpToDate != null) dpToDate.SelectedDate = null;
            _isUpdatingDates = false;
            ApplyFilters();
        }

        // ── ROW CLICK & OFFICIAL DETAILS MODAL ──
        private void TransactionRow_Click(object sender, MouseButtonEventArgs e)
        {
            // If the user clicked the Void button, let the button handle it
            DependencyObject obj = e.OriginalSource as DependencyObject;
            while (obj != null && obj != sender)
            {
                if (obj is Button) return;
                obj = VisualTreeHelper.GetParent(obj);
            }
            if (sender is FrameworkElement elem && elem.Tag is TransactionItem item)
            {
                OpenTransactionDetailsModal(item.InvoiceNo, item.TransactionType);
            }
        }

        private void BtnQuickVoid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is TransactionItem item)
            {
                var confirm = MessageBox.Show(
                    $"Are you sure you want to VOID transaction '{item.InvoiceNo}' for {item.FormattedTotal}?\n\nThis will automatically restock items to inventory and record in history audit logs.",
                    "Confirm Void Transaction",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm == MessageBoxResult.Yes)
                {
                    if (SalesService.VoidTransaction(item.InvoiceNo, item.TransactionType, out string error))
                    {
                        MessageBox.Show($"Transaction '{item.InvoiceNo}' voided successfully. Inventory has been restocked.", "Void Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadData();
                    }
                    else
                    {
                        MessageBox.Show($"Failed to void transaction: {error}", "Void Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnViewTransactionDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is TransactionItem item)
            {
                OpenTransactionDetailsModal(item.InvoiceNo, item.TransactionType);
            }
        }

        public void OpenOrder(string invoiceNo, string? type = null)
        {
            if (string.IsNullOrEmpty(type))
            {
                type = invoiceNo.StartsWith("JO", StringComparison.OrdinalIgnoreCase) ? "JobOrder" : "Retail";
            }
            else if (type.Contains("Job", StringComparison.OrdinalIgnoreCase))
            {
                type = "JobOrder";
            }
            else
            {
                type = "Retail";
            }

            OpenTransactionDetailsModal(invoiceNo, type);
        }

        private void OpenTransactionDetailsModal(string invoiceNo, string type)
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
                pnlVoidWatermark.Visibility = Visibility.Visible;
                lblReceiptStatus.Text = "VOIDED / TRANSACTION CANCELLED";
                lblReceiptStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#DC2626")!;
                btnModalVoid.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlVoidWatermark.Visibility = Visibility.Collapsed;
                lblReceiptStatus.Text = "COMPLETED / FULLY PAID";
                lblReceiptStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#16A34A")!;
                btnModalVoid.Visibility = Visibility.Visible;
            }

            lblReceiptSubtotal.Text = _activeReceipt.FormattedSubtotal;
            lblReceiptDiscount.Text = _activeReceipt.FormattedDiscount;
            lblReceiptGrandTotal.Text = _activeReceipt.FormattedTotal;

            icReceiptItems.ItemsSource = _activeReceipt.Items;
            modalTransactionDetails.Visibility = Visibility.Visible;
        }

        private void BtnCloseTransactionDetails_Click(object sender, RoutedEventArgs e)
        {
            modalTransactionDetails.Visibility = Visibility.Collapsed;
            _activeReceipt = null;
        }

        private void BtnPrintModalReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_activeReceipt == null) return;

            try
            {
                var printDlg = new PrintDialog();
                if (printDlg.ShowDialog() == true)
                {
                    pnlPrintSlip.Measure(new Size(printDlg.PrintableAreaWidth, double.PositiveInfinity));
                    pnlPrintSlip.Arrange(new Rect(0, 0, printDlg.PrintableAreaWidth, pnlPrintSlip.DesiredSize.Height));
                    printDlg.PrintVisual(pnlPrintSlip, $"BikeKings_OfficialReceipt_{_activeReceipt.InvoiceNo}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Printing error: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnVoidModal_Click(object sender, RoutedEventArgs e)
        {
            if (_activeReceipt == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to VOID transaction '{_activeReceipt.InvoiceNo}' for {_activeReceipt.FormattedTotal}?\n\nThis will automatically restock items to inventory and record in history audit logs.",
                "Confirm Void",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (SalesService.VoidTransaction(_activeReceipt.InvoiceNo, _activeReceipt.TransactionType, out string error))
                {
                    MessageBox.Show($"Transaction '{_activeReceipt.InvoiceNo}' voided successfully. Inventory has been restocked.", "Void Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    modalTransactionDetails.Visibility = Visibility.Collapsed;
                    ApplyFilters();
                }
                else
                {
                    MessageBox.Show($"Failed to void transaction: {error}", "Void Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportSingleInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (_activeReceipt == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "CSV Spreadsheet (*.csv)|*.csv",
                FileName = $"Invoice_{_activeReceipt.InvoiceNo}_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using var writer = new StreamWriter(sfd.FileName, false, new System.Text.UTF8Encoding(true));
                    writer.WriteLine("BIKE KINGS CYCLE & MOTOR REPAIR SHOP - OFFICIAL INVOICE");
                    writer.WriteLine("Unit 5 JNM Bldg. Don Antonio Heights, Holy Spirit 1127, Quezon City");
                    writer.WriteLine("NON-VAT Reg. TIN: 254-217-808-00000");
                    writer.WriteLine("");
                    writer.WriteLine($"Invoice No,{_activeReceipt.InvoiceNo}");
                    writer.WriteLine($"Date & Time,{_activeReceipt.FormattedDate}");
                    writer.WriteLine($"Customer Name,{_activeReceipt.CustomerName}");
                    writer.WriteLine($"Contact Number,{_activeReceipt.ContactNumber}");
                    writer.WriteLine($"Motorcycle Unit,{_activeReceipt.MotorcycleUnit}");
                    writer.WriteLine($"Plate Number,{_activeReceipt.PlateNumber}");
                    writer.WriteLine($"Cashier,{_activeReceipt.CashierName}");
                    writer.WriteLine($"Mechanic,{_activeReceipt.MechanicName}");
                    writer.WriteLine($"Payment Method,{_activeReceipt.PaymentMethod}");
                    writer.WriteLine($"Status,{_activeReceipt.Status}");
                    writer.WriteLine("");
                    writer.WriteLine("Item No,Description,Quantity,Unit Price,Subtotal");
                    foreach (var item in _activeReceipt.Items)
                    {
                        writer.WriteLine($"{item.ItemNo},\"{item.Description}\",{item.Quantity},{item.UnitPrice:F2},{item.Subtotal:F2}");
                    }
                    writer.WriteLine("");
                    writer.WriteLine($"Total Sales / Subtotal,₱{_activeReceipt.Subtotal:F2}");
                    writer.WriteLine($"Discount,-₱{_activeReceipt.Discount:F2}");
                    writer.WriteLine($"Grand Total Due,₱{_activeReceipt.TotalAmount:F2}");
                    MessageBox.Show("Invoice details exported successfully to CSV!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_allTransactions == null || _allTransactions.Count == 0)
            {
                MessageBox.Show("No transactions available in current filter to export.", "Export Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "CSV Spreadsheet (*.csv)|*.csv",
                FileName = $"BikeKings_Transactions_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using var writer = new StreamWriter(sfd.FileName, false, new System.Text.UTF8Encoding(true));
                    writer.WriteLine("Invoice / Job No,Type,Date,Customer,Cashier,Mechanic,Subtotal,Discount,Total Amount,Payment Method,Status");
                    foreach (var item in _allTransactions)
                    {
                        writer.WriteLine($"\"{item.InvoiceNo}\",\"{item.TransactionType}\",\"{item.FormattedDate}\",\"{item.CustomerName}\",\"{item.CashierName}\",\"{item.MechanicName}\",{item.Subtotal:F2},{item.Discount:F2},{item.TotalAmount:F2},\"{item.PaymentMethod}\",\"{item.Status}\"");
                    }
                    MessageBox.Show("Transaction records successfully exported to CSV!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}