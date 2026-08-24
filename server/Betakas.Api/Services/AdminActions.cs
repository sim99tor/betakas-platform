using Betakas.Api.Data;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

/// <summary>Yönetim eylemleri: üyelik onayı ve platform ekonomisi ayarları.</summary>
public class AdminActions(BetakasDbContext db, LedgerService ledger, EconomyService economy)
{
    /// <summary>Kapalı ekosistem: onaylanan üyeye 100 başlangıç tokeni tanımlanır.</summary>
    public async Task<DomainResult> ApproveUserAsync(User me, string userId)
    {
        if (me.Role != "admin") return DomainResult.Denied();

        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (u is null) return DomainResult.Missing("Kullanıcı bulunamadı.");
        if (u.Status == "active") return DomainResult.Conflict("Bu hesap zaten aktif.");

        u.Status = "active";
        await ledger.PostAsync("system", u.Id, 100, "bonus", null, $"Yeni üye başlangıç tokeni: {u.Name}");

        await db.SaveChangesAsync();
        return DomainResult.Success($"{u.Name} onaylandı, 100 başlangıç tokeni tanımlandı.");
    }

    public async Task<DomainResult> RejectUserAsync(User me, string userId)
    {
        if (me.Role != "admin") return DomainResult.Denied();

        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (u is null) return DomainResult.Missing("Kullanıcı bulunamadı.");
        if (u.Status != "pending") return DomainResult.Conflict("Yalnızca onay bekleyen başvuru reddedilebilir.");

        // Başvuru henüz onaylanmadığı için defterde hareketi yoktur; kayıt silinebilir.
        db.Users.Remove(u);
        await db.SaveChangesAsync();
        return DomainResult.Success("Başvuru reddedildi.");
    }

    public async Task<DomainResult> SetFeePctAsync(User me, decimal value)
    {
        if (me.Role != "admin") return DomainResult.Denied("Komisyon oranını yalnızca yönetim değiştirebilir.");
        if (value is < 0 or > 60) return DomainResult.Invalid("Komisyon oranı 0-60 arasında olmalı.");

        var st = await economy.StateAsync();
        st.FeePct = (int)Math.Round(value, MidpointRounding.AwayFromZero);

        await db.SaveChangesAsync();
        return DomainResult.Success($"Komisyon oranı %{st.FeePct} olarak güncellendi.");
    }

    public async Task<DomainResult> SetTokenPriceAsync(User me, decimal value)
    {
        if (me.Role != "admin") return DomainResult.Denied("Token fiyatını yalnızca yönetim değiştirebilir.");
        if (value is < 1 or > 500) return DomainResult.Invalid("Token fiyatı 1-500 ₺ arasında olmalı.");

        var st = await economy.StateAsync();
        st.TokenPrice = Math.Round(value, 2, MidpointRounding.AwayFromZero);

        await db.SaveChangesAsync();
        return DomainResult.Success($"Token fiyatı {st.TokenPrice:N2} ₺ olarak güncellendi.");
    }
}
