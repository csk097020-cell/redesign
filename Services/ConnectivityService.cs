using System.Diagnostics;

namespace MomentaryMomentos.Services;

/// <summary>
/// Detects network connectivity and connection type.
/// Used to determine when to sync data and whether WiFi-only mode should apply.
/// </summary>
public class ConnectivityService
{
    /// <summary>
    /// Check if device has any network connection.
    /// </summary>
    public bool IsConnected
    {
        get
        {
            try
            {
                return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConnectivityService: IsConnected check failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Check if device is connected via WiFi.
    /// </summary>
    public bool IsWiFiConnected
    {
        get
        {
            try
            {
                var profiles = Connectivity.Current.ConnectionProfiles;
                return profiles.Contains(ConnectionProfile.WiFi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConnectivityService: IsWiFiConnected check failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Check if device is connected via cellular/mobile data.
    /// </summary>
    public bool IsCellularConnected
    {
        get
        {
            try
            {
                var profiles = Connectivity.Current.ConnectionProfiles;
                return profiles.Contains(ConnectionProfile.Cellular);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConnectivityService: IsCellularConnected check failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Get a description of the current connection.
    /// </summary>
    public string ConnectionDescription
    {
        get
        {
            if (!IsConnected)
                return "Offline";
            
            if (IsWiFiConnected)
                return "WiFi";
            
            if (IsCellularConnected)
                return "Mobile Data";
            
            return "Connected";
        }
    }

    /// <summary>
    /// Subscribe to connectivity changes.
    /// </summary>
    public void MonitorConnectivity(Action<bool> onConnectivityChanged)
    {
        try
        {
            Connectivity.Current.ConnectivityChanged += (sender, args) =>
            {
                try
                {
                    var isConnected = args.NetworkAccess == NetworkAccess.Internet;
                    onConnectivityChanged(isConnected);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ConnectivityService: Error in connectivity callback: {ex.Message}");
                }
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ConnectivityService: Failed to subscribe to ConnectivityChanged: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if we should upload based on WiFi-only setting.
    /// </summary>
    public bool ShouldUpload(bool wifiOnlyMode)
    {
        if (!IsConnected)
            return false;

        if (!wifiOnlyMode)
            return true; // Upload on any connection

        return IsWiFiConnected; // Only upload on WiFi
    }
}
