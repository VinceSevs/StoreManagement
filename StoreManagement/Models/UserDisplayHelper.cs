using System;

namespace StoreManagement.Models
{
    public static class UserDisplayHelper
    {
        public static string GetInitials(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return "?";

            var parts = username.Split(new[] { '.', '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpperInvariant();

            if (parts.Length == 1 && parts[0].Length >= 2)
                return parts[0].Substring(0, 2).ToUpperInvariant();

            if (parts.Length == 1 && parts[0].Length == 1)
                return parts[0].ToUpperInvariant();

            return "?";
        }
    }
}
