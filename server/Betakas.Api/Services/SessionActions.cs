using System.Text.Json.Nodes;
using Betakas.Api.Data;
using Betakas.Api.Dto;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

/// <summary>
/// Test oturumunun yaşam döngüsü: başvuru → onay → teslim → puanlama.
/// Escrow'dan token serbest bırakma ve havuzdan nakit ödeme yalnızca burada olur.
/// </summary>
public class SessionActions(
    BetakasDbContext db,
    LedgerService ledger,
    EconomyService economy,
    ReputationService reputation)
{
    // ---- Testçi tarafı ----

    public async Task<DomainResult> ApplyAsync(User me, string requestId)
    {
        var r = await db.Requests.FirstOrDefaultAsync(x => x.Id == requestId);
        if (r is null) return DomainResult.Missing("Talep bulunamadı.");
        if (r.Status != "open") return DomainResult.Conflict("Bu talep kapalı.");
        if (r.OwnerId == me.Id) return DomainResult.Invalid("Kendi talebine başvuramazsın.");
        if (me.Status != "active") return DomainResult.Denied("Hesabın henüz onaylanmadı.");

        if (await db.Sessions.AnyAsync(s => s.RequestId == requestId && s.TesterId == me.Id && s.Status != "rejected"))
            return DomainResult.Conflict("Bu talebe zaten başvurdun.");

        if (await ledger.SlotsLeftAsync(r) <= 0)
            return DomainResult.Conflict("Bu talebin slotları doldu.");

        // Rakip gizliliği: aynı sektördeki kurucular kapalı talepleri göremez/başvuramaz.
        if (r.Visibility == "exclude-sector")
        {
            var owner = await db.Users.FirstAsync(u => u.Id == r.OwnerId);
            if (me.Sector != null && owner.Sector != null && me.Sector == owner.Sector)
                return DomainResult.Denied("Bu talep aynı sektördeki kullanıcılara kapalı.");
        }

        db.Sessions.Add(new TestSession
        {
            Id = await ledger.NextIdAsync("s"),
            RequestId = requestId,
            TesterId = me.Id,
            Status = "applied",
            AppliedAt = LedgerService.Now(),
            ProofUrl = ""
        });

        await db.SaveChangesAsync();
        return DomainResult.Success("Başvurun gönderildi. Talep sahibi onaylayınca test edebilirsin.");
    }

    /// <summary>Teslim: kalite kapısı (alan uzunlukları, kanıt linki, süre) sunucuda uygulanır.</summary>
    public async Task<DomainResult> SubmitAsync(User me, string sessionId, SubmitFeedbackDto dto)
    {
        var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId);
        if (s is null) return DomainResult.Missing("Oturum bulunamadı.");
        if (s.TesterId != me.Id) return DomainResult.Denied("Bu oturum sana ait değil.");
        if (s.Status != "approved") return DomainResult.Conflict("Yalnızca onaylanmış bir başvuru teslim edilebilir.");

        var r = await db.Requests.FirstAsync(x => x.Id == s.RequestId);
        var tpl = FeedbackTemplates.For(r.Stage);

        var feedback = new JsonObject();
        foreach (var f in tpl.Fields)
        {
            var val = (dto.Fields != null && dto.Fields.TryGetValue(f.Key, out var v) ? v : "")?.Trim() ?? "";
            if (val.Length < f.Min)
                return DomainResult.Invalid($"\"{f.Label}\" en az {f.Min} karakter olmalı.");
            feedback[f.Key] = val;
        }

        var why = (dto.WouldUseWhy ?? "").Trim();
        if (why.Length == 0) return DomainResult.Invalid($"'{tpl.WhyLabel}' alanı zorunlu.");
        if (!FeedbackTemplates.Choices.Contains(dto.WouldUse))
            return DomainResult.Invalid("Karar sorusunu yanıtla.");

        var proof = (dto.ProofUrl ?? "").Trim();
        if (!proof.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return DomainResult.Invalid("Geçerli bir ekran kaydı linki zorunlu (test kanıtı).");
        if (dto.DurationMin < 5)
            return DomainResult.Invalid("Test süresi en az 5 dakika olmalı.");

        feedback["wouldUse"] = dto.WouldUse;
        feedback["wouldUseWhy"] = why;

        s.Feedback = feedback;
        s.ProofUrl = proof;
        s.DurationMin = dto.DurationMin;
        s.Status = "submitted";
        s.SubmittedAt = LedgerService.Now();

        await db.SaveChangesAsync();
        return DomainResult.Success("Test teslim edildi. Talep sahibi onaylayınca tokenin serbest kalır.");
    }

    /// <summary>Testçinin kurucuyu değerlendirmesi (iki yönlü puanlama).</summary>
    public async Task<DomainResult> RateOwnerAsync(User me, string sessionId, int rating)
    {
        if (rating is < 1 or > 5) return DomainResult.Invalid("Puan 1-5 arasında olmalı.");

        var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId);
        if (s is null) return DomainResult.Missing("Oturum bulunamadı.");
        if (s.TesterId != me.Id) return DomainResult.Denied("Bu oturum sana ait değil.");
        if (s.Status != "accepted") return DomainResult.Conflict("Yalnızca kabul edilmiş testten sonra puan verebilirsin.");
        if (s.OwnerRating != null) return DomainResult.Conflict("Bu test için zaten puan verdin.");

        s.OwnerRating = rating;
        await db.SaveChangesAsync();
        return DomainResult.Success("Değerlendirmen kaydedildi.");
    }

    // ---- Kurucu tarafı ----

    public async Task<DomainResult> ApproveApplicationAsync(User me, string sessionId)
    {
        var (s, r, err) = await LoadOwnedAsync(me, sessionId);
        if (err != null) return err;
        if (s!.Status != "applied") return DomainResult.Conflict("Bu başvuru zaten işlenmiş.");

        // Onay bir slotu bağlar; arada slot dolduysa onaylanamaz.
        if (await ledger.SlotsLeftAsync(r!) < 0)
            return DomainResult.Conflict("Talebin slotları dolu.");

        s.Status = "approved";
        await db.SaveChangesAsync();
        return DomainResult.Success("Başvuru onaylandı.");
    }

    public async Task<DomainResult> RejectApplicationAsync(User me, string sessionId)
    {
        var (s, _, err) = await LoadOwnedAsync(me, sessionId);
        if (err != null) return err;
        if (s!.Status != "applied") return DomainResult.Conflict("Yalnızca bekleyen başvuru reddedilebilir.");

        // Silmek yerine "rejected" işaretlenir: slot serbest kalır, kayıt izlenebilir kalır.
        s.Status = "rejected";
        await db.SaveChangesAsync();
        return DomainResult.Success("Başvuru reddedildi.");
    }

    /// <summary>
    /// Teslimi kabul eder ve puanlar. Üç para hareketi olur:
    /// escrow → testçi (token), sistem → testçi (itibar bonusu), havuz → testçi (₺).
    /// </summary>
    public async Task<DomainResult> AcceptAsync(User me, string sessionId, int rating)
    {
        if (rating is < 1 or > 5) return DomainResult.Invalid("Puan 1-5 arasında olmalı.");

        var (s, r, err) = await LoadOwnedAsync(me, sessionId);
        if (err != null) return err;
        if (s!.Status != "submitted") return DomainResult.Conflict("Yalnızca teslim edilmiş bir test kabul edilebilir.");

        var tester = await db.Users.FirstAsync(u => u.Id == s.TesterId);

        // Çarpan, bu testin puanı işlenmeden ÖNCEKİ itibara göre hesaplanır ki
        // başvuru anında gösterilen kazanç önizlemesiyle tutarlı kalsın.
        var mult = await reputation.MultiplierAsync(s.TesterId);

        // Escrow'da bu talep için yeterli token kalmış olmalı — aksi halde defter bozulur.
        if (await ledger.EscrowRemainingAsync(r!.Id) < r.Credits)
            return DomainResult.Conflict("Bu talebin escrow bakiyesi yetersiz — yönetimle iletişime geç.");

        s.Status = "accepted";
        s.Rating = rating;

        await ledger.PostAsync("escrow", s.TesterId, r.Credits, "escrow_release", r.Id,
            $"Feedback onaylandı → {tester.Name} ({rating}★): {r.Title}");

        // İtibar bonusu sistemden basılır ki escrow matematiği bozulmasın.
        var bonus = (int)Math.Round(r.Credits * (mult - 1m), MidpointRounding.AwayFromZero);
        if (bonus > 0)
            await ledger.PostAsync("system", s.TesterId, bonus, "rep_bonus", r.Id,
                $"İtibar çarpanı bonusu ({mult}×) → {tester.Name}");

        var cash = await PayTesterAsync(s, r, tester);

        await db.SaveChangesAsync();

        var msg = $"{r.Credits} token escrow'dan {tester.Name} hesabına geçti";
        if (bonus > 0) msg += $" + {bonus} itibar bonusu ({mult}×)";
        if (cash > 0) msg += $" · ödül havuzundan {cash:N2} ₺ nakit yazıldı";
        return DomainResult.Success(msg + ".");
    }

    public async Task<DomainResult> DisputeAsync(User me, string sessionId, string? note)
    {
        var (s, _, err) = await LoadOwnedAsync(me, sessionId);
        if (err != null) return err;
        if (s!.Status != "submitted") return DomainResult.Conflict("Yalnızca teslim edilmiş bir teste itiraz edilebilir.");

        var text = (note ?? "").Trim();
        if (text.Length < 10) return DomainResult.Invalid("İtiraz gerekçesi en az 10 karakter olmalı.");

        s.Status = "disputed";
        s.DisputeNote = text;
        await db.SaveChangesAsync();
        return DomainResult.Success("İtiraz kaydedildi — token escrow'da kilitli kaldı, yönetim karar verecek.");
    }

    // ---- Yönetim tarafı ----

    /// <summary>Anlaşmazlığı sonuçlandırır: token testçiye verilir ya da kurucuya iade edilir.</summary>
    public async Task<DomainResult> ResolveDisputeAsync(User me, string sessionId, string outcome)
    {
        if (me.Role != "admin") return DomainResult.Denied("Anlaşmazlığı yalnızca yönetim karara bağlar.");
        if (outcome != "release" && outcome != "refund")
            return DomainResult.Invalid("Karar 'release' ya da 'refund' olmalı.");

        var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId);
        if (s is null) return DomainResult.Missing("Oturum bulunamadı.");
        if (s.Status != "disputed") return DomainResult.Conflict("Bu oturumda bekleyen bir itiraz yok.");

        var r = await db.Requests.FirstAsync(x => x.Id == s.RequestId);
        var tester = await db.Users.FirstAsync(u => u.Id == s.TesterId);

        if (await ledger.EscrowRemainingAsync(r.Id) < r.Credits)
            return DomainResult.Conflict("Talebin escrow bakiyesi yetersiz.");

        s.DisputeOutcome = outcome;

        if (outcome == "release")
        {
            s.Status = "accepted";
            s.Rating = 3; // yönetim kararıyla standart puan
            await ledger.PostAsync("escrow", s.TesterId, r.Credits, "escrow_release", r.Id,
                $"Yönetim kararı: token test edene verildi → {tester.Name}");
            var cash = await PayTesterAsync(s, r, tester);
            await db.SaveChangesAsync();
            return DomainResult.Success($"Karar: token {tester.Name} hesabına verildi" +
                (cash > 0 ? $" · {cash:N2} ₺ nakit ödendi" : "") + ".");
        }

        // İade: token kurucuya döner, testçinin itibarına ceza yansır (bkz. ReputationService).
        s.Status = "rejected";
        await ledger.PostAsync("escrow", r.OwnerId, r.Credits, "escrow_refund", r.Id,
            $"Yönetim kararı: token talep sahibine iade edildi ({r.Title})");

        await db.SaveChangesAsync();
        return DomainResult.Success($"Karar: token talep sahibine iade edildi — {tester.Name}'in itibarına ceza yansıdı.");
    }

    // ---- Yardımcılar ----

    /// <summary>
    /// Onaylanan testin parasal karşılığı. Yalnızca testçi rolü havuzdan ödeme alır:
    /// kurucular arası takas token ekonomisinde kalır, promosyon tokenlerinin nakit
    /// karşılığı yoktur. Havuz yetersizse token aktarılır ama nakit ödenmez.
    /// </summary>
    private async Task<decimal> PayTesterAsync(TestSession session, TestRequest request, User tester)
    {
        if (tester.Role != "tester") return 0m;

        var rate = await economy.PayoutRateAsync();
        var amount = Math.Round(request.Credits * rate, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0) return 0m;

        if (await ledger.CashBalanceAsync("pool") < amount) return 0m;

        await ledger.PostCashAsync("pool", tester.Id, amount, "test_payout", request.Id,
            $"Test ödemesi: {request.Title} ({request.Credits} token × {rate:N2} ₺)");
        session.CashPaid = amount;
        return amount;
    }

    /// <summary>Oturumu yükler ve çağıranın talebin sahibi olduğunu doğrular.</summary>
    private async Task<(TestSession?, TestRequest?, DomainResult?)> LoadOwnedAsync(User me, string sessionId)
    {
        var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId);
        if (s is null) return (null, null, DomainResult.Missing("Oturum bulunamadı."));

        var r = await db.Requests.FirstOrDefaultAsync(x => x.Id == s.RequestId);
        if (r is null) return (null, null, DomainResult.Missing("Talep bulunamadı."));

        if (r.OwnerId != me.Id)
            return (null, null, DomainResult.Denied("Yalnızca talep sahibi bu işlemi yapabilir."));

        return (s, r, null);
    }
}
