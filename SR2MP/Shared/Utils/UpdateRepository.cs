using System;
using System.Text.RegularExpressions;

namespace PlaygroundUpdates
{
    // The numeric repository ID survives owner renames and transfers. Keep the
    // two explicit release owners for the transition; never trust arbitrary URLs.
    internal static class UpdateRepository
    {
        internal const string FeedUrl = "https://api.github.com/repositories/1472210540/contents/update.json?ref=main";
        internal const string MediaType = "application/vnd.github.raw+json";

        internal static bool IsReleaseUrl(string value, string asset)
        {
            Uri uri;
            if (String.IsNullOrEmpty(value) || value != value.Trim() || !Uri.TryCreate(value, UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttps || !String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                return false;
            string[] parts = uri.AbsolutePath.Split('/');
            if (parts.Length != 7 || (parts[1] != "robertbrania-code" && parts[1] != "chestandcompass")
                || parts[2] != "Playground" || parts[3] != "releases" || parts[4] != "download"
                || !Regex.IsMatch(parts[5], "^v[0-9A-Za-z.-]+$") || parts[6] != asset)
                return false;
            return asset == "Playground.dll" || asset == "PlaygroundSetup.exe";
        }
    }
}
