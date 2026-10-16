using System.Windows;
using BikeKings_System.Database;
using BikeKings_System.Views;

namespace BikeKings_System
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DatabaseInitializer.Initialize();
            var login = new LoginWindow();
            login.Show();
        }
    }
}