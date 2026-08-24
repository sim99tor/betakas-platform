using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Betakas.Api.Data;
using Betakas.Api.Dto;
using Betakas.Api.Models;
using Betakas.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Endpoints;

/// <summary>
/// Domain uçları. Her uç dar bir eylemi temsil eder ve kuralını sunucuda doğrular;
/// istemci artık ham state yazamaz. Başarılı her eylem güncel state'i geri döndürür,
/// böylece tarayıcı tek gidiş-dönüşte tazelenir.
/// </summary>
public static class DomainEndpoints
{
    public static async Task<User?> CurrentUserAsync(ClaimsPrincipal p, BetakasDbContext db)
    {
        var id = p.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? p.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null ? null : await db.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Ortak sarmalayıcı: kullanıcıyı çözer, eylemi çalıştırır, revizyonu artırır ve
    /// taze state'i döndürür. Kural ihlalleri eylemin belirlediği HTTP durumuyla döner.
    /// </summary>
    private static async Task<IResult> RunAsync(
        ClaimsPrincipal principal,
        BetakasDbContext db,
        StateService state,
        Func<User, Task<DomainResult>> action)
    {
        var me = await CurrentUserAsync(principal, db);
        if (me is null) return Results.Unauthorized();
        if (me.Status != "active") return Results.Json(new { error = "Hesabın aktif değil." }, statusCode: 403);

        DomainResult result;
        try
        {
            result = await action(me);
        }
        catch (DbUpdateException)
        {
            // Aynı anda gelen iki istek aynı id'yi üretmiş olabilir — istemci tekrar dener.
            return Results.Json(new { error = "Eşzamanlı bir işlem çakıştı, tekrar dene." }, statusCode: 409);
        }

        if (!result.Ok)
            return Results.Json(new { error = result.Error }, statusCode: result.Status);

        // Revizyon sayacı: diğer tarayıcıların yoklamayla tazelenmesini sağlar.
        var st = await db.PlatformState.FirstAsync(x => x.Id == 1);
        st.Rev += 1;
        await db.SaveChangesAsync();

        return Results.Ok(new { message = result.Message, state = await state.GetStateAsync(me.Id) });
    }

    public static void MapDomainEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // ---------- Talepler ----------

        api.MapPost("/requests", (CreateRequestDto dto, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, RequestActions a) =>
            RunAsync(p, db, st, me => a.CreateAsync(me, dto)));

        api.MapPost("/requests/{id}/boost", (string id, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, RequestActions a) =>
            RunAsync(p, db, st, me => a.BoostAsync(me, id)));

        api.MapPost("/requests/{id}/close", (string id, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, RequestActions a) =>
            RunAsync(p, db, st, me => a.CloseAsync(me, id)));

        // ---------- Ürün sürümleri ----------

        api.MapPost("/versions", (CreateVersionDto dto, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, VersionActions a) =>
            RunAsync(p, db, st, me => a.CreateAsync(me, dto)));

        api.MapPut("/versions/{id}/fixes", (string id, SaveFixesDto dto, ClaimsPrincipal p,
                BetakasDbContext db, StateService st, VersionActions a) =>
            RunAsync(p, db, st, me => a.SaveFixesAsync(me, id, dto.Fixes)));

        // ---------- Oturumlar: testçi ----------

        api.MapPost("/requests/{id}/apply", (string id, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, SessionActions a) =>
            RunAsync(p, db, st, me => a.ApplyAsync(me, id)));

        api.MapPost("/sessions/{id}/submit", (string id, SubmitFeedbackDto dto, ClaimsPrincipal p,
                BetakasDbContext db, StateService st, SessionActions a) =>
            RunAsync(p, db, st, me => a.SubmitAsync(me, id, dto)));

        api.MapPost("/sessions/{id}/rate-owner", (string id, RatingDto dto, ClaimsPrincipal p,
                BetakasDbContext db, StateService st, SessionActions a) =>
            RunAsync(p, db, st, me => a.RateOwnerAsync(me, id, dto.Rating)));

        // ---------- Oturumlar: kurucu ----------

        api.MapPost("/sessions/{id}/approve", (string id, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, SessionActions a) =>
            RunAsync(p, db, st, me => a.ApproveApplicationAsync(me, id)));

        api.MapPost("/sessions/{id}/reject", (string id, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, SessionActions a) =>
            RunAsync(p, db, st, me => a.RejectApplicationAsync(me, id)));

        api.MapPost("/sessions/{id}/accept", (string id, RatingDto dto, ClaimsPrincipal p,
                BetakasDbContext db, StateService st, SessionActions a) =>
            RunAsync(p, db, st, me => a.AcceptAsync(me, id, dto.Rating)));

        api.MapPost("/sessions/{id}/dispute", (string id, DisputeDto dto, ClaimsPrincipal p,
                BetakasDbContext db, StateService st, SessionActions a) =>
            RunAsync(p, db, st, me => a.DisputeAsync(me, id, dto.Note)));

        // ---------- Faturalama ----------

        api.MapPost("/billing/buy", (BuyPackageDto dto, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, BillingActions a) =>
            RunAsync(p, db, st, me => a.BuyPackageAsync(me, dto.PackageId ?? "", dto.Card)));

        api.MapPost("/billing/subscribe", (SubscribeDto dto, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, BillingActions a) =>
            RunAsync(p, db, st, me => a.SubscribeAsync(me, dto.PlanId ?? "", dto.Card)));

        api.MapPost("/billing/cancel", (ClaimsPrincipal p, BetakasDbContext db,
                StateService st, BillingActions a) =>
            RunAsync(p, db, st, a.CancelSubscriptionAsync));

        api.MapPost("/billing/renew", (ClaimsPrincipal p, BetakasDbContext db,
                StateService st, BillingActions a) =>
            RunAsync(p, db, st, a.RenewAsync));

        // ---------- Çekimler ----------

        api.MapPost("/withdrawals", (WithdrawalDto2 dto, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, BillingActions a) =>
            RunAsync(p, db, st, me => a.RequestWithdrawalAsync(me, dto.Amount, dto.Iban)));

        api.MapPost("/withdrawals/{id}/resolve", (string id, ResolveWithdrawalDto dto, ClaimsPrincipal p,
                BetakasDbContext db, StateService st, BillingActions a) =>
            RunAsync(p, db, st, me => a.ResolveWithdrawalAsync(me, id, dto.Outcome ?? "", dto.Note)));

        // ---------- Profil ----------

        api.MapPut("/me/expertise", (ExpertiseDto dto, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, ProfileActions a) =>
            RunAsync(p, db, st, me => a.SaveExpertiseAsync(me, dto.Categories, dto.Other)));

        // ---------- Yönetim ----------

        api.MapPost("/sessions/{id}/resolve-dispute", (string id, ResolveDisputeDto dto, ClaimsPrincipal p,
                BetakasDbContext db, StateService st, SessionActions a) =>
            RunAsync(p, db, st, me => a.ResolveDisputeAsync(me, id, dto.Outcome ?? "")));

        api.MapPost("/admin/users/{id}/approve", (string id, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, AdminActions a) =>
            RunAsync(p, db, st, me => a.ApproveUserAsync(me, id)));

        api.MapPost("/admin/users/{id}/reject", (string id, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, AdminActions a) =>
            RunAsync(p, db, st, me => a.RejectUserAsync(me, id)));

        api.MapPut("/admin/settings/fee", (DecimalValueDto dto, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, AdminActions a) =>
            RunAsync(p, db, st, me => a.SetFeePctAsync(me, dto.Value)));

        api.MapPut("/admin/settings/token-price", (DecimalValueDto dto, ClaimsPrincipal p, BetakasDbContext db,
                StateService st, AdminActions a) =>
            RunAsync(p, db, st, me => a.SetTokenPriceAsync(me, dto.Value)));
    }
}
