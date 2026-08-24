using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Betakas.Api.Tests;

/// <summary>Kimlik doğrulama: parola, kaba kuvvet sınırı, yenileme jetonu ve iptal.</summary>
[Collection("api")]
public class AuthTests(BetakasFixture fx)
{
    [Fact]
    public async Task Parola_hashi_hicbir_yanitta_istemciye_gitmez()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (_, state) = await api.GetAsync("/api/state");
        var raw = state.GetRawText();

        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2a$", raw); // BCrypt hash öneki
    }

    [Fact]
    public async Task Onay_bekleyen_hesap_giris_yapamaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();

        var (status, body) = await api.PostAsync("/api/auth/login",
            new { email = "selin@testci.co", password = BetakasFactory.DemoPassword, role = "tester" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("onaylanmadı", ApiClient.Error(body));
    }

    [Fact]
    public async Task Cok_sayida_hatali_denemeden_sonra_hesap_gecici_kilitlenir()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();

        // Eşik test yapılandırmasında 3'tür.
        for (var i = 0; i < 3; i++)
        {
            var (status, _) = await api.PostAsync("/api/auth/login",
                new { email = "burak@testci.co", password = "yanlis-parola", role = "tester" });
            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        // Dördüncü deneme — DOĞRU parolayla bile — kilit yüzünden reddedilir.
        var (locked, body) = await api.PostAsync("/api/auth/login",
            new { email = "burak@testci.co", password = BetakasFactory.DemoPassword, role = "tester" });

        Assert.Equal(HttpStatusCode.TooManyRequests, locked);
        Assert.Contains("Çok fazla başarısız deneme", ApiClient.Error(body));
    }

    [Fact]
    public async Task Hatali_parola_hesabin_varligini_sizdirmaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();

        var (_, unknown) = await api.PostAsync("/api/auth/login",
            new { email = "olmayan@hesap.co", password = "birsey", role = "tester" });
        var (_, wrongPw) = await api.PostAsync("/api/auth/login",
            new { email = "burak@testci.co", password = "yanlis", role = "tester" });

        Assert.Equal(ApiClient.Error(unknown), ApiClient.Error(wrongPw));
    }

    [Fact]
    public async Task Yenileme_jetonu_yeni_erisim_jetonu_uretir_ve_dondurulur()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        var login = await api.LoginAsync("ayse@testci.co", "tester");
        var firstRefresh = login.GetProperty("refreshToken").GetString();

        var (status, body) = await api.PostAsync("/api/auth/refresh", new { refreshToken = firstRefresh });

        Assert.Equal(HttpStatusCode.OK, status);
        var secondRefresh = body.GetProperty("refreshToken").GetString();
        Assert.NotNull(body.GetProperty("token").GetString());
        Assert.NotEqual(firstRefresh, secondRefresh); // rotation
    }

    [Fact]
    public async Task Kullanilmis_yenileme_jetonu_ikinci_kez_kabul_edilmez()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        var login = await api.LoginAsync("ayse@testci.co", "tester");
        var used = login.GetProperty("refreshToken").GetString();

        await api.PostAsync("/api/auth/refresh", new { refreshToken = used });

        // Aynı jeton tekrar geldi → sızıntı varsayımı, reddedilir.
        var (status, _) = await api.PostAsync("/api/auth/refresh", new { refreshToken = used });
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Cikis_yapinca_yenileme_jetonu_iptal_olur()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        var login = await api.LoginAsync("burak@testci.co", "tester");
        var refresh = login.GetProperty("refreshToken").GetString();

        await api.PostAsync("/api/auth/logout", new { refreshToken = refresh });

        var (status, _) = await api.PostAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Jetonsuz_istek_reddedilir()
    {
        var api = fx.NewClient();
        var (status, _) = await api.GetAsync("/api/state");
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Public_state_giris_gerektirmez_ama_hassas_veri_icermez()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();

        var (status, body) = await api.GetAsync("/api/public/state");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(body.GetProperty("ledger").EnumerateArray());
        Assert.Empty(body.GetProperty("cashLedger").EnumerateArray());
        Assert.Empty(body.GetProperty("purchases").EnumerateArray());

        // Feedback metinleri ve kullanıcı e-postaları redakte edilir.
        foreach (var s in body.GetProperty("sessions").EnumerateArray())
            Assert.True(s.GetProperty("feedback").ValueKind == JsonValueKind.Null);
        foreach (var u in body.GetProperty("users").EnumerateArray())
            Assert.Equal("", u.GetProperty("email").GetString());
    }
}
