namespace SkyBook.Maui;

/// <summary>Runtime configuration (the React client reads these from <c>.env</c> / Vite).</summary>
public static class AppConfig
{
    /// <summary>
    /// Syncfusion license key (https://www.syncfusion.com/account/manage-trials). Leave empty to run with the trial banner.
    /// Do not commit a real key; supply it through your own build/CI mechanism.
    /// </summary>
    public static string SyncfusionLicenseKey { get; } = "";

    /// <summary>Equivalent of VITE_AGENT_URL. The Android emulator reaches the host machine at 10.0.2.2.</summary>
    public static Uri AgentUrl { get; } = new(
        Environment.GetEnvironmentVariable("SKYBOOK_AGENT_URL")
        ?? (DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:10006" : "http://localhost:10006"));

    /// <summary>Viewport width below which the two panes collapse into Chat / Booking-view tabs (React: max-width 980px).</summary>
    public const double CompactWidth = 980;
}
