using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Betakas.Api.Models;

/// <summary>
/// Platformun tekil durum satırı (Id her zaman 1). <see cref="Rev"/> iyimser eşzamanlılık
/// sayacıdır: her başarılı yazma bunu bir artırır, bayat revizyonla gelen yazma 409 alır.
/// </summary>
public class PlatformState
{
    public int Id { get; set; } = 1;
    public long Rev { get; set; }
    public decimal TokenPrice { get; set; } = 10m;
    public int FeePct { get; set; } = 20;
    public int SprintNo { get; set; } = 4;
    public int Seq { get; set; } = 100;
    public int SeedVersion { get; set; } = 7;
}

public class User
{
    [MaxLength(64)] public string Id { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(8)] public string Initials { get; set; } = "";
    [MaxLength(320)] public string Email { get; set; } = "";

    /// <summary>BCrypt hash. İstemciye hiçbir zaman gönderilmez.</summary>
    [MaxLength(200)] public string PasswordHash { get; set; } = "";

    [MaxLength(200)] public string? Startup { get; set; }
    [MaxLength(200)] public string? Title { get; set; }
    [MaxLength(500)] public string? Tagline { get; set; }
    [MaxLength(100)] public string? Sector { get; set; }

    /// <summary>Serbest yetenek etiketleri (jsonb string dizisi).</summary>
    public List<string> Skills { get; set; } = new();

    /// <summary>Ürün tipi taksonomisindeki uzmanlık alanları (jsonb string dizisi).</summary>
    public List<string> ExpertiseCategories { get; set; } = new();

    [MaxLength(60)] public string? ExpertiseOther { get; set; }

    /// <summary>founder | tester | admin</summary>
    [MaxLength(20)] public string Role { get; set; } = "founder";

    /// <summary>active | pending</summary>
    [MaxLength(20)] public string Status { get; set; } = "pending";

    /// <summary>Testçinin çekim talebinde kullandığı son IBAN.</summary>
    [MaxLength(64)] public string? Iban { get; set; }

    // --- Abonelik (istemcideki user.subscription alanının karşılığı) ---
    [MaxLength(40)] public string? SubscriptionPlanId { get; set; }
    public long? SubscriptionRenewsAt { get; set; }
    public bool SubscriptionActive { get; set; }
}

/// <summary>Kurucunun çıkardığı ürün sürümü (v1.0 → v1.1 → v2.0).</summary>
public class ProductVersion
{
    [MaxLength(64)] public string Id { get; set; } = "";
    [MaxLength(64)] public string OwnerId { get; set; } = "";
    [MaxLength(40)] public string Label { get; set; } = "";
    [MaxLength(1000)] public string? Url { get; set; }
    public long CreatedAt { get; set; }
    public string? Notes { get; set; }

    /// <summary>Bug numarası → durum (fixed | wip | later | norepro). jsonb.</summary>
    public JsonObject? Fixes { get; set; }

    public User? Owner { get; set; }
}

/// <summary>Bir sürüme açılan test talebi. Token escrow bu kayda bağlanır.</summary>
public class TestRequest
{
    [MaxLength(64)] public string Id { get; set; } = "";
    [MaxLength(64)] public string OwnerId { get; set; } = "";
    [MaxLength(64)] public string? VersionId { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string? Url { get; set; }
    [MaxLength(80)] public string? ProductCategory { get; set; }

    /// <summary>Fikir/Prototip | MVP | Büyüme. Boşsa istemci MVP sayar.</summary>
    [MaxLength(40)] public string? Stage { get; set; }

    [MaxLength(80)] public string? FeedbackType { get; set; }
    public string? Scenario { get; set; }

    /// <summary>Slot başına bloke edilen token.</summary>
    public int Credits { get; set; }
    public int Slots { get; set; }

    /// <summary>public | exclude-sector</summary>
    [MaxLength(40)] public string Visibility { get; set; } = "public";

    /// <summary>open | closed</summary>
    [MaxLength(20)] public string Status { get; set; } = "open";

    public long CreatedAt { get; set; }
    public bool Boosted { get; set; }
    public long? BoostedAt { get; set; }

    public User? Owner { get; set; }
    public ProductVersion? Version { get; set; }
}

/// <summary>Bir testçinin bir talep üzerindeki oturumu: başvuru → onay → teslim → puanlama.</summary>
public class TestSession
{
    [MaxLength(64)] public string Id { get; set; } = "";
    [MaxLength(64)] public string RequestId { get; set; } = "";
    [MaxLength(64)] public string TesterId { get; set; } = "";

    /// <summary>applied | approved | submitted | accepted | disputed | rejected</summary>
    [MaxLength(20)] public string Status { get; set; } = "applied";

    public long? AppliedAt { get; set; }
    public long? SubmittedAt { get; set; }

    /// <summary>Kurucunun testçiye verdiği puan (1-5).</summary>
    public int? Rating { get; set; }

    /// <summary>Testçinin kurucuya verdiği puan (1-5).</summary>
    public int? OwnerRating { get; set; }

    public int? DurationMin { get; set; }
    [MaxLength(1000)] public string? ProofUrl { get; set; }

