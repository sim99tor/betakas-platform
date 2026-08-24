using Betakas.Api.Data;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

public record ThrottleResult(bool Allowed, int RetryAfterSeconds = 0);

/// <summary>
/// Giriş denemesi sınırlayıcı. Başarısız denemeler hem e-posta hem IP başına sayılır:
///
///   • e-posta sayacı → tek bir hesaba parola deneme saldırısını durdurur
///   • IP sayacı      → çok sayıda hesabı tarayan saldırıyı durdurur
///
/// Sayaçlar veritabanında tutulur, böylece sunucu yeniden başlasa da sınır korunur ve
/// birden çok örnek çalıştırıldığında ortak davranır.
/// </summary>
public class LoginThrottle(BetakasDbContext db, IConfiguration config)
{
    private readonly int _maxPerEmail = config.GetValue("Auth:MaxAttemptsPerEmail", 5);
    private readonly int _maxPerIp = config.GetValue("Auth:MaxAttemptsPerIp", 20);
    private readonly int _windowMinutes = config.GetValue("Auth:WindowMinutes", 15);
    private readonly int _lockoutMinutes = config.GetValue("Auth:LockoutMinutes", 15);

    private static string EmailKey(string email) => "email:" + email.Trim().ToLowerInvariant();

    private static string IpKey(string ip) => "ip:" + ip;

    public async Task<ThrottleResult> CheckAsync(string email, string ip)
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-_windowMinutes);

        // Anahtarlar yerel değişkene alınır: EF metot çağrısını SQL'e çeviremez.
        var emailKey = EmailKey(email);
        var ipKey = IpKey(ip);

        var emailCount = await db.LoginAttempts.CountAsync(a => a.Key == emailKey && a.At >= since);
        if (emailCount >= _maxPerEmail)
            return new ThrottleResult(false, _lockoutMinutes * 60);

        var ipCount = await db.LoginAttempts.CountAsync(a => a.Key == ipKey && a.At >= since);
        if (ipCount >= _maxPerIp)
            return new ThrottleResult(false, _lockoutMinutes * 60);

        return new ThrottleResult(true);
    }

    public async Task RecordFailureAsync(string email, string ip)
    {
        var now = DateTimeOffset.UtcNow;
        db.LoginAttempts.Add(new LoginAttempt { Key = EmailKey(email), At = now });
        db.LoginAttempts.Add(new LoginAttempt { Key = IpKey(ip), At = now });
        await db.SaveChangesAsync();
    }

    /// <summary>Başarılı girişte o e-postanın sayacı sıfırlanır (IP sayacı kalır).</summary>
    public async Task ClearAsync(string email)
    {
        var emailKey = EmailKey(email);
        await db.LoginAttempts.Where(a => a.Key == emailKey).ExecuteDeleteAsync();
    }

    /// <summary>Pencere dışında kalan kayıtları temizler (açılışta çağrılır).</summary>
    public async Task PurgeOldAsync()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_windowMinutes * 4);
        await db.LoginAttempts.Where(a => a.At < cutoff).ExecuteDeleteAsync();
    }
}
