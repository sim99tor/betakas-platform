using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Betakas.Api.Data;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Betakas.Api.Services;

public record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt);

/// <summary>
/// Oturum jetonları. Erişim jetonu kısa ömürlüdür (varsayılan 20 dk) ve iptal edilemez;
/// uzun ömürlü yenileme jetonu veritabanında tutulur, dolayısıyla iptal edilebilir.
///
/// Yenileme jetonu her kullanımda döndürülür (rotation). Kullanılmış bir jeton ikinci kez
/// gelirse bu, jetonun çalındığı anlamına gelir — o kullanıcının tüm oturumları kapatılır.
/// </summary>
public class TokenService(BetakasDbContext db, IConfiguration config)
{
    private readonly int _accessMinutes = config.GetValue("Jwt:AccessTokenMinutes", 20);
    private readonly int _refreshDays = config.GetValue("Jwt:RefreshTokenDays", 14);

    public SymmetricSecurityKey SigningKey =>
        new(Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key eksik.")));

    /// <summary>Ham jeton saklanmaz; yalnızca özeti tutulur.</summary>
    private static string HashOf(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string NewRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public string IssueAccessToken(User u, out DateTimeOffset expiresAt)
    {
        var expires = DateTime.UtcNow.AddMinutes(_accessMinutes);
        expiresAt = expires;

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, u.Id),
                new Claim(JwtRegisteredClaimNames.Email, u.Email),
                new Claim(ClaimTypes.Role, u.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: expires,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<TokenPair> IssuePairAsync(User u)
    {
        var access = IssueAccessToken(u, out var expiresAt);
        var raw = NewRawToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = u.Id,
            TokenHash = HashOf(raw),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_refreshDays)
        });
        await db.SaveChangesAsync();

        return new TokenPair(access, raw, expiresAt);
    }

    /// <summary>
    /// Yenileme jetonunu doğrular ve döndürür. Jeton geçersiz, süresi dolmuş ya da daha önce
    /// kullanılmışsa null döner; yeniden kullanım tespitinde tüm oturumlar iptal edilir.
    /// </summary>
    public async Task<TokenPair?> RotateAsync(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var hash = HashOf(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (existing is null) return null;

        if (existing.RevokedAt != null)
        {
            // Kullanılmış jeton tekrar geldi → sızıntı varsayımı: kullanıcının tüm jetonlarını iptal et.
            await RevokeAllAsync(existing.UserId);
            return null;
        }

        if (existing.ExpiresAt <= DateTimeOffset.UtcNow) return null;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId);
        if (user is null || user.Status != "active") return null;

        var pair = await IssuePairAsync(user);

        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.ReplacedById = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.Id)
            .FirstOrDefaultAsync();
        await db.SaveChangesAsync();

        return pair;
    }

    public async Task RevokeAsync(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;
        var hash = HashOf(rawToken);
        var t = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null);
        if (t is null) return;
        t.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task RevokeAllAsync(string userId)
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow));
    }

    /// <summary>Süresi dolmuş ve iptal edilmiş kayıtları temizler (açılışta çağrılır).</summary>
    public async Task PurgeExpiredAsync()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff || (t.RevokedAt != null && t.RevokedAt < cutoff))
            .ExecuteDeleteAsync();
    }
}
