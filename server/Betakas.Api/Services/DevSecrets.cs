using System.Security.Cryptography;

namespace Betakas.Api.Services;

/// <summary>
/// Sırların çözüm sırası: ortam değişkeni → user-secrets → appsettings → (yalnızca
/// geliştirmede) yerel olarak üretilen dosya.
///
/// appsettings.json'da hiçbir sır yoktur. Üretimde eksik sır sessizce tolere edilmez —
/// uygulama açılışta hata verir; böylece varsayılan bir anahtarla canlıya çıkılamaz.
/// </summary>
public static class DevSecrets
{
    private const string KeyFileName = ".dev-jwt-key";

    public static string ResolveJwtKey(IConfiguration config, IWebHostEnvironment env, ILogger logger)
    {
        // Ortam değişkeni:  Jwt__Key=...      user-secrets:  dotnet user-secrets set "Jwt:Key" "..."
        var configured = config["Jwt:Key"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (configured.Length < 32)
                throw new InvalidOperationException("Jwt:Key en az 32 karakter olmalı.");
            return configured;
        }

        if (!env.IsDevelopment())
            throw new InvalidOperationException(
                "Jwt:Key yapılandırılmamış. Ortam değişkeni olarak verin: Jwt__Key=<en az 32 karakter>");

        // Geliştirme kolaylığı: makineye özel bir anahtar üretilip gitignore'lu dosyada saklanır.
        var path = Path.Combine(env.ContentRootPath, KeyFileName);
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (existing.Length >= 32) return existing;
        }

        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        File.WriteAllText(path, generated);
        logger.LogWarning("Jwt:Key bulunamadı — geliştirme için yeni anahtar üretildi: {Path}", path);
        return generated;
    }

    /// <summary>
    /// Bağlantı dizesi de aynı sırayı izler. Geliştirmede yerel varsayılana düşer,
    /// üretimde açıkça verilmesi zorunludur.
    /// </summary>
    public static string ResolveConnectionString(
        IConfiguration config, IWebHostEnvironment env, bool useSqlServer, ILogger logger)
    {
        var name = useSqlServer ? "SqlServer" : "Postgres";
        var configured = config.GetConnectionString(name);
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        if (!env.IsDevelopment())
            throw new InvalidOperationException(
                $"ConnectionStrings:{name} yapılandırılmamış. Ortam değişkeni: ConnectionStrings__{name}=...");

        var fallback = useSqlServer
            ? "Server=localhost\\SQLEXPRESS;Database=betakas;Trusted_Connection=True;TrustServerCertificate=True"
            : "Host=localhost;Port=5432;Database=betakas;Username=postgres;Password=betakas123";

        logger.LogWarning("ConnectionStrings:{Name} verilmedi — yerel geliştirme varsayılanı kullanılıyor.", name);
        return fallback;
    }
}
