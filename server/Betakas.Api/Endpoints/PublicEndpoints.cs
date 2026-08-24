using Betakas.Api.Services;

namespace Betakas.Api.Endpoints;

/// <summary>
/// Giriş gerektirmeyen, redakte edilmiş veri. İki şey için gerekir: giriş ekranındaki
/// demo hesap kartları ve paylaşılabilir public testçi profili (/t/&lt;id&gt;).
/// Feedback metinleri, e-postalar (demo kartları dışında), defterler, faturalar ve
/// çekimler bilinçli olarak dışarıda bırakılmıştır.
///
/// Redaksiyon tam state ile aynı önbellekten türetilir (bkz. <see cref="PublicStateService"/>),
/// böylece giriş ekranı ayrıca veritabanı turu atmaz.
/// </summary>
public static class PublicEndpoints
{
    public static void MapPublicEndpoints(this WebApplication app)
    {
        app.MapGet("/api/public/state", async (PublicStateService publicState) =>
            Results.Ok(await publicState.GetAsync()));
    }
}
