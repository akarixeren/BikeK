using System;
using System.Windows;
using System.Windows.Controls;
using BikeKings_System.Database;

namespace BikeKings_System.Views.Admin
{
    public partial class HistoryLogsControl : UserControl
    {
        public HistoryLogsControl()
        {
            InitializeComponent();
            LoadLogs();
        }

        public void LoadLogs()
        {
            string search = txtSearch?.Text?.Trim() ?? "";
            string action = (cmbActionFilter?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Actions";
            string role = (cmbRoleFilter?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Roles";
            string dateRange = (cmbDateFilter?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Time";

            DateTime? from = null;
            DateTime? to = null;

            switch (dateRange)
            {
                case "Today":
                    from = DateTime.Today;
                    to = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "Last 7 Days":
                    from = DateTime.Today.AddDays(-7);
                    to = DateTime.Now;
                    break;
                case "This Month":
                    from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    to = DateTime.Now;
                    break;
                case "Custom Range":
                    from = dpFromDate?.SelectedDate;
                    to = dpToDate?.SelectedDate?.Date.AddDays(1).AddTicks(-1);
                    break;
            }

            var logs = AuditLogService.GetLogs(search, action, role, from, to);
            icLogs.ItemsSource = logs;

            if (lblLogCount != null)
            {
                lblLogCount.Text = $"{logs.Count} log entries found";
            }
        }

        private void FilterChanged(object sender, EventArgs e)
        {
            if (IsLoaded) LoadLogs();
        }

        private void CmbDateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            string selected = (cmbDateFilter.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Time";
            if (pnlCustomDate != null)
            {
                pnlCustomDate.Visibility = selected == "Custom Range" ? Visibility.Visible : Visibility.Collapsed;
            }
            LoadLogs();
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            cmbActionFilter.SelectedIndex = 0;
            cmbRoleFilter.SelectedIndex = 0;
            cmbDateFilter.SelectedIndex = 0;
            if (dpFromDate != null) dpFromDate.SelectedDate = null;
            if (dpToDate != null) dpToDate.SelectedDate = null;
            if (pnlCustomDate != null) pnlCustomDate.Visibility = Visibility.Collapsed;
            LoadLogs();
        }
    }
}