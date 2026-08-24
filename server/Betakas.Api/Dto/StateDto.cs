using System.Text.Json.Nodes;

namespace Betakas.Api.Dto;

/// <summary>
/// İstemcinin `S` global state objesinin birebir karşılığı. Alan adları ve şekli kasıtlı olarak
/// mevcut arayüzle aynıdır; böylece istemci tarafındaki iş mantığı (escrow, defter, itibar,
/// sürüm notu) hiç değişmeden sunucudan beslenebilir.
/// </summary>
public class StateDto
{
    /// <summary>Sunucudaki revizyon sayacı — iyimser eşzamanlılık için geri gönderilir.</summary>
    public long Rev { get; set; }

    public int Version { get; set; }

    /// <summary>Oturumdaki kullanıcı; sunucu JWT'den doldurur, istemci yazamaz.</summary>
    public string? AuthUserId { get; set; }

    public SettingsDto Settings { get; set; } = new();
    public int SprintNo { get; set; }
    public int Seq { get; set; }

    public List<UserDto> Users { get; set; } = new();
    public List<VersionDto> Versions { get; set; } = new();
    public List<RequestDto> Requests { get; set; } = new();
    public List<SessionDto> Sessions { get; set; } = new();
    public List<LedgerDto> Ledger { get; set; } = new();
    public List<CashLedgerDto> CashLedger { get; set; } = new();
    public List<PurchaseDto> Purchases { get; set; } = new();
    public List<WithdrawalDto> Withdrawals { get; set; } = new();
}

public class SettingsDto
{
    public decimal TokenPrice { get; set; }
    public int FeePct { get; set; }
}

public class SubscriptionDto
{
    public string? PlanId { get; set; }
    public long? RenewsAt { get; set; }
    public bool Active { get; set; }
}

/// <summary>Parola alanı bilinçli olarak yoktur — hash sunucudan hiç çıkmaz.</summary>
public class UserDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Initials { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Startup { get; set; }
    public string? Title { get; set; }
    public string? Tagline { get; set; }
    public string? Sector { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<string> ExpertiseCategories { get; set; } = new();
    public string? ExpertiseOther { get; set; }
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public SubscriptionDto? Subscription { get; set; }

    /// <summary>Yalnızca oturumdaki kullanıcının kendi kaydında doldurulur.</summary>
    public string? Iban { get; set; }
}

public class VersionDto
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Url { get; set; }
    public long CreatedAt { get; set; }
    public string? Notes { get; set; }
    public JsonObject? Fixes { get; set; }
}

public class RequestDto
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string? VersionId { get; set; }
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public string? ProductCategory { get; set; }
    public string? Stage { get; set; }
    public string? FeedbackType { get; set; }
    public string? Scenario { get; set; }
    public int Credits { get; set; }
    public int Slots { get; set; }
    public string Visibility { get; set; } = "public";
    public string Status { get; set; } = "open";
    public long CreatedAt { get; set; }
    public bool Boosted { get; set; }
    public long? BoostedAt { get; set; }
}

public class SessionDto
{
    public string Id { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string TesterId { get; set; } = "";
    public string Status { get; set; } = "";
    public long? AppliedAt { get; set; }
    public long? SubmittedAt { get; set; }
    public int? Rating { get; set; }
    public int? OwnerRating { get; set; }
    public int? DurationMin { get; set; }
    public string? ProofUrl { get; set; }
    public JsonObject? Feedback { get; set; }
    public string? DisputeNote { get; set; }
    public string? DisputeOutcome { get; set; }
    public decimal? CashPaid { get; set; }
}

public class LedgerDto
{
    public string Id { get; set; } = "";
    public long Ts { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Amount { get; set; }
    public string Type { get; set; } = "";
    public string? Ref { get; set; }
    public string? Note { get; set; }
}

public class CashLedgerDto
{
    public string Id { get; set; } = "";
    public long Ts { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public string? Ref { get; set; }
    public string? Note { get; set; }
}

public class PurchaseDto
{
    public string Id { get; set; } = "";
    public long Ts { get; set; }
    public string UserId { get; set; } = "";
    public string? Kind { get; set; }
    public string PackageId { get; set; } = "";
    public string PackageName { get; set; } = "";
    public int Tokens { get; set; }
    public int Testers { get; set; }
    public decimal Gross { get; set; }
    public decimal Fee { get; set; }
    public decimal Pool { get; set; }
    public string InvoiceNo { get; set; } = "";
}

public class WithdrawalDto
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public long RequestedAt { get; set; }
    public long? ResolvedAt { get; set; }
    public string? Iban { get; set; }
}

// --- Auth ---

public record LoginRequest(string Email, string Password, string? Role);

public record RegisterRequest(
    string Name, string Email, string Password, string Role,
    string? Org, string? Tagline, string? Sector,
    List<string>? ExpertiseCategories, string? ExpertiseOther);

public record AuthResponse(string Token, UserDto User);

public record RefreshRequest(string? RefreshToken);
