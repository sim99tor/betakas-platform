using Betakas.Api.Data;
using Betakas.Api.Dto;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

/// <summary>
/// Para tarafı: token paketi satın alma, abonelik dönemleri ve testçi çekimleri.
/// Her tahsilatta tutar komisyon (platform geliri) ve ödül havuzu olarak ikiye ayrılır.
/// </summary>
public class BillingActions(BetakasDbContext db, LedgerService ledger, EconomyService economy)
{
    /// <summary>Demo ödeme kapısı: gerçek bir sağlayıcı yok, kart numarası biçimsel olarak denetlenir.</summary>
    private static bool ValidCard(string? card) =>
        (card ?? "").Replace(" ", "").Replace("-", "").Length >= 12;

    private async Task<string> NextInvoiceNoAsync()
    {
        var count = await db.Purchases.CountAsync();
        return $"BTK-{DateTime.UtcNow.Year}-{1000 + count + 1}";
    }

    // ---- Tek seferlik paket ----

    public async Task<DomainResult> BuyPackageAsync(User me, string packageId, string? card)
    {
        if (me.Role != "founder") return DomainResult.Denied("Token yalnızca kurucular satın alır.");

        var pkg = EconomyService.GetPackage(packageId);
        if (pkg is null) return DomainResult.Invalid("Geçersiz paket.");
        if (!ValidCard(card)) return DomainResult.Invalid("Geçerli bir kart numarası gir (demo: 4242 4242 4242 4242).");

        var gross = await economy.PackagePriceAsync(pkg);
        var fee = Math.Round(gross * await economy.FeePctAsync() / 100m, MidpointRounding.AwayFromZero);
        var pool = gross - fee;
        var invoiceNo = await NextInvoiceNoAsync();

        db.Purchases.Add(new Purchase
        {
            Id = await ledger.NextIdAsync("pu"), Ts = LedgerService.Now(), UserId = me.Id,
            PackageId = pkg.Id, PackageName = pkg.Name, Tokens = pkg.Tokens, Testers = pkg.Testers,
            Gross = gross, Fee = fee, Pool = pool, InvoiceNo = invoiceNo
        });

        await ledger.PostCashAsync(me.Id, "revenue", fee, "commission", pkg.Id,
            $"Platform komisyonu (%{await economy.FeePctAsync()}) · {pkg.Name} paketi → {me.Name}");
        await ledger.PostCashAsync(me.Id, "pool", pool, "purchase", pkg.Id,
            $"Testçi ödül havuzuna aktarıldı · {pkg.Name} paketi → {me.Name}");
        await ledger.PostAsync("system", me.Id, pkg.Tokens, "token_purchase", null,
            $"{pkg.Name} paketi satın alındı ({pkg.Tokens} token · {pkg.Testers} testçi)");

        await db.SaveChangesAsync();
        return DomainResult.Success($"Ödeme alındı · {pkg.Tokens} token tanımlandı · fatura {invoiceNo}.");
    }

    // ---- Abonelik ----

    public async Task<DomainResult> SubscribeAsync(User me, string planId, string? card)
    {
        if (me.Role != "founder") return DomainResult.Denied("Abonelik yalnızca kurucular içindir.");

        var plan = EconomyService.GetPlan(planId);
        if (plan is null) return DomainResult.Invalid("Geçersiz plan.");
        if (!ValidCard(card)) return DomainResult.Invalid("Geçerli bir kart numarası gir (demo: 4242 4242 4242 4242).");
        if (EconomyService.ActivePlanOf(me) != null)
            return DomainResult.Conflict("Zaten aktif bir aboneliğin var. Önce iptal et.");

        me.SubscriptionPlanId = plan.Id;
        me.SubscriptionActive = true;
        me.SubscriptionRenewsAt = LedgerService.Now();

        // Abonelik başlarken ilk dönem hemen tahsil edilir ve tokenler basılır.
        var charged = await ChargePeriodAsync(me, plan, "Abonelik başlatıldı");
        await db.SaveChangesAsync();
        return DomainResult.Success(charged);
    }

    public async Task<DomainResult> CancelSubscriptionAsync(User me)
    {
        var plan = EconomyService.ActivePlanOf(me);
        if (plan is null) return DomainResult.Conflict("Aktif aboneliğin yok.");

        // Basılmış tokenler hesapta kalır; yalnızca yenileme durur.
        me.SubscriptionActive = false;
        await db.SaveChangesAsync();
        return DomainResult.Success($"{plan.Name} aboneliği iptal edildi. Mevcut tokenlerin hesabında kalır.");
    }

    /// <summary>Demo ortamında zamanlayıcı yok: bir dönemi elle ilerletir.</summary>
    public async Task<DomainResult> RenewAsync(User me)
    {
        var plan = EconomyService.ActivePlanOf(me);
        if (plan is null) return DomainResult.Conflict("Aktif aboneliğin yok.");

        var msg = await ChargePeriodAsync(me, plan, "Aylık yenileme");
        await db.SaveChangesAsync();
        return DomainResult.Success(msg);
    }

