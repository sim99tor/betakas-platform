using Betakas.Api.Data;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

/// <summary>
/// Çift kayıtlı defterler. Bakiye asla bir sütunda tutulmaz — her zaman defterden
/// türetilir. Token defterinde sanal hesaplar: "system", "escrow".
/// Para defterinde: "revenue" (platform geliri), "pool" (ödül havuzu), "bank" (dış ödeme).
/// </summary>
public class LedgerService(BetakasDbContext db)
{
    /// <summary>
    /// Id sayacı veritabanındaki tek satırda tutulur; böylece eşzamanlı isteklerde
    /// iki kullanıcı aynı id'yi üretemez.
    /// </summary>
    public async Task<string> NextIdAsync(string prefix)
    {
        var st = await db.PlatformState.FirstAsync(x => x.Id == 1);
        st.Seq += 1;
        return prefix + st.Seq;
    }

    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // ---- Token defteri ----

    public async Task PostAsync(string from, string to, int amount, string type, string? refId, string note)
    {
        db.Ledger.Add(new LedgerEntry
        {
            Id = await NextIdAsync("l"), Ts = Now(),
            From = from, To = to, Amount = amount, Type = type, Ref = refId, Note = note
        });
    }

    public async Task<int> BalanceAsync(string acct)
    {
        var credit = await db.Ledger.Where(e => e.To == acct).SumAsync(e => (int?)e.Amount) ?? 0;
        var debit = await db.Ledger.Where(e => e.From == acct).SumAsync(e => (int?)e.Amount) ?? 0;
        return credit - debit;
    }

    /// <summary>Bir talep için escrow'da hâlâ kilitli olan token (kilitlenen − serbest kalan − iade).</summary>
    public async Task<int> EscrowRemainingAsync(string requestId)
    {
        var rows = await db.Ledger.Where(e => e.Ref == requestId).ToListAsync();
        var locked = rows.Where(e => e.To == "escrow").Sum(e => e.Amount);
        var released = rows.Where(e => e.From == "escrow").Sum(e => e.Amount);
        return locked - released;
    }

    // ---- Para defteri (₺) ----

    public async Task PostCashAsync(string from, string to, decimal amount, string type, string? refId, string note)
    {
        db.CashLedger.Add(new CashLedgerEntry
        {
            Id = await NextIdAsync("c"), Ts = Now(),
            From = from, To = to, Amount = amount, Type = type, Ref = refId, Note = note
        });
    }

    public async Task<decimal> CashBalanceAsync(string acct) =>
        (await db.CashLedger.Where(e => e.To == acct).SumAsync(e => (decimal?)e.Amount) ?? 0m)
        - (await db.CashLedger.Where(e => e.From == acct).SumAsync(e => (decimal?)e.Amount) ?? 0m);

    /// <summary>Testçinin çekilebilir bakiyesi: kazandığı − çektiği − onay bekleyen talepler.</summary>
    public async Task<decimal> WithdrawableAsync(string userId)
    {
        var pending = await db.Withdrawals
            .Where(w => w.UserId == userId && w.Status == "pending")
            .SumAsync(w => (decimal?)w.Amount) ?? 0m;
        return await CashBalanceAsync(userId) - pending;
    }

    // ---- Slot sayımı ----

    /// <summary>Bir talepte dolu sayılan slotlar: reddedilmemiş her oturum bir slot tutar.</summary>
    public async Task<int> SlotsTakenAsync(string requestId) =>
        await db.Sessions.CountAsync(s => s.RequestId == requestId && s.Status != "rejected");

    public async Task<int> SlotsLeftAsync(TestRequest r) => r.Slots - await SlotsTakenAsync(r.Id);
}
