using Betakas.Api.Data;
using Betakas.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Betakas.Api.Tests;

/// <summary>
/// Testler gerçek veritabanına karşı koşar — escrow ve defter mantığı sağlayıcıya bağlı
/// olduğu için in-memory sağlayıcı yanıltıcı olurdu. Ayrı bir test veritabanı kullanılır
/// (varsayılan: betakas_test), böylece geliştirme verisi bozulmaz.
/// </summary>
public class BetakasFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "betakas";

    /// <summary>
    /// Test veritabanı sırayla şuradan çözülür: ortam değişkeni → user-secrets → yerel
    /// varsayılan. Supabase kullanılıyorsa `supabase-kur.ps1` bunu user-secrets'e yazar.
    /// </summary>
    private static string TestConnectionString
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresTest");
            if (!string.IsNullOrWhiteSpace(env)) return env;

            var secrets = new ConfigurationBuilder()
                .AddUserSecrets<BetakasFactory>(optional: true)
                .Build();

            return secrets["ConnectionStrings:PostgresTest"]
                   ?? "Host=localhost;Port=5432;Database=betakas_test;Username=postgres;Password=betakas123";
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = TestConnectionString,
            ["Database:Provider"] = "Postgres",
            ["Jwt:Key"] = "test-ortami-icin-sabit-anahtar-en-az-32-karakter-uzunlugunda",
            // Kaba kuvvet testinin belirli olması için eşikler sabitlenir.
            ["Auth:MaxAttemptsPerEmail"] = "3",
            ["Auth:MaxAttemptsPerIp"] = "50",
            ["Auth:WindowMinutes"] = "15"
        }));
        return base.CreateHost(builder);
    }

    /// <summary>Her testin aynı bilinen durumdan başlaması için demo veriyi yeniden yazar.</summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<SeedService>().ResetAsync();

        var db = sp.GetRequiredService<BetakasDbContext>();
        db.LoginAttempts.RemoveRange(db.LoginAttempts);
        db.RefreshTokens.RemoveRange(db.RefreshTokens);
        await db.SaveChangesAsync();
    }
}
