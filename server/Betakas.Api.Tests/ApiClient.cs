using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Betakas.Api.Tests;

/// <summary>Testlerde tekrar eden HTTP kalıplarını sadeleştiren ince sarmalayıcı.</summary>
public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public HttpClient Http => http;

    public async Task<JsonElement> LoginAsync(string email, string role)
    {
        var res = await http.PostAsJsonAsync("/api/auth/login",
            new { email, password = BetakasFactory.DemoPassword, role });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return body;
    }

    public void ClearAuth() => http.DefaultRequestHeaders.Authorization = null;

    public async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(
        HttpMethod method, string url, object? payload = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (payload != null) req.Content = JsonContent.Create(payload, options: Json);

        var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        var body = string.IsNullOrWhiteSpace(text)
            ? default
            : JsonSerializer.Deserialize<JsonElement>(text);
        return (res.StatusCode, body);
    }

    public Task<(HttpStatusCode, JsonElement)> PostAsync(string url, object? payload = null) =>
        SendAsync(HttpMethod.Post, url, payload);

    public Task<(HttpStatusCode, JsonElement)> PutAsync(string url, object? payload = null) =>
        SendAsync(HttpMethod.Put, url, payload);

    public Task<(HttpStatusCode, JsonElement)> GetAsync(string url) =>
        SendAsync(HttpMethod.Get, url);

    /// <summary>Eylem yanıtındaki güncel state.</summary>
    public static JsonElement StateOf(JsonElement body) => body.GetProperty("state");

    /// <summary>Token bakiyesi her zaman defterden türetilir — testler de aynı yolu izler.</summary>
    public static int Balance(JsonElement state, string account)
    {
        var total = 0;
        foreach (var e in state.GetProperty("ledger").EnumerateArray())
        {
            var amount = e.GetProperty("amount").GetInt32();
            if (e.GetProperty("to").GetString() == account) total += amount;
            if (e.GetProperty("from").GetString() == account) total -= amount;
        }
        return total;
    }

    public static decimal CashBalance(JsonElement state, string account)
    {
        decimal total = 0;
        foreach (var e in state.GetProperty("cashLedger").EnumerateArray())
        {
            var amount = e.GetProperty("amount").GetDecimal();
            if (e.GetProperty("to").GetString() == account) total += amount;
            if (e.GetProperty("from").GetString() == account) total -= amount;
        }
        return total;
    }

    public static JsonElement Session(JsonElement state, string id) =>
        state.GetProperty("sessions").EnumerateArray().First(s => s.GetProperty("id").GetString() == id);

    public static string Error(JsonElement body) =>
        body.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
}
