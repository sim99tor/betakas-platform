using Betakas.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

public record Reputation(double Avg, int Count, int Completed, int Penalties);

/// <summary>
/// İtibar, tamamlanmış oturumlardan türetilir — hiçbir yerde saklanmaz.
/// Anlaşmazlıkta kaybeden taraf ceza alır ve ortalaması buna göre düşer.
/// </summary>
public class ReputationService(BetakasDbContext db)
{
    public async Task<Reputation> OfAsync(string userId)
    {
        var sessions = await db.Sessions
            .Where(s => s.TesterId == userId)
            .Select(s => new { s.Status, s.Rating, s.DisputeOutcome })
            .ToListAsync();

        double sum = 0;
        int n = 0, completed = 0, penalties = 0;

        foreach (var s in sessions)
        {
            if (s.Status == "accepted" && s.Rating is > 0)
            {
                sum += s.Rating!.Value;
                n++;
                completed++;
            }
            else if (s.Status == "rejected" && s.DisputeOutcome == "refund")
            {
                // Anlaşmazlığı kaybeden testçi: ceza puanı ortalamaya yazılır,
                // dolayısıyla token çarpanı da düşer.
                sum += EconomyService.DisputePenaltyRating;
                n++;
                penalties++;
            }
        }

        return new Reputation(n == 0 ? 0 : sum / n, n, completed, penalties);
    }

    /// <summary>
    /// İtibar çarpanı: kaliteli feedback verene aynı test daha çok token kazandırır.
    /// Bonus escrow'dan değil sistemden basılır ki escrow matematiği bozulmasın.
    /// </summary>
    public async Task<decimal> MultiplierAsync(string userId)
    {
        var r = await OfAsync(userId);
        if (r.Count >= 2 && r.Avg >= 4.5) return 1.2m;
        if (r.Count >= 1 && r.Avg >= 4.0) return 1.1m;
        return 1.0m;
    }
}
