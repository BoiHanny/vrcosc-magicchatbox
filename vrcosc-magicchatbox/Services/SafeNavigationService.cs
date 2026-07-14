using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Opens URLs and folders safely, validating schemes and domain allowlists before launching.
/// </summary>
public sealed class SafeNavigationService : INavigationService
{
    private static readonly string[] DefaultAllowedSchemes = { "https" };

    public bool OpenUrl(string url)
    {
        return OpenUrlInternal(url, allowedDomains: null);
    }

    public bool OpenUrl(string url, string[] allowedDomains)
    {
        return OpenUrlInternal(url, allowedDomains);
    }

    public bool OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        try
        {
            string fullPath = Path.GetFullPath(folderPath);
            if (!Directory.Exists(fullPath))
            {
                Logging.WriteInfo($"SafeNavigationService: Folder does not exist: {fullPath}");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(fullPath);
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    public bool OpenFileInExplorer(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                Logging.WriteInfo($"SafeNavigationService: File does not exist: {fullPath}");
                return false;
            }

            // Use ArgumentList so the runtime applies correct Windows command-line
            // escaping, avoiding quote-injection issues when fullPath contains spaces.
            // Explorer accepts "/select," and the path as two distinct tokens.
            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("/select,");
            psi.ArgumentList.Add(fullPath);
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private bool OpenUrlInternal(string url, string[]? allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Logging.WriteInfo($"SafeNavigationService: Rejected malformed URL: {url}");
            return false;
        }

        if (!DefaultAllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            Logging.WriteInfo($"SafeNavigationService: Rejected non-HTTPS URL: {url}");
            return false;
        }

        if (allowedDomains != null && allowedDomains.Length > 0)
        {
            bool domainAllowed = allowedDomains.Any(d =>
                uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

            if (!domainAllowed)
            {
                Logging.WriteInfo($"SafeNavigationService: Rejected URL for non-whitelisted domain: {url}");
                return false;
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }
}
