using System.Net;
using Xunit;

namespace Betakas.Api.Tests;

/// <summary>
/// Teslim formunun kalite kapısı ve ürün aşamasına göre değişen şablon.
/// Eskiden yalnızca tarayıcıda denetleniyordu; artık sunucu reddediyor.
/// </summary>
[Collection("api")]
public class FeedbackQualityTests(BetakasFixture fx)
{
    [Fact]
    public async Task Kisa_feedback_reddedilir()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("ayse@testci.co", "tester");

        var (status, body) = await api.PostAsync("/api/sessions/s13/submit", new
        {
            fields = new Dictionary<string, string>
            {
                ["firstImpression"] = "kısa",
                ["bugs"] = "yok",
                ["ux"] = "iyi"
            },
            wouldUse = "evet",
            wouldUseWhy = "Beğendim.",
            proofUrl = "https://loom.com/share/x",
            durationMin = 30
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("en az 60 karakter", ApiClient.Error(body));
    }

    [Fact]
    public async Task Kanit_linki_olmadan_teslim_edilemez()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("ayse@testci.co", "tester");

        var (status, body) = await api.PostAsync("/api/sessions/s13/submit", Valid(proofUrl: ""));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("ekran kaydı", ApiClient.Error(body));
    }

    [Fact]
    public async Task Cok_kisa_test_suresi_reddedilir()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("ayse@testci.co", "tester");

        var (status, body) = await api.PostAsync("/api/sessions/s13/submit", Valid(durationMin: 2));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("en az 5 dakika", ApiClient.Error(body));
    }

    [Fact]
    public async Task Gecerli_teslim_kabul_edilir()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("ayse@testci.co", "tester");

        var (status, body) = await api.PostAsync("/api/sessions/s13/submit", Valid());

        Assert.Equal(HttpStatusCode.OK, status);
        var session = ApiClient.Session(ApiClient.StateOf(body), "s13");
        Assert.Equal("submitted", session.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Onaylanmamis_oturum_teslim_edilemez()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        // s14 henüz "applied" durumundadır (Burak'ın bekleyen başvurusu).
        await api.LoginAsync("burak@testci.co", "tester");

        var (status, body) = await api.PostAsync("/api/sessions/s14/submit", Valid());

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("onaylanmış", ApiClient.Error(body));
    }

    [Fact]
    public async Task Baskasinin_oturumu_teslim_edilemez()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("burak@testci.co", "tester");

        var (status, _) = await api.PostAsync("/api/sessions/s13/submit", Valid());
        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    private static object Valid(string proofUrl = "https://loom.com/share/test", int durationMin = 30) => new
    {
        fields = new Dictionary<string, string>
        {
            ["firstImpression"] = new string('a', 70),
            ["bugs"] = new string('b', 40),
            ["ux"] = new string('c', 70)
        },
        wouldUse = "evet",
        wouldUseWhy = "Günlük kullanırdım.",
        proofUrl,
        durationMin
    };
}
