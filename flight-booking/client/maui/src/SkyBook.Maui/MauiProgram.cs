using Microsoft.Extensions.Logging;
using Syncfusion.Maui.A2UI.Hosting;
using Syncfusion.Maui.A2UI.SyncfusionComponents;
using Syncfusion.Maui.Core.Hosting;

namespace SkyBook.Maui;

public static class MauiProgram
{
    /// <summary>Catalog id the agent's MAUI designs declare in <c>createSurface</c>.</summary>
    public const string CatalogId = SyncfusionA2uiAppBuilderExtensions.DefaultCatalogId;

    public static MauiApp CreateMauiApp()
    {
        if (AppConfig.SyncfusionLicenseKey.Length > 0)
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(AppConfig.SyncfusionLicenseKey);

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .UseA2UIWithSyncfusionComponents(CatalogId)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(3) });
        builder.Services.AddSingleton(sp => new SkyBook.Core.AgentClient(sp.GetRequiredService<HttpClient>(), AppConfig.AgentUrl));
        builder.Services.AddSingleton<SkyBook.Core.BookingSession>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
