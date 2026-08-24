using System.Text.Json.Nodes;

namespace Betakas.Api.Dto;

// --- Talep ---

public record CreateRequestDto(
    string? Title,
    string? VersionId,
    string? ProductCategory,
    string? Stage,
    string? FeedbackType,
    string? Scenario,
    int Credits,
    int Slots,
    bool ExcludeSector);

// --- Sürüm ---

public record CreateVersionDto(string? Label, string? Url, string? Notes, JsonObject? Fixes);

public record SaveFixesDto(JsonObject? Fixes);

// --- Oturum ---

/// <summary>
/// Teslim yükü. <see cref="Fields"/> anahtarları ürün aşamasının şablonundan gelir
/// (firstImpression / bugs / ux / valueProp / dropOff …); sunucu şablona göre doğrular.
/// </summary>
public record SubmitFeedbackDto(
    Dictionary<string, string>? Fields,
    string? WouldUse,
    string? WouldUseWhy,
    string? ProofUrl,
    int DurationMin);

public record RatingDto(int Rating);

public record DisputeDto(string? Note);

public record ResolveDisputeDto(string? Outcome);

// --- Faturalama ---

public record BuyPackageDto(string? PackageId, string? Card);

public record SubscribeDto(string? PlanId, string? Card);

public record WithdrawalDto2(decimal Amount, string? Iban);

public record ResolveWithdrawalDto(string? Outcome, string? Note);

// --- Yönetim ---

public record DecimalValueDto(decimal Value);

// --- Profil ---

public record ExpertiseDto(List<string>? Categories, string? Other);
