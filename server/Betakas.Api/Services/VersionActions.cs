using System.Text.Json.Nodes;
using Betakas.Api.Data;
using Betakas.Api.Dto;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

/// <summary>Ürün sürümü eylemleri: yeni sürüm çıkarma ve sürüm notu bug tikleri.</summary>
public class VersionActions(BetakasDbContext db, LedgerService ledger)
{
    /// <summary>Bug tikinin alabileceği değerler; başka bir değer kabul edilmez.</summary>
    public static readonly string[] FixStates = ["fixed", "wip", "later", "norepro"];

    public async Task<DomainResult> CreateAsync(User me, CreateVersionDto dto)
    {
        if (me.Role != "founder") return DomainResult.Denied("Yalnızca kurucular sürüm çıkarabilir.");

        var label = (dto.Label ?? "").Trim();
        var url = (dto.Url ?? "").Trim();
        var notes = (dto.Notes ?? "").Trim();

        if (label.Length == 0 || url.Length == 0 || notes.Length == 0)
            return DomainResult.Invalid("Sürüm etiketi, link ve değişiklik notu zorunlu.");
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return DomainResult.Invalid("Sürüm linki http(s) ile başlamalı.");

        var mine = await db.Versions.Where(v => v.OwnerId == me.Id).Select(v => v.Label).ToListAsync();
        if (mine.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase)))
            return DomainResult.Conflict($"Bu etikette bir sürümün zaten var: {label}");

        var fixes = await SanitizeFixesAsync(me.Id, dto.Fixes);

        db.Versions.Add(new ProductVersion
        {
            Id = await ledger.NextIdAsync("v"),
            OwnerId = me.Id,
            Label = label,
            Url = url,
            Notes = notes,
            Fixes = fixes,
            CreatedAt = LedgerService.Now()
        });

        await db.SaveChangesAsync();
        return DomainResult.Success($"{label} sürümü oluşturuldu.");
    }

    public async Task<DomainResult> SaveFixesAsync(User me, string versionId, JsonObject? fixes)
    {
        var v = await db.Versions.FirstOrDefaultAsync(x => x.Id == versionId);
        if (v is null) return DomainResult.Missing("Sürüm bulunamadı.");
        if (v.OwnerId != me.Id) return DomainResult.Denied("Yalnızca kendi sürümünün notunu düzenleyebilirsin.");

        v.Fixes = await SanitizeFixesAsync(me.Id, fixes);
        await db.SaveChangesAsync();
        return DomainResult.Success("Sürüm notu güncellendi.");
    }

    /// <summary>
    /// Tik haritasını temizler: yalnızca bilinen durum değerleri ve yalnızca bu kurucunun
    /// kendi sürümlerine bildirilmiş bug id'leri kabul edilir. Böylece istemci uydurma
    /// anahtarlarla sürüm notunu şişiremez.
    /// </summary>
    private async Task<JsonObject> SanitizeFixesAsync(string ownerId, JsonObject? incoming)
    {
        var clean = new JsonObject();
        if (incoming is null) return clean;

        // Geçerli bug id'leri "<oturumId>:<sıra>" biçimindedir; oturum bu kurucunun
        // taleplerinden birine ait olmalıdır.
        var myRequestIds = await db.Requests.Where(r => r.OwnerId == ownerId).Select(r => r.Id).ToListAsync();
        var mySessionIds = await db.Sessions
            .Where(s => myRequestIds.Contains(s.RequestId))
            .Select(s => s.Id)
            .ToListAsync();
        var allowed = mySessionIds.ToHashSet();

        foreach (var kv in incoming)
        {
            var state = kv.Value?.GetValue<string>();
            if (state is null || !FixStates.Contains(state)) continue;

            var sessionId = kv.Key.Split(':')[0];
            if (!allowed.Contains(sessionId)) continue;

            clean[kv.Key] = state;
        }

        return clean;
    }
}
