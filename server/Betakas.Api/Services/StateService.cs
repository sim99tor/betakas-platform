using Betakas.Api.Data;
using Betakas.Api.Dto;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

/// <summary>
/// State'i okumaya adanmış servis. Yazma yolu bilinçli olarak yoktur: her değişiklik
/// kendi domain ucundan geçer (bkz. <c>DomainEndpoints</c>), böylece kurallar sunucuda uygulanır.
///
/// Uzak bir veritabanında gecikme baskındır, bu yüzden iki iyileştirme uygulanır:
///   1. Önbellek — rev değişmediyse veri de değişmemiştir; tek bir ucuz rev sorgusu yeter.
///   2. Paralel toplama — önbellek ıskalandığında dokuz tablo ayrı bağlantılardan aynı anda
///      okunur, böylece toplam süre dokuz sorgunun toplamı değil, en yavaşı kadar olur.
/// </summary>
public class StateService(BetakasDbContext db, IBetakasContextFactory factory, StateCache cache)
{
    public async Task<StateDto> GetStateAsync(string? authUserId)
    {
        var rev = await GetRevAsync();
        var snapshot = cache.Get(rev) ?? await BuildAndCacheAsync(rev);
        return Personalize(snapshot, authUserId);
    }

    public async Task<long> GetRevAsync() =>
        await db.PlatformState.AsNoTracking().Where(x => x.Id == 1).Select(x => x.Rev).FirstAsync();

    private async Task<StateSnapshot> BuildAndCacheAsync(long rev)
    {
        var snapshot = await BuildSnapshotAsync();
        cache.Set(rev, snapshot);
        return snapshot;
    }

    /// <summary>Her sorgu kendi context'inde çalışır; tek DbContext eşzamanlı kullanılamaz.</summary>
    private async Task<T> QueryAsync<T>(Func<BetakasDbContext, Task<T>> query)
    {
        await using var context = factory.Create();
        return await query(context);
    }

