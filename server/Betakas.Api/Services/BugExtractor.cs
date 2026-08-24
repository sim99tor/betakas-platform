using System.Text.RegularExpressions;
using Betakas.Api.Data;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

public record BugItem(string Id, string No, string Text, string Reporter, bool Critical);

/// <summary>
/// Testçilerin serbest metinlerinden numaralı bug listesi çıkarır. İstemcideki
/// <c>splitBugItems</c> / <c>versionBugs</c> ile aynı kuralları uygular; sürüm notu
/// tiklerinin anahtarları buradan üretildiği için iki taraf birebir aynı sonucu vermelidir.
/// </summary>
public partial class BugExtractor(BetakasDbContext db)
{
    // "Bug görmedim" tarzı cümleler madde sayılmaz.
    [GeneratedRegex(@"^(belirgin\s+)?(bir\s+)?(bug|hata|sorun|problem)\S*\s*(yok|görmedim|görmüyorum|yaşamadım|çıkmadı|rastlamadım)",
        RegexOptions.IgnoreCase)]
    private static partial Regex NoBugRegex();

    // "1) ... 2) ..." kalıbı — yalnızca 1'den başlayıp sırayla artan numaralar bölme noktasıdır.
    [GeneratedRegex(@"(?:^|[\s;])(\d{1,2})\s*[)\.]\s+")]
    private static partial Regex NumberedRegex();

    [GeneratedRegex(@"(?:^|\n)\s*[-•*]\s+")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"kritik|çöküyor|donuyor|donduruyor|veri kayb|çöktü", RegexOptions.IgnoreCase)]
    private static partial Regex CriticalRegex();

    public static List<string> SplitBugItems(string? text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return [];

        var marks = new List<(int Cut, int Start)>();
        var want = 1;
        foreach (Match m in NumberedRegex().Matches(t))
        {
            if (int.Parse(m.Groups[1].Value) != want) continue;
            marks.Add((m.Index, m.Index + m.Length));
            want++;
        }

        List<string> parts;
        if (marks.Count > 0)
        {
            parts = [];
            for (var i = 0; i < marks.Count; i++)
            {
                var end = i + 1 < marks.Count ? marks[i + 1].Cut : t.Length;
                parts.Add(t[marks[i].Start..end]);
            }
        }
        else if (BulletRegex().IsMatch(t))
        {
            parts = [.. BulletRegex().Split(t)];
        }
        else
        {
            parts = [.. t.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
        }

        return parts
            .Select(p => Regex.Replace(p.Trim(), @"^[,;]\s*", ""))
            .Where(p => p.Length >= 8 && !NoBugRegex().IsMatch(p))
            .ToList();
    }

    /// <summary>Bir sürüme gelen onaylı testlerden numaralanmış bug listesi.</summary>
    public async Task<List<BugItem>> VersionBugsAsync(string versionId)
    {
        var requestIds = await db.Requests.Where(r => r.VersionId == versionId).Select(r => r.Id).ToListAsync();
        if (requestIds.Count == 0) return [];

        var sessions = await db.Sessions
            .Where(s => s.Status == "accepted" && requestIds.Contains(s.RequestId))
            .OrderBy(s => s.SubmittedAt ?? 0)
            .ToListAsync();

        var testers = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
        var output = new List<BugItem>();

        foreach (var s in sessions)
        {
            // Bug listesi yalnızca MVP şablonunun "bugs" alanından çıkarılır;
            // diğer aşamalar numaralandırmayı kirletmez.
            var text = s.Feedback?["bugs"]?.GetValue<string>();
            var items = SplitBugItems(text);

            for (var i = 0; i < items.Count; i++)
            {
                output.Add(new BugItem(
                    Id: $"{s.Id}:{i}",
                    No: (output.Count + 1).ToString("D2"),
                    Text: items[i],
                    Reporter: testers.GetValueOrDefault(s.TesterId, "Testçi"),
                    Critical: CriticalRegex().IsMatch(items[i])));
            }
        }

        return output;
    }

    /// <summary>
    /// Bir sürümün yanıt verdiği bug kaynağı: kendisinden önceki, bug BİLDİRİLMİŞ en yeni sürüm.
    /// Araya teste hiç sokulmamış bir sürüm girse bile açık maddeler kaybolmasın diye
    /// doğrudan bir öncekine değil, geriye doğru bakılır.
    /// </summary>
    public async Task<ProductVersion?> BugSourceForAsync(ProductVersion version)
    {
        var siblings = await db.Versions
            .Where(v => v.OwnerId == version.OwnerId)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync();

        var idx = siblings.FindIndex(v => v.Id == version.Id);
        for (var i = idx - 1; i >= 0; i--)
            if ((await VersionBugsAsync(siblings[i].Id)).Count > 0)
                return siblings[i];

        return null;
    }
}
