using System.Net;
using Xunit;

namespace Betakas.Api.Tests;

/// <summary>
/// Yetki kuralları. Bu testlerin hepsi eskiden istemcide olan ve kötü niyetli bir
/// kullanıcının atlayabileceği kontrollerdir; artık sunucuda uygulanıyorlar.
/// </summary>
[Collection("api")]
public class AuthorizationTests(BetakasFixture fx)
{
    [Fact]
    public async Task Kurucu_komisyon_oranini_degistiremez()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (status, body) = await api.PutAsync("/api/admin/settings/fee", new { value = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains("yönetim", ApiClient.Error(body));
    }

    [Fact]
    public async Task Testci_baskasinin_talebini_kapatamaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("ayse@testci.co", "tester");

        var (status, _) = await api.PostAsync("/api/requests/r1/close");
        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task Testci_uyelik_onaylayamaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("ayse@testci.co", "tester");

        var (status, _) = await api.PostAsync("/api/admin/users/t3/approve");
        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task Kurucu_kendi_talebine_basvuramaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (status, body) = await api.PostAsync("/api/requests/r3/apply");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("Kendi talebine", ApiClient.Error(body));
    }

    [Fact]
    public async Task Baskasinin_teslimini_kabul_edemezsin()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        // s4, Mert'in (u2) r1 talebindeki teslimdir; Elif onu kabul edemez.
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (status, body) = await api.PostAsync("/api/sessions/s4/accept", new { rating = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains("talep sahibi", ApiClient.Error(body));
    }

    [Fact]
    public async Task Testci_anlasmazligi_karara_baglayamaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("ayse@testci.co", "tester");

        var (status, _) = await api.PostAsync("/api/sessions/s5/resolve-dispute", new { outcome = "release" });
        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task Demoyu_yalnizca_yonetim_sifirlayabilir()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("mert@stokpro.co", "founder");

        var (status, _) = await api.PostAsync("/api/admin/reset");
        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task Baskasinin_surumune_talep_acilamaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (status, body) = await api.PostAsync("/api/requests", new
        {
            title = "Başkasının sürümü",
            versionId = "v5", // Mert'in sürümü
            productCategory = "SaaS / B2B",
            stage = "MVP",
            feedbackType = "Bug Avı",
            scenario = "Senaryoyu baştan sona uygula ve takıldığın noktaları not et.",
            credits = 10,
            slots = 1,
            excludeSector = false
        });

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains("kendi sürümün", ApiClient.Error(body));
    }

    [Fact]
    public async Task Yonetim_komisyon_oranini_degistirebilir_ama_sinir_disina_cikamaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("yonetim@betakas.co", "admin");

        var (ok, body) = await api.PutAsync("/api/admin/settings/fee", new { value = 25 });
        Assert.Equal(HttpStatusCode.OK, ok);
        Assert.Equal(25, ApiClient.StateOf(body).GetProperty("settings").GetProperty("feePct").GetInt32());

        var (tooHigh, _) = await api.PutAsync("/api/admin/settings/fee", new { value = 95 });
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh);
    }
}
