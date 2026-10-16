using System;
using Microsoft.Data.SqlClient;

namespace BikeKings_System.Database
{
    public static class DatabaseHelper
    {
        private static readonly string ConnectionString =
            @"Server=.;Database=BikeKingsDB;Integrated Security=True;TrustServerCertificate=True;";

        public static SqlConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        public static string GetMasterConnectionString()
        {
            // ✅ Fixed: Changed from .\SQLEXPRESS to .
            return @"Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True;";
        }
    }
}