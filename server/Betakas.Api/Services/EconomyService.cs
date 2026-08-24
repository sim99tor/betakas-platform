using Betakas.Api.Data;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

public record TokenPackage(string Id, string Name, int Testers, int Tokens);

public record SubscriptionPlan(string Id, string Name, decimal Price, int Tokens, int Testers, bool Priority);

public record PlanSplit(decimal Gross, decimal Fee, decimal Pool);

/// <summary>
/// Token ekonomisinin kuralları. 1 token = 1 test kredisi; kurucu tokeni gerçek parayla
/// alır, tutar komisyon (platform geliri) ve testçi ödül havuzu olarak ikiye ayrılır.
/// </summary>
public class EconomyService(BetakasDbContext db)
{
    public const int BoostCost = 10;
    public const decimal MinWithdrawal = 150m;
    public const int DisputePenaltyRating = 1;
    public const long MonthMs = 30L * 24 * 60 * 60 * 1000;

    public static readonly TokenPackage[] Packages =
    [
        new("p1", "Başlangıç", 3, 45),
        new("p2", "Büyüme", 10, 150),
        new("p3", "Ölçek", 25, 375)
    ];

    public static readonly SubscriptionPlan[] Plans =
    [
        new("sp1", "Başlangıç", 600m, 60, 4, false),
        new("sp2", "Büyüme", 1800m, 200, 13, true)
    ];

    public static TokenPackage? GetPackage(string id) => Packages.FirstOrDefault(p => p.Id == id);

    public static SubscriptionPlan? GetPlan(string id) => Plans.FirstOrDefault(p => p.Id == id);

    private PlatformState? _state;

    public async Task<PlatformState> StateAsync() =>
        _state ??= await db.PlatformState.FirstAsync(x => x.Id == 1);

    public async Task<decimal> TokenPriceAsync() => (await StateAsync()).TokenPrice;

    public async Task<int> FeePctAsync() => (await StateAsync()).FeePct;

    /// <summary>Testçiye ödenen ₺/token — komisyon düşüldükten sonra kalan.</summary>
    public async Task<decimal> PayoutRateAsync()
    {
        var st = await StateAsync();
        return Math.Round(st.TokenPrice * (100 - st.FeePct)) / 100m;
    }

    public async Task<decimal> PackagePriceAsync(TokenPackage pkg) => pkg.Tokens * await TokenPriceAsync();

    /// <summary>
    /// Abonelikte havuz payı ÖNCE sabitlenir: basılan tokenin testçi karşılığı havuza yatar,
    /// kalan tutar platform gelirine yazılır. Böylece abonelik indirimi platform marjından
    /// karşılanır, testçi yükümlülüğü hiçbir zaman eksik fonlanmaz.
    /// </summary>
    public async Task<PlanSplit> PlanSplitAsync(SubscriptionPlan plan)
    {
        var rate = await PayoutRateAsync();
        var pool = Math.Min(plan.Price, Math.Round(plan.Tokens * rate, MidpointRounding.AwayFromZero));
        return new PlanSplit(plan.Price, plan.Price - pool, pool);
    }

    /// <summary>Kullanıcının aktif aboneliği; yoksa null.</summary>
    public static SubscriptionPlan? ActivePlanOf(User u) =>
        u.SubscriptionActive && u.SubscriptionPlanId != null ? GetPlan(u.SubscriptionPlanId) : null;
}
