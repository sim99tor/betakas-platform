using Betakas.Api.Data;
using Betakas.Api.Models;

namespace Betakas.Api.Services;

/// <summary>Kullanıcının kendi profilinde değiştirebildiği alanlar.</summary>
public class ProfileActions(BetakasDbContext db)
{
    public const string OtherCategory = "Diğer";

    public async Task<DomainResult> SaveExpertiseAsync(User me, List<string>? categories, string? other)
    {
        var picked = (categories ?? [])
            .Where(c => RequestActions.ProductCategories.Contains(c))
            .Distinct()
            .ToList();

        if (picked.Count == 0) return DomainResult.Invalid("En az bir uzmanlık alanı seç.");

        var otherText = (other ?? "").Trim();
        if (picked.Contains(OtherCategory) && otherText.Length == 0)
            return DomainResult.Invalid("\"Diğer\" seçtin — hangi alanı kastettiğini yaz.");
        if (otherText.Length > 60)
            return DomainResult.Invalid("Serbest alan en fazla 60 karakter olabilir.");

        me.ExpertiseCategories = picked;
        me.ExpertiseOther = picked.Contains(OtherCategory) ? otherText : null;

        await db.SaveChangesAsync();
        return DomainResult.Success($"Uzmanlık alanların güncellendi · {picked.Count} kategori seçili.");
    }
}
