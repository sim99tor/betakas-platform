using System.Net;
using System.Text.Json;
using Xunit;

namespace Betakas.Api.Tests;

/// <summary>
/// Token ekonomisinin çekirdeği: escrow kilidi, serbest bırakma, iade ve nakit ödeme.
/// Bu kurallar artık sunucuda olduğu için burada doğrulanabiliyorlar.
/// </summary>
[Collection("api")]
public class EscrowTests(BetakasFixture fx)
{
    [Fact]
    public async Task Talep_acilinca_slot_carpi_token_escrowa_kilitlenir()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (_, before) = await api.GetAsync("/api/state");
        var balanceBefore = ApiClient.Balance(before, "u1");
        var escrowBefore = ApiClient.Balance(before, "escrow");

        var (status, body) = await api.PostAsync("/api/requests", NewRequest(credits: 12, slots: 2));

        Assert.Equal(HttpStatusCode.OK, status);
        var state = ApiClient.StateOf(body);
        Assert.Equal(balanceBefore - 24, ApiClient.Balance(state, "u1"));
        Assert.Equal(escrowBefore + 24, ApiClient.Balance(state, "escrow"));
    }

    [Fact]
    public async Task Bakiyeden_fazla_token_bloke_edilemez()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (status, body) = await api.PostAsync("/api/requests", NewRequest(credits: 5000, slots: 1));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("Yetersiz token", ApiClient.Error(body));
    }

    [Fact]
    public async Task Test_kabul_edilince_escrow_cozulur_ve_havuzdan_nakit_odenir()
    {
        await fx.ResetAsync();

        // Ayşe (testçi) Mert'in talebindeki onaylı oturumu teslim eder.
        var tester = fx.NewClient();
        await tester.LoginAsync("ayse@testci.co", "tester");
        var (submitStatus, _) = await tester.PostAsync("/api/sessions/s13/submit", ValidFeedback());
        Assert.Equal(HttpStatusCode.OK, submitStatus);

        var founder = fx.NewClient();
        await founder.LoginAsync("mert@stokpro.co", "founder");

        var (_, before) = await founder.GetAsync("/api/state");
        var testerTokens = ApiClient.Balance(before, "t1");
        var escrowBefore = ApiClient.Balance(before, "escrow");
        var testerCash = ApiClient.CashBalance(before, "t1");
        var poolBefore = ApiClient.CashBalance(before, "pool");

        var (status, body) = await founder.PostAsync("/api/sessions/s13/accept", new { rating = 5 });
        Assert.Equal(HttpStatusCode.OK, status);

        var state = ApiClient.StateOf(body);
        var session = ApiClient.Session(state, "s13");
        Assert.Equal("accepted", session.GetProperty("status").GetString());

        // r1 talebi 15 token/slot; Ayşe 5.0★ ve 2 tamamlanmış testle 1.2× çarpana sahiptir.
        // Escrow'dan 15 token çıkar, sistemden 3 token itibar bonusu basılır.
        Assert.Equal(escrowBefore - 15, ApiClient.Balance(state, "escrow"));
        Assert.Equal(testerTokens + 15 + 3, ApiClient.Balance(state, "t1"));

        // Nakit: 15 token × ₺8 (token ₺10, komisyon %20) = ₺120 havuzdan testçiye.
        Assert.Equal(testerCash + 120m, ApiClient.CashBalance(state, "t1"));
        Assert.Equal(poolBefore - 120m, ApiClient.CashBalance(state, "pool"));
    }

    [Fact]
    public async Task Talep_kapatilinca_kalan_escrow_iade_edilir()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (_, created) = await api.PostAsync("/api/requests", NewRequest(credits: 20, slots: 2));
        var state = ApiClient.StateOf(created);
        var newId = state.GetProperty("requests").EnumerateArray().Last().GetProperty("id").GetString()!;
        var afterLock = ApiClient.Balance(state, "u1");

        var (status, body) = await api.PostAsync($"/api/requests/{newId}/close");

        Assert.Equal(HttpStatusCode.OK, status);
        // 40 token kilitlenmişti, hiç teslim olmadığı için tamamı geri döner.
        Assert.Equal(afterLock + 40, ApiClient.Balance(ApiClient.StateOf(body), "u1"));
    }

    [Fact]
    public async Task Anlasmazlik_iade_ile_bitince_token_kurucuya_doner_ve_itibara_ceza_yansir()
    {
        await fx.ResetAsync();
        var admin = fx.NewClient();
        await admin.LoginAsync("yonetim@betakas.co", "admin");

        var (_, before) = await admin.GetAsync("/api/state");
        var ownerBefore = ApiClient.Balance(before, "u1"); // s5, Elif'in r3 talebindedir

        var (status, body) = await admin.PostAsync("/api/sessions/s5/resolve-dispute", new { outcome = "refund" });

        Assert.Equal(HttpStatusCode.OK, status);
        var state = ApiClient.StateOf(body);
        var session = ApiClient.Session(state, "s5");

        Assert.Equal("rejected", session.GetProperty("status").GetString());
        Assert.Equal("refund", session.GetProperty("disputeOutcome").GetString());
        Assert.Equal(ownerBefore + 25, ApiClient.Balance(state, "u1")); // r3: 25 token/slot
    }

    [Fact]
    public async Task One_cikarma_token_yakar_ve_iki_kez_yapilamaz()
    {
        await fx.ResetAsync();
        var api = fx.NewClient();
        await api.LoginAsync("elif@finbutce.co", "founder");

        var (_, before) = await api.GetAsync("/api/state");
        var balanceBefore = ApiClient.Balance(before, "u1");

        var (status, body) = await api.PostAsync("/api/requests/r3/boost");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(balanceBefore - 10, ApiClient.Balance(ApiClient.StateOf(body), "u1"));

        var (again, _) = await api.PostAsync("/api/requests/r3/boost");
        Assert.Equal(HttpStatusCode.Conflict, again);
    }

    private static object NewRequest(int credits, int slots) => new
    {
        title = "Test talebi",
        versionId = "v2",
        productCategory = "Tüketici Uygulaması (B2C)",
        stage = "MVP",
        feedbackType = "Bug Avı",
        scenario = "Senaryoyu baştan sona uygula ve takıldığın noktaları not et.",
        credits,
        slots,
        excludeSector = false
    };

    private static object ValidFeedback() => new
    {
        fields = new Dictionary<string, string>
        {
            ["firstImpression"] = new string('a', 70),
            ["bugs"] = new string('b', 40),
            ["ux"] = new string('c', 70)
        },
        wouldUse = "evet",
        wouldUseWhy = "Günlük kullanırdım.",
        proofUrl = "https://loom.com/share/test",
        durationMin = 30
    };
}
