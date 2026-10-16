using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using BikeKings_System.Database;
using BikeKings_System.Helpers;
using BikeKings_System.Models;

namespace BikeKings_System.Views.Admin
{
    public class ServiceItem
    {
        public int ServiceID { get; set; }
        public string ServiceName { get; set; } = "";
        public decimal LaborFee { get; set; }
        public string FormattedFee => $"₱{LaborFee:N2}";
    }

    public class StaffItem
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string DisplayName => $"{FullName} (@{Username})";
        public string RoleText => $"Role: {Role}";

        // Protect primary root admin account from accidental deletion
        public Visibility RemoveVisibility => (Role == "Admin" && Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public partial class MaintenanceControl : UserControl
    {
        private string _editingType = "";
        private int _editingId = 0;

        private static readonly Regex _personNameRegex = new Regex(@"^[\p{L}\s.\-']{2,100}$", RegexOptions.Compiled);
        private static readonly Regex _commissionRegex = new Regex(@"^(100(\.0{1,2})?|[1-9]?[0-9](\.[0-9]{1,2})?)$", RegexOptions.Compiled);
        private static readonly Regex _generalNameRegex = new Regex(@"^[a-zA-Z0-9\s.,&'\-()/#+""%*\[\]:;!@]{2,100}$", RegexOptions.Compiled);

        public MaintenanceControl()
        {
            InitializeComponent();
            LoadAllData();
        }

        public void LoadAllData()
        {
            // 1. Categories
            icCategories.ItemsSource = MaintenanceService.GetCategoriesList();

            // 2. Brands
            icBrands.ItemsSource = MaintenanceService.GetBrandsList();

            // 3. Services
            var dtServices = MaintenanceService.GetServices();
            var serviceList = new List<ServiceItem>();
            if (dtServices != null)
            {
                foreach (DataRow row in dtServices.Rows)
                {
                    serviceList.Add(new ServiceItem
                    {
                        ServiceID = Convert.ToInt32(row["ServiceID"]),
                        ServiceName = row["ServiceName"]?.ToString() ?? "",
                        LaborFee = row["LaborFee"] != DBNull.Value ? Convert.ToDecimal(row["LaborFee"]) : 0m
                    });
                }
            }
            icServices.ItemsSource = serviceList;

            // 4. Standalone Mechanics
            icMechanics.ItemsSource = MaintenanceService.GetMechanicsList();

            // 5. Staff Users
            var dtUsers = MaintenanceService.GetStaffUsers();
            var staffList = new List<StaffItem>();
            if (dtUsers != null)
            {
                foreach (DataRow row in dtUsers.Rows)
                {
                    staffList.Add(new StaffItem
                    {
                        UserID = Convert.ToInt32(row["UserID"]),
                        FullName = row["FullName"]?.ToString() ?? "",
                        Username = row["Username"]?.ToString() ?? "",
                        Role = row["Role"]?.ToString() ?? ""
                    });
                }
            }
            icStaff.ItemsSource = staffList;
        }

        // ── 1. CATEGORIES ──
        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            string categoryName = txtCategory.Text.Trim();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                MessageBox.Show("Please enter a category name.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MaintenanceService.AddCategory(categoryName))
            {
                AuditLogService.Log("MAINTENANCE", $"Added category '{categoryName}'.");
                txtCategory.Clear();
                LoadAllData();
            }
            else
            {
                MessageBox.Show("Category could not be added or already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditCategory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is CategoryModel cat)
            {
                _editingType = "Category";
                _editingId = cat.CategoryID;
                lblEditModalTitle.Text = "Edit Category Name";
                txtEditName.Text = cat.CategoryName;
                pnlEditServiceFee.Visibility = Visibility.Collapsed;
                modalEditItem.Visibility = Visibility.Visible;
            }
        }

        private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int catId)
            {
                if (MessageBox.Show("Are you sure you want to delete this category?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    MaintenanceService.DeleteCategory(catId);
                    AuditLogService.Log("MAINTENANCE", $"Deleted category ID {catId}.");
                    LoadAllData();
                }
            }
        }

        // ── 2. BRANDS ──
        private void BtnAddBrand_Click(object sender, RoutedEventArgs e)
        {
            string brandName = txtBrand.Text.Trim();
            if (string.IsNullOrWhiteSpace(brandName))
            {
                MessageBox.Show("Please enter a brand name.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MaintenanceService.AddBrand(brandName))
            {
                AuditLogService.Log("MAINTENANCE", $"Added brand '{brandName}'.");
                txtBrand.Clear();
                LoadAllData();
            }
            else
            {
                MessageBox.Show("Brand could not be added or already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditBrand_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is BrandModel brand)
            {
                _editingType = "Brand";
                _editingId = brand.BrandID;
                lblEditModalTitle.Text = "Edit Brand Name";
                txtEditName.Text = brand.BrandName;
                pnlEditServiceFee.Visibility = Visibility.Collapsed;
                modalEditItem.Visibility = Visibility.Visible;
            }
        }

        private void BtnDeleteBrand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int brandId)
            {
                if (MessageBox.Show("Are you sure you want to delete this brand?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    MaintenanceService.DeleteBrand(brandId);
                    AuditLogService.Log("MAINTENANCE", $"Deleted brand ID {brandId}.");
                    LoadAllData();
                }
            }
        }

        // ── 3. SERVICES ──
        private void BtnAddService_Click(object sender, RoutedEventArgs e)
        {
            string serviceName = txtServiceName.Text.Trim();
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                MessageBox.Show("Please enter a service name.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtLaborFee.Text.Trim(), out decimal fee) || fee < 0)
            {
                MessageBox.Show("Please enter a valid labor fee.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MaintenanceService.AddService(serviceName, fee))
            {
                AuditLogService.Log("MAINTENANCE", $"Added service '{serviceName}' (₱{fee:N2}).");
                txtServiceName.Clear();
                txtLaborFee.Text = "0.00";
                LoadAllData();
            }
            else
            {
                MessageBox.Show("Service could not be added or already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditService_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ServiceItem srv)
            {
                _editingType = "Service";
                _editingId = srv.ServiceID;
                lblEditModalTitle.Text = "Edit Repair Service";
                txtEditName.Text = srv.ServiceName;
                txtEditFee.Text = srv.LaborFee.ToString("F2");
                pnlEditServiceFee.Visibility = Visibility.Visible;
                modalEditItem.Visibility = Visibility.Visible;
            }
        }

        private void BtnDeleteService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int serviceId)
            {
                if (MessageBox.Show("Are you sure you want to delete this repair service?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    MaintenanceService.DeleteService(serviceId);
                    AuditLogService.Log("MAINTENANCE", $"Deleted service ID {serviceId}.");
                    LoadAllData();
                }
            }
        }

        // ── 4. MECHANICS ──
        private void BtnAddMechanic_Click(object sender, RoutedEventArgs e)
        {
            string name = txtMechanic.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || !_personNameRegex.IsMatch(name))
            {
                MessageBox.Show("Please enter a valid mechanic name (letters, spaces, dots, or hyphens only, min 2 characters).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Clean any % character entered by user (e.g. "80%")
            string rawComm = txtMechCommission.Text.Replace("%", "").Trim();
            if (!_commissionRegex.IsMatch(rawComm) || !decimal.TryParse(rawComm, out decimal commRate) || commRate <= 0 || commRate > 100)
            {
                MessageBox.Show("Please enter a valid commission rate between 1% and 100% (e.g. 80 or 75.5).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MaintenanceService.AddMechanic(name, commRate))
            {
                AuditLogService.Log("MAINTENANCE", $"Registered mechanic '{name}' ({commRate:0}% commission).");
                txtMechanic.Clear();
                txtMechCommission.Text = "80";
                LoadAllData();
            }
            else
            {
                MessageBox.Show("Mechanic could not be added or already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnEditMechanic_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is MechanicModel mech)
            {
                _editingType = "Mechanic";
                _editingId = mech.MechanicID;
                lblEditModalTitle.Text = "Edit Mechanic & Commission";
                txtEditName.Text = mech.FullName;
                txtEditCommission.Text = mech.CommissionRate.ToString("0");
                pnlEditServiceFee.Visibility = Visibility.Collapsed;
                pnlEditMechCommission.Visibility = Visibility.Visible;
                modalEditItem.Visibility = Visibility.Visible;
            }
        }

        private void BtnRemoveMechanic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int mechId)
            {
                if (MessageBox.Show("Are you sure you want to remove this mechanic?", "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    MaintenanceService.DeleteMechanic(mechId);
                    AuditLogService.Log("MAINTENANCE", $"Removed mechanic ID {mechId}.");
                    LoadAllData();
                }
            }
        }

        // ── 5. STAFF & ADMIN USERS ──
        private void BtnCreateUser_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateUserWindow();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                AuditLogService.Log("USER_MGMT", "Created a new system user account.");
                LoadAllData();
            }
        }

        private void BtnRemoveUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int userId)
            {
                if (MessageBox.Show("Are you sure you want to deactivate this account?", "Confirm Deactivation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (MaintenanceService.DeactivateUser(userId))
                    {
                        AuditLogService.Log("USER_MGMT", $"Deactivated user account ID {userId}.");
                        LoadAllData();
                    }
                }
            }
        }

        // ── 6. EDIT MODAL HANDLERS ──
        private void BtnCloseEditModal_Click(object sender, RoutedEventArgs e)
        {
            modalEditItem.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveEditModal_Click(object sender, RoutedEventArgs e)
        {
            string newName = txtEditName.Text.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool success = false;
            switch (_editingType)
            {
                case "Category":
                    success = MaintenanceService.UpdateCategory(_editingId, newName);
                    break;
                case "Brand":
                    success = MaintenanceService.UpdateBrand(_editingId, newName);
                    break;
                case "Service":
                    if (!decimal.TryParse(txtEditFee.Text.Trim(), out decimal fee) || fee < 0)
                    {
                        MessageBox.Show("Please enter a valid labor fee.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    success = MaintenanceService.UpdateService(_editingId, newName, fee);
                    break;
                case "Mechanic":
                    if (!_personNameRegex.IsMatch(newName))
                    {
                        MessageBox.Show("Please enter a valid mechanic name (letters, spaces, dots, or hyphens only).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string rawEditComm = txtEditCommission.Text.Replace("%", "").Trim();
                    if (!_commissionRegex.IsMatch(rawEditComm) || !decimal.TryParse(rawEditComm, out decimal commRate) || commRate <= 0 || commRate > 100)
                    {
                        MessageBox.Show("Please enter a valid commission rate (1 - 100%).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    success = MaintenanceService.UpdateMechanic(_editingId, newName, commRate);
                    break;


            }

            if (success)
            {
                AuditLogService.Log("MAINTENANCE", $"Updated {_editingType} ID {_editingId} to '{newName}'.");
                modalEditItem.Visibility = Visibility.Collapsed;
                LoadAllData();
            }
            else
            {
                MessageBox.Show("Could not save changes to database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}