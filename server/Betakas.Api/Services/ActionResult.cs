namespace Betakas.Api.Services;

/// <summary>
/// Domain eylemlerinin ortak sonucu. Kural ihlalleri istisna değil, veri olarak döner;
/// uç noktalar bunu HTTP durumuna çevirir.
/// </summary>
public record DomainResult(bool Ok, string? Error = null, int Status = 200, string? Message = null)
{
    public static DomainResult Success(string? message = null) => new(true, Message: message);

    /// <summary>Kural ihlali — istemci girdisi geçersiz (400).</summary>
    public static DomainResult Invalid(string error) => new(false, error, 400);

    /// <summary>Yetki yok (403).</summary>
    public static DomainResult Denied(string error = "Bu işlem için yetkin yok.") => new(false, error, 403);

    /// <summary>Kayıt bulunamadı (404).</summary>
    public static DomainResult Missing(string error = "Kayıt bulunamadı.") => new(false, error, 404);

    /// <summary>Mevcut durumla çelişiyor — ör. slot dolmuş, talep kapanmış (409).</summary>
    public static DomainResult Conflict(string error) => new(false, error, 409);
}