    /// <summary>
    /// Aşamaya göre şekli değişen feedback objesi (firstImpression / bugs / ux / wouldUse /
    /// valueProp / confusing / dropOff / mostValuable / missingFeature …). Şemayı istemci
    /// belirlediği için jsonb olarak saklanır.
    /// </summary>
    public JsonObject? Feedback { get; set; }

    public string? DisputeNote { get; set; }

    /// <summary>Yönetimin anlaşmazlık kararı: release | refund.</summary>
    [MaxLength(20)] public string? DisputeOutcome { get; set; }

    /// <summary>Onay anında havuzdan testçiye geçen nakit (₺); ödenmediyse null.</summary>
    public decimal? CashPaid { get; set; }

    public TestRequest? Request { get; set; }
    public User? Tester { get; set; }
}

/// <summary>
/// Çift kayıtlı token defteri satırı. Bakiye asla elle tutulmaz, bu defterden türetilir.
/// Hesaplar: kullanıcı id'leri · "system" · "escrow".
/// </summary>
public class LedgerEntry
{
    [MaxLength(64)] public string Id { get; set; } = "";
    public long Ts { get; set; }
    [MaxLength(64)] public string From { get; set; } = "";
    [MaxLength(64)] public string To { get; set; } = "";
    public int Amount { get; set; }
    [MaxLength(40)] public string Type { get; set; } = "";
    [MaxLength(64)] public string? Ref { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Çift kayıtlı para (₺) defteri satırı.
/// Hesaplar: kullanıcı id'leri · "revenue" (platform geliri) · "pool" (ödül havuzu) · "bank".
/// </summary>
public class CashLedgerEntry
{
    [MaxLength(64)] public string Id { get; set; } = "";
    public long Ts { get; set; }
    [MaxLength(64)] public string From { get; set; } = "";
    [MaxLength(64)] public string To { get; set; } = "";
    public decimal Amount { get; set; }
    [MaxLength(40)] public string Type { get; set; } = "";
    [MaxLength(64)] public string? Ref { get; set; }
    public string? Note { get; set; }
}

/// <summary>Token paketi satın alımı veya abonelik dönemi tahsilatı (fatura kaydı).</summary>
public class Purchase
{
    [MaxLength(64)] public string Id { get; set; } = "";
    public long Ts { get; set; }
    [MaxLength(64)] public string UserId { get; set; } = "";

    /// <summary>Boşsa tek seferlik paket; "subscription" ise abonelik dönemi.</summary>
    [MaxLength(40)] public string? Kind { get; set; }

    [MaxLength(40)] public string PackageId { get; set; } = "";
    [MaxLength(120)] public string PackageName { get; set; } = "";
    public int Tokens { get; set; }
    public int Testers { get; set; }

    /// <summary>Tahsil edilen toplam tutar (₺).</summary>
    public decimal Gross { get; set; }

    /// <summary>Platform komisyonu (₺).</summary>
    public decimal Fee { get; set; }

    /// <summary>Testçi ödül havuzuna ayrılan tutar (₺).</summary>
    public decimal Pool { get; set; }

    [MaxLength(40)] public string InvoiceNo { get; set; } = "";
}

/// <summary>
/// Yenileme jetonu. Erişim jetonu kısa ömürlüdür; oturum bununla uzatılır.
/// Ham değer saklanmaz — yalnızca SHA-256 özeti tutulur, böylece veritabanı sızsa bile
/// jetonlar kullanılamaz. Kullanıldığında döndürülür (rotation) ve eskisi iptal edilir.
/// </summary>
public class RefreshToken
{
    [MaxLength(64)] public string Id { get; set; } = "";
    [MaxLength(64)] public string UserId { get; set; } = "";

    /// <summary>Ham jetonun SHA-256 özeti (base64).</summary>
    [MaxLength(64)] public string TokenHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Kullanıldığı ya da iptal edildiği an; null ise hâlâ geçerli.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Rotasyonda bu jetonun yerine geçen kayıt — yeniden kullanım tespiti için.</summary>
    [MaxLength(64)] public string? ReplacedById { get; set; }

    public User? User { get; set; }
}

/// <summary>
/// Başarısız giriş denemesi. Kaba kuvvet saldırısına karşı e-posta ve IP başına
/// pencere içindeki deneme sayısı buradan sayılır.
/// </summary>
public class LoginAttempt
{
    public long Id { get; set; }

    /// <summary>Sayacın anahtarı: "email:foo@bar" ya da "ip:1.2.3.4".</summary>
    [MaxLength(200)] public string Key { get; set; } = "";

    public DateTimeOffset At { get; set; }
}

/// <summary>Testçinin nakit çekim talebi.</summary>
public class Withdrawal
{
    [MaxLength(64)] public string Id { get; set; } = "";
    [MaxLength(64)] public string UserId { get; set; } = "";
    public decimal Amount { get; set; }

    /// <summary>pending | paid | rejected</summary>
    [MaxLength(20)] public string Status { get; set; } = "pending";

    public long RequestedAt { get; set; }
    public long? ResolvedAt { get; set; }
    [MaxLength(64)] public string? Iban { get; set; }

    /// <summary>Ret gerekçesi (testçiye gösterilir).</summary>
    public string? Note { get; set; }
}