    /// <summary>
    /// Bir abonelik dönemini tahsil eder: tokenleri basar, parayı komisyon/havuz olarak
    /// ayırır, fatura kaydı düşer ve sonraki yenileme tarihini bir ay ileri alır.
    /// </summary>
    private async Task<string> ChargePeriodAsync(User u, SubscriptionPlan plan, string reason)
    {
        var split = await economy.PlanSplitAsync(plan);
        var invoiceNo = await NextInvoiceNoAsync();

        db.Purchases.Add(new Purchase
        {
            Id = await ledger.NextIdAsync("pu"), Ts = LedgerService.Now(), UserId = u.Id, Kind = "subscription",
            PackageId = plan.Id, PackageName = plan.Name + " aboneliği",
            Tokens = plan.Tokens, Testers = plan.Testers,
            Gross = split.Gross, Fee = split.Fee, Pool = split.Pool, InvoiceNo = invoiceNo
        });

        await ledger.PostCashAsync(u.Id, "revenue", split.Fee, "subscription_fee", plan.Id,
            $"Abonelik komisyonu · {plan.Name} → {u.Name}");
        await ledger.PostCashAsync(u.Id, "pool", split.Pool, "subscription", plan.Id,
            $"Testçi ödül havuzuna aktarıldı · {plan.Name} aboneliği → {u.Name}");
        await ledger.PostAsync("system", u.Id, plan.Tokens, "subscription_renewal", plan.Id,
            $"{plan.Name} aboneliği {reason} ({plan.Tokens} token)");

        u.SubscriptionRenewsAt = LedgerService.Now() + EconomyService.MonthMs;

        return $"{reason} · {plan.Tokens} token eklendi · {split.Gross:N2} ₺ tahsil edildi (fatura {invoiceNo}).";
    }

    // ---- Çekim ----

    public async Task<DomainResult> RequestWithdrawalAsync(User me, decimal amount, string? iban)
    {
        if (me.Role != "tester") return DomainResult.Denied("Nakit çekimi yalnızca testçiler yapar.");

        var available = await ledger.WithdrawableAsync(me.Id);
        if (available < EconomyService.MinWithdrawal)
            return DomainResult.Invalid(
                $"Minimum çekim tutarı {EconomyService.MinWithdrawal:N2} ₺. Çekilebilir bakiyen: {available:N2} ₺.");

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0) return DomainResult.Invalid("Geçerli bir tutar gir.");
        if (amount < EconomyService.MinWithdrawal)
            return DomainResult.Invalid($"Minimum çekim tutarı {EconomyService.MinWithdrawal:N2} ₺.");
        if (amount > available)
            return DomainResult.Invalid($"Çekilebilir bakiyenden fazla talep edemezsin ({available:N2} ₺).");

        var acct = (iban ?? "").Trim();
        if (acct.Length < 8) return DomainResult.Invalid("Geçerli bir IBAN gir.");

        me.Iban = acct;
        db.Withdrawals.Add(new Withdrawal
        {
            Id = await ledger.NextIdAsync("w"), UserId = me.Id, Amount = amount,
            Status = "pending", RequestedAt = LedgerService.Now(), Iban = acct
        });

        await db.SaveChangesAsync();
        return DomainResult.Success($"Çekim talebin oluşturuldu: {amount:N2} ₺ · yönetim onayından sonra ödenir.");
    }

    public async Task<DomainResult> ResolveWithdrawalAsync(User me, string id, string outcome, string? note)
    {
        if (me.Role != "admin") return DomainResult.Denied("Çekimleri yalnızca yönetim sonuçlandırır.");
        if (outcome != "paid" && outcome != "rejected")
            return DomainResult.Invalid("Karar 'paid' ya da 'rejected' olmalı.");

        var w = await db.Withdrawals.FirstOrDefaultAsync(x => x.Id == id);
        if (w is null) return DomainResult.Missing("Çekim talebi bulunamadı.");
        if (w.Status != "pending") return DomainResult.Conflict("Bu talep zaten sonuçlandırılmış.");

        var u = await db.Users.FirstAsync(x => x.Id == w.UserId);

        if (outcome == "paid")
        {
            if (await ledger.CashBalanceAsync(w.UserId) < w.Amount)
                return DomainResult.Conflict("Testçinin bakiyesi bu çekimi karşılamıyor.");

            w.Status = "paid";
            w.ResolvedAt = LedgerService.Now();
            await ledger.PostCashAsync(w.UserId, "bank", w.Amount, "withdrawal", w.Id, $"Çekim ödendi → {u.Name}");

            await db.SaveChangesAsync();
            return DomainResult.Success($"{w.Amount:N2} ₺ ödendi → {u.Name}.");
        }

        var reason = (note ?? "").Trim();
        if (reason.Length < 5) return DomainResult.Invalid("Ret gerekçesi zorunlu.");

        w.Status = "rejected";
        w.ResolvedAt = LedgerService.Now();
        w.Note = reason;

        await db.SaveChangesAsync();
        return DomainResult.Success("Çekim talebi reddedildi — bakiye testçide kaldı.");
    }
}