    private async Task<StateSnapshot> BuildSnapshotAsync()
    {
        var stateTask = QueryAsync(c => c.PlatformState.AsNoTracking().FirstAsync(x => x.Id == 1));

        // IBAN ayrı toplanır: önbellek ortak olduğu için kişiye özel alan içeremez.
        var ibansTask = QueryAsync(c => c.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Iban })
            .ToDictionaryAsync(x => x.Id, x => x.Iban));

        var usersTask = QueryAsync(c => c.Users.AsNoTracking().OrderBy(u => u.Id)
            .Select(u => new UserDto
            {
                Id = u.Id, Name = u.Name, Initials = u.Initials, Email = u.Email,
                Startup = u.Startup, Title = u.Title, Tagline = u.Tagline, Sector = u.Sector,
                Skills = u.Skills, ExpertiseCategories = u.ExpertiseCategories,
                ExpertiseOther = u.ExpertiseOther, Role = u.Role, Status = u.Status,
                Subscription = u.SubscriptionPlanId == null
                    ? null
                    : new SubscriptionDto
                    {
                        PlanId = u.SubscriptionPlanId,
                        RenewsAt = u.SubscriptionRenewsAt,
                        Active = u.SubscriptionActive
                    }
            }).ToListAsync());

        var versionsTask = QueryAsync(c => c.Versions.AsNoTracking().OrderBy(v => v.CreatedAt)
            .Select(v => new VersionDto
            {
                Id = v.Id, OwnerId = v.OwnerId, Label = v.Label, Url = v.Url,
                CreatedAt = v.CreatedAt, Notes = v.Notes, Fixes = v.Fixes
            }).ToListAsync());

        var requestsTask = QueryAsync(c => c.Requests.AsNoTracking().OrderBy(r => r.CreatedAt)
            .Select(r => new RequestDto
            {
                Id = r.Id, OwnerId = r.OwnerId, VersionId = r.VersionId, Title = r.Title, Url = r.Url,
                ProductCategory = r.ProductCategory, Stage = r.Stage, FeedbackType = r.FeedbackType,
                Scenario = r.Scenario, Credits = r.Credits, Slots = r.Slots,
                Visibility = r.Visibility, Status = r.Status, CreatedAt = r.CreatedAt,
                Boosted = r.Boosted, BoostedAt = r.BoostedAt
            }).ToListAsync());

        var sessionsTask = QueryAsync(c => c.Sessions.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new SessionDto
            {
                Id = s.Id, RequestId = s.RequestId, TesterId = s.TesterId, Status = s.Status,
                AppliedAt = s.AppliedAt, SubmittedAt = s.SubmittedAt, Rating = s.Rating,
                OwnerRating = s.OwnerRating, DurationMin = s.DurationMin, ProofUrl = s.ProofUrl,
                Feedback = s.Feedback, DisputeNote = s.DisputeNote,
                DisputeOutcome = s.DisputeOutcome, CashPaid = s.CashPaid
            }).ToListAsync());

        var ledgerTask = QueryAsync(c => c.Ledger.AsNoTracking().OrderBy(l => l.Ts)
            .Select(l => new LedgerDto
            {
                Id = l.Id, Ts = l.Ts, From = l.From, To = l.To,
                Amount = l.Amount, Type = l.Type, Ref = l.Ref, Note = l.Note
            }).ToListAsync());

        var cashTask = QueryAsync(c => c.CashLedger.AsNoTracking().OrderBy(x => x.Ts)
            .Select(x => new CashLedgerDto
            {
                Id = x.Id, Ts = x.Ts, From = x.From, To = x.To,
                Amount = x.Amount, Type = x.Type, Ref = x.Ref, Note = x.Note
            }).ToListAsync());

        var purchasesTask = QueryAsync(c => c.Purchases.AsNoTracking().OrderBy(p => p.Ts)
            .Select(p => new PurchaseDto
            {
                Id = p.Id, Ts = p.Ts, UserId = p.UserId, Kind = p.Kind,
                PackageId = p.PackageId, PackageName = p.PackageName, Tokens = p.Tokens,
                Testers = p.Testers, Gross = p.Gross, Fee = p.Fee, Pool = p.Pool, InvoiceNo = p.InvoiceNo
            }).ToListAsync());

        var withdrawalsTask = QueryAsync(c => c.Withdrawals.AsNoTracking().OrderBy(w => w.RequestedAt)
            .Select(w => new WithdrawalDto
            {
                Id = w.Id, UserId = w.UserId, Amount = w.Amount, Status = w.Status,
                RequestedAt = w.RequestedAt, ResolvedAt = w.ResolvedAt, Iban = w.Iban
            }).ToListAsync());

        await Task.WhenAll(
            stateTask, ibansTask, usersTask, versionsTask, requestsTask,
            sessionsTask, ledgerTask, cashTask, purchasesTask, withdrawalsTask);

        var st = await stateTask;

        var shared = new StateDto
        {
            Rev = st.Rev,
            Version = st.SeedVersion,
            AuthUserId = null,
            Settings = new SettingsDto { TokenPrice = st.TokenPrice, FeePct = st.FeePct },
            SprintNo = st.SprintNo,
            Seq = st.Seq,
            Users = await usersTask,
            Versions = await versionsTask,
            Requests = await requestsTask,
            Sessions = await sessionsTask,
            Ledger = await ledgerTask,
            CashLedger = await cashTask,
            Purchases = await purchasesTask,
            Withdrawals = await withdrawalsTask,
        };

        return new StateSnapshot(shared, await ibansTask);
    }

    /// <summary>
    /// Ortak anlık görüntüye kişiye özel alanları ekler. Yalnızca oturumdaki kullanıcının
    /// kaydı kopyalanır ki önbellekteki nesneler istekler arasında değişmesin ve
    /// IBAN başkasına sızmasın.
    /// </summary>
    private static StateDto Personalize(StateSnapshot snapshot, string? authUserId)
    {
        var shared = snapshot.Shared;

        var users = shared.Users.Select(u => u.Id == authUserId
            ? new UserDto
            {
                Id = u.Id, Name = u.Name, Initials = u.Initials, Email = u.Email,
                Startup = u.Startup, Title = u.Title, Tagline = u.Tagline, Sector = u.Sector,
                Skills = u.Skills, ExpertiseCategories = u.ExpertiseCategories,
                ExpertiseOther = u.ExpertiseOther, Role = u.Role, Status = u.Status,
                Subscription = u.Subscription,
                Iban = snapshot.Ibans.GetValueOrDefault(u.Id),
            }
            : u).ToList();

        return new StateDto
        {
            Rev = shared.Rev,
            Version = shared.Version,
            AuthUserId = authUserId,
            Settings = shared.Settings,
            SprintNo = shared.SprintNo,
            Seq = shared.Seq,
            Users = users,
            Versions = shared.Versions,
            Requests = shared.Requests,
            Sessions = shared.Sessions,
            Ledger = shared.Ledger,
            CashLedger = shared.CashLedger,
            Purchases = shared.Purchases,
            Withdrawals = shared.Withdrawals,
        };
    }
}

