using Betakas.Api.Data;
using Betakas.Api.Dto;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

/// <summary>
/// Test talebi eylemleri. Escrow kilidi burada kurulur ve burada çözülür — istemci
/// hiçbir defter satırını kendisi yazamaz.
/// </summary>
public class RequestActions(BetakasDbContext db, LedgerService ledger)
{
    public static readonly string[] ProductCategories =
        ["SaaS / B2B", "Mobil Uygulama", "E-ticaret", "AI Aracı", "Tüketici Uygulaması (B2C)", "Diğer"];

    public static readonly string[] Stages = ["Fikir/Prototip", "MVP", "Büyüme"];

    public static readonly string[] FeedbackTypes =
        ["Bug Avı", "UX / Kullanılabilirlik", "İlk İzlenim", "Onboarding Akışı"];

    public const string DefaultStage = "MVP";

    /// <summary>Talep açar ve slot × token kadar bakiyeyi escrow'a kilitler.</summary>
    public async Task<DomainResult> CreateAsync(User me, CreateRequestDto dto)
    {
        if (me.Role != "founder") return DomainResult.Denied("Yalnızca kurucular test talebi açabilir.");

        var title = (dto.Title ?? "").Trim();
        var scenario = (dto.Scenario ?? "").Trim();
        if (title.Length == 0 || scenario.Length == 0)
            return DomainResult.Invalid("Başlık ve senaryo zorunlu.");

        var version = await db.Versions.FirstOrDefaultAsync(v => v.Id == dto.VersionId);
        if (version is null) return DomainResult.Invalid("Geçerli bir ürün sürümü seç.");
        if (version.OwnerId != me.Id) return DomainResult.Denied("Yalnızca kendi sürümün için talep açabilirsin.");
        if (string.IsNullOrWhiteSpace(version.Url)) return DomainResult.Invalid("Seçilen sürümün linki yok.");

        if (string.IsNullOrWhiteSpace(dto.ProductCategory) || !ProductCategories.Contains(dto.ProductCategory))
            return DomainResult.Invalid("Ürün kategorisi seçmelisin — testçi eşleştirmesi buna göre yapılıyor.");

        var stage = Stages.Contains(dto.Stage) ? dto.Stage! : DefaultStage;
        var type = FeedbackTypes.Contains(dto.FeedbackType) ? dto.FeedbackType! : FeedbackTypes[0];

        if (dto.Credits < 5 || dto.Slots < 1)
            return DomainResult.Invalid("En az 5 token ve 1 slot gerekli.");
        if (dto.Slots > 20)
            return DomainResult.Invalid("Bir talepte en fazla 20 slot açılabilir.");

        var total = dto.Credits * dto.Slots;
        var balance = await ledger.BalanceAsync(me.Id);
        if (total > balance)
            return DomainResult.Invalid($"Yetersiz token. Gereken {total}, bakiyen {balance}.");

        var id = await ledger.NextIdAsync("r");
        db.Requests.Add(new TestRequest
        {
            Id = id,
            OwnerId = me.Id,
            VersionId = version.Id,
            Title = title,
            Url = version.Url,
            ProductCategory = dto.ProductCategory,
            Stage = stage,
            FeedbackType = type,
            Scenario = scenario,
            Credits = dto.Credits,
            Slots = dto.Slots,
            Visibility = dto.ExcludeSector ? "exclude-sector" : "public",
            Status = "open",
            CreatedAt = LedgerService.Now()
        });

        await ledger.PostAsync(me.Id, "escrow", total, "escrow_lock", id,
            $"Test talebi açıldı: {title} ({dto.Slots} slot × {dto.Credits})");

        await db.SaveChangesAsync();
        return DomainResult.Success($"Talep açıldı · {total} token escrow'a bloke edildi.");
    }

    /// <summary>Keşfet'te öne çıkarır; bedeli sistemden yakılır, iade edilmez.</summary>
    public async Task<DomainResult> BoostAsync(User me, string requestId)
    {
        var r = await db.Requests.FirstOrDefaultAsync(x => x.Id == requestId);
        if (r is null) return DomainResult.Missing("Talep bulunamadı.");
        if (r.OwnerId != me.Id) return DomainResult.Denied("Yalnızca kendi talebini öne çıkarabilirsin.");
        if (r.Boosted) return DomainResult.Conflict("Bu talep zaten öne çıkarılmış.");
        if (r.Status != "open") return DomainResult.Conflict("Kapalı talep öne çıkarılamaz.");

        if (await ledger.BalanceAsync(me.Id) < EconomyService.BoostCost)
            return DomainResult.Invalid("Öne çıkarmak için yetersiz token.");

        await ledger.PostAsync(me.Id, "system", EconomyService.BoostCost, "boost_burn", r.Id,
            $"Talep öne çıkarıldı (Keşfet'te üstte): {r.Title}");

        r.Boosted = true;
        r.BoostedAt = LedgerService.Now();

        await db.SaveChangesAsync();
        return DomainResult.Success("Talebin Keşfet'te öne çıkarıldı.");
    }

    /// <summary>Talebi kapatır ve escrow'da kalan tokeni sahibine iade eder.</summary>
    public async Task<DomainResult> CloseAsync(User me, string requestId)
    {
        var r = await db.Requests.FirstOrDefaultAsync(x => x.Id == requestId);
        if (r is null) return DomainResult.Missing("Talep bulunamadı.");
        if (r.OwnerId != me.Id && me.Role != "admin")
            return DomainResult.Denied("Yalnızca talep sahibi kapatabilir.");
        if (r.Status == "closed") return DomainResult.Conflict("Talep zaten kapalı.");

        // Teslim edilmiş ama karara bağlanmamış oturum varsa kapatma escrow'u kilitler.
        var openSessions = await db.Sessions.CountAsync(s =>
            s.RequestId == r.Id && (s.Status == "submitted" || s.Status == "disputed"));
        if (openSessions > 0)
            return DomainResult.Conflict($"Karara bağlanmamış {openSessions} teslim var — önce onları sonuçlandır.");

        var remaining = await ledger.EscrowRemainingAsync(r.Id);
        r.Status = "closed";

        if (remaining > 0)
            await ledger.PostAsync("escrow", r.OwnerId, remaining, "escrow_refund", r.Id,
                $"Talep kapatıldı, kalan token iade edildi: {r.Title}");

        await db.SaveChangesAsync();
        return DomainResult.Success($"Talep kapatıldı · {remaining} token iade edildi.");
    }
}
