using BikeKings_System.Models;

namespace BikeKings_System.Helpers
{
    public static class SessionHelper
    {
        public static UserModel? CurrentUser { get; set; } = null;
        public static void Clear() => CurrentUser = null;
    }
}