/// <summary>
/// Giriş gerektirmeyen, redakte edilmiş state. Tam state ile AYNI önbellekten türetilir —
/// giriş ekranı herkesin gördüğü ilk sayfa olduğu için ayrıca sorgu atmaz.
/// </summary>
public class PublicStateService(StateService state)
{
    public async Task<object> GetAsync()
    {
        // Tam state'i almak önbelleği doldurur; buradaki redaksiyon ondan türetilir.
        var full = await state.GetStateAsync(null);

        var users = full.Users.Select(u => new UserDto
        {
            Id = u.Id, Name = u.Name, Initials = u.Initials,
            Email = "", // public profilde e-posta gösterilmez
            Startup = u.Startup, Title = u.Title, Tagline = u.Tagline, Sector = u.Sector,
            Skills = u.Skills, ExpertiseCategories = u.ExpertiseCategories,
            ExpertiseOther = u.ExpertiseOther, Role = u.Role, Status = u.Status,
        }).ToList();

        // Puan ve durum kalır; testçinin yazdığı feedback metni kalmaz.
        var sessions = full.Sessions.Select(s => new SessionDto
        {
            Id = s.Id, RequestId = s.RequestId, TesterId = s.TesterId, Status = s.Status,
            AppliedAt = s.AppliedAt, SubmittedAt = s.SubmittedAt,
            Rating = s.Rating, OwnerRating = s.OwnerRating, DurationMin = s.DurationMin,
            DisputeOutcome = s.DisputeOutcome,
        }).ToList();

        var requests = full.Requests.Select(r => new RequestDto
        {
            Id = r.Id, OwnerId = r.OwnerId, VersionId = r.VersionId, Title = r.Title,
            ProductCategory = r.ProductCategory, Stage = r.Stage, FeedbackType = r.FeedbackType,
            Credits = r.Credits, Slots = r.Slots, Visibility = r.Visibility,
            Status = r.Status, CreatedAt = r.CreatedAt,
        }).ToList();

        var versions = full.Versions.Select(v => new VersionDto
        {
            Id = v.Id, OwnerId = v.OwnerId, Label = v.Label, CreatedAt = v.CreatedAt,
        }).ToList();

        // --- Giriş ekranındaki demo hesap kartları ---
        // Bu bir demo kolaylığıdır: hesap seçilince form dolar. Parola gönderilmez.
        // E-postalar zaten önbellekteki tam state'te var; ayrıca sorgu atmaya gerek yok.
        var emails = full.Users.ToDictionary(u => u.Id, u => u.Email);

        var demoAccounts = users
            .Where(u => u.Status == "active")
            .Select(u =>
            {
                var mine = sessions.Where(s => s.TesterId == u.Id && s.Status == "accepted" && s.Rating != null).ToList();
                var completed = mine.Count;
                var avg = completed == 0 ? 0d : mine.Average(s => s.Rating!.Value);
                var balance = full.Ledger.Where(l => l.To == u.Id).Sum(l => l.Amount)
                              - full.Ledger.Where(l => l.From == u.Id).Sum(l => l.Amount);
                return new
                {
                    id = u.Id, name = u.Name, initials = u.Initials,
                    email = emails.GetValueOrDefault(u.Id, ""),
                    role = u.Role,
                    subtitle = u.Role == "tester" ? u.Title : u.Startup,
                    completed, avg, balance
                };
            }).ToList();

        return new
        {
            rev = full.Rev,
            version = full.Version,
            authUserId = (string?)null,
            settings = full.Settings,
            sprintNo = full.SprintNo,
            seq = full.Seq,
            users,
            versions,
            requests,
            sessions,
            ledger = Array.Empty<LedgerDto>(),
            cashLedger = Array.Empty<CashLedgerDto>(),
            purchases = Array.Empty<PurchaseDto>(),
            withdrawals = Array.Empty<WithdrawalDto>(),
            demoAccounts
        };
    }

}
