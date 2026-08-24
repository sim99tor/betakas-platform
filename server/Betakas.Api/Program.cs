using System.Security.Claims;
using System.Text.Json;
using Betakas.Api.Data;
using Betakas.Api.Dto;
using Betakas.Api.Endpoints;
using Betakas.Api.Models;
using Betakas.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Yapılandırma günlüğü henüz DI'dan alınamaz; açılış uyarıları için basit bir logger.
using var bootLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
var bootLogger = bootLoggerFactory.CreateLogger("Betakas.Boot");

// ---------------- Veritabanı ----------------
// Diyagramdaki "Postgresql, MSSQL" seçeneği: sağlayıcı yapılandırmadan gelir.
// Şema ve ilişkiler aynıdır; yalnızca sütun tipleri değişir (bkz. BetakasDbContext).
//
// Supabase ayrı bir sağlayıcı DEĞİLDİR — barındırılan PostgreSQL'dir. "Postgres" sağlayıcısı
// ve Supabase'in bağlantı dizesi yeterlidir; TLS ve havuz ayarları otomatik uygulanır.
var dbProvider = builder.Configuration["Database:Provider"] ?? "Postgres";
var useSqlServer = dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
var connectionString = DevSecrets.ResolveConnectionString(
    builder.Configuration, builder.Environment, useSqlServer, bootLogger);

if (!useSqlServer)
{
    connectionString = PostgresConnection.Normalize(connectionString, bootLogger);
    bootLogger.LogInformation("Veritabanı: {Target}", PostgresConnection.Describe(connectionString));
}

// Migration setleri context tipine bağlıdır; her sağlayıcı kendi türetilmiş context'ini
// kullanır, uygulama kodu ise her zaman soyut BetakasDbContext'i görür.
if (useSqlServer)
    builder.Services.AddDbContext<BetakasDbContext, SqlServerDbContext>(o => o.UseSqlServer(connectionString));
else
    builder.Services.AddDbContext<BetakasDbContext, PostgresDbContext>(o =>
        o.UseNpgsql(connectionString, x => x.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));

// State onbellegi surec omru boyunca yasar; sunucu tek yazar oldugu icin guvenli.
builder.Services.AddSingleton<StateCache>();

// ---------------- Servisler ----------------
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<StateService>();
builder.Services.AddScoped<PublicStateService>();
builder.Services.AddScoped<EconomyService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<ReputationService>();
builder.Services.AddScoped<RequestActions>();
builder.Services.AddScoped<VersionActions>();
builder.Services.AddScoped<SessionActions>();
builder.Services.AddScoped<BillingActions>();
builder.Services.AddScoped<AdminActions>();
builder.Services.AddScoped<ProfileActions>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<LoginThrottle>();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// ---------------- Kimlik doğrulama ----------------
var jwtKey = DevSecrets.ResolveJwtKey(builder.Configuration, builder.Environment, bootLogger);
builder.Configuration["Jwt:Key"] = jwtKey; // TokenService aynı kaynaktan okusun
var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
    System.Text.Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ---------------- HTTPS ----------------
// Üretimde HTTPS zorunludur. Geliştirmede http de açık kalır (demo kolaylığı);
// https dinleyicisi ASPNETCORE_URLS ile eklenir, sertifika: dotnet dev-certs https --trust
// Ters vekil (Vercel, Render, nginx) arkasinda calisirken TLS disarida sonlanir ve
// uygulamaya duz HTTP gelir. X-Forwarded-* baslikları okunmazsa UseHttpsRedirection
// istegi zaten https olan adrese tekrar yonlendirir ve sonsuz donguye girer.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    // Vekilin adresi platforma gore degisir; bilinen ag listesi tutulamadigi icin bosaltilir.
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(o =>
    {
        o.MaxAge = TimeSpan.FromDays(180);
        o.IncludeSubDomains = true;
    });
    builder.Services.AddHttpsRedirection(o => o.RedirectStatusCode = StatusCodes.Status308PermanentRedirect);
}

var app = builder.Build();

// ---------------- Veritabanını hazırla ----------------
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<BetakasDbContext>();

    // Şema migration'larla yönetilir; EnsureCreated artık kullanılmıyor.
    await db.Database.MigrateAsync();

    await sp.GetRequiredService<SeedService>().EnsureSeededAsync();
    await sp.GetRequiredService<TokenService>().PurgeExpiredAsync();
    await sp.GetRequiredService<LoginThrottle>().PurgeOldAsync();
}

// Diger her seyden once: sonraki middleware'ler dogru semayi (https) gormeli.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// ---------------- Arayüz ----------------
// Arayüz API ile aynı origin'den servis edilir — CORS gerekmez.
// Tercih sırası: derlenmiş React (client/dist) → eski tek dosyalık sürüm (web/).
// Böylece React derlenmemişse uygulama yine de açılır.
// Yayin duzeninde (container) arayuz farkli bir yerde durur; Ui:Root ile verilebilir.
// Verilmezse gelistirme duzeni varsayilir: <repo>/client/dist ve <repo>/web
var configuredUi = builder.Configuration["Ui:Root"];
var root = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
var reactDist = string.IsNullOrWhiteSpace(configuredUi)
    ? Path.Combine(root, "client", "dist")
    : Path.GetFullPath(configuredUi);
var legacyWeb = Path.Combine(root, "web");

var uiRoot = Directory.Exists(Path.Combine(reactDist, "index.html")) || File.Exists(Path.Combine(reactDist, "index.html"))
    ? reactDist
    : legacyWeb;
var isSpa = uiRoot == reactDist;

if (Directory.Exists(uiRoot))
{
    var provider = new PhysicalFileProvider(uiRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider, RequestPath = "" });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider, RequestPath = "" });
    bootLogger.LogInformation("Arayüz: {Root}{Spa}", uiRoot, isSpa ? " (React SPA)" : " (klasik)");
}
else
{
    bootLogger.LogWarning("Arayüz bulunamadı. React için: cd client && npm run build");
}

// ---------------- Auth uçları ----------------

static UserDto ToDto(User u) => new()
{
    Id = u.Id, Name = u.Name, Initials = u.Initials, Email = u.Email,
    Startup = u.Startup, Title = u.Title, Tagline = u.Tagline, Sector = u.Sector,
    Skills = u.Skills, ExpertiseCategories = u.ExpertiseCategories,
    ExpertiseOther = u.ExpertiseOther, Role = u.Role, Status = u.Status,
    Subscription = u.SubscriptionPlanId == null ? null : new SubscriptionDto
    {
        PlanId = u.SubscriptionPlanId, RenewsAt = u.SubscriptionRenewsAt, Active = u.SubscriptionActive
    }
};

static string ClientIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor";

app.MapPost("/api/auth/login", async (
    LoginRequest req, HttpContext ctx, BetakasDbContext db, TokenService tokens, LoginThrottle throttle) =>
{
    var email = (req.Email ?? "").Trim().ToLowerInvariant();
    if (email.Length == 0 || string.IsNullOrEmpty(req.Password))
        return Results.BadRequest(new { error = "E-posta ve şifre zorunlu." });

    var ip = ClientIp(ctx);

    // Kaba kuvvet kapısı: parola doğrulanmadan ÖNCE bakılır.
    var gate = await throttle.CheckAsync(email, ip);
    if (!gate.Allowed)
    {
        ctx.Response.Headers.RetryAfter = gate.RetryAfterSeconds.ToString();
        return Results.Json(
            new { error = $"Çok fazla başarısız deneme. {gate.RetryAfterSeconds / 60} dakika sonra tekrar dene." },
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    var u = await db.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email);

    // Hesabın var olup olmadığını sızdırmamak için ikisinde de aynı mesaj ve aynı sayaç.
    if (u is null || !BCrypt.Net.BCrypt.Verify(req.Password, u.PasswordHash))
    {
        await throttle.RecordFailureAsync(email, ip);
        return Results.BadRequest(new { error = "E-posta veya şifre hatalı." });
    }

    if (u.Status == "pending")
        return Results.BadRequest(new { error = "Başvurun henüz onaylanmadı. Kapalı ekosistem: yönetim onayından sonra giriş açılır." });

    if (!string.IsNullOrEmpty(req.Role) && req.Role != u.Role)
        return Results.BadRequest(new { error = "wrong-tab", role = u.Role });

    await throttle.ClearAsync(email);
    var pair = await tokens.IssuePairAsync(u);

    return Results.Ok(new
    {
        token = pair.AccessToken,
        refreshToken = pair.RefreshToken,
        expiresAt = pair.AccessExpiresAt,
        user = ToDto(u)
    });
});

app.MapPost("/api/auth/refresh", async (RefreshRequest req, TokenService tokens, BetakasDbContext db) =>
{
    var pair = await tokens.RotateAsync(req.RefreshToken);
    if (pair is null)
        return Results.Json(new { error = "Oturumun sona erdi, tekrar giriş yap." },
            statusCode: StatusCodes.Status401Unauthorized);

    // Yeni erişim jetonunun sahibini istemciye de bildir (rol/durum değişmiş olabilir).
    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    var sub = handler.ReadJwtToken(pair.AccessToken).Subject;
    var u = await db.Users.FirstAsync(x => x.Id == sub);

    return Results.Ok(new
    {
        token = pair.AccessToken,
        refreshToken = pair.RefreshToken,
        expiresAt = pair.AccessExpiresAt,
        user = ToDto(u)
    });
});

app.MapPost("/api/auth/logout", async (RefreshRequest req, TokenService tokens) =>
{
    await tokens.RevokeAsync(req.RefreshToken);
    return Results.Ok(new { ok = true });
});

/// Tüm cihazlardaki oturumları kapatır.
app.MapPost("/api/auth/logout-all", async (ClaimsPrincipal p, BetakasDbContext db, TokenService tokens) =>
{
    var me = await DomainEndpoints.CurrentUserAsync(p, db);
    if (me is null) return Results.Unauthorized();
    await tokens.RevokeAllAsync(me.Id);
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

app.MapPost("/api/auth/register", async (RegisterRequest req, BetakasDbContext db, LedgerService ledger) =>
{
    var name = (req.Name ?? "").Trim();
    var email = (req.Email ?? "").Trim();
    var org = (req.Org ?? "").Trim();
    var tagline = (req.Tagline ?? "").Trim();

    if (name.Length == 0 || email.Length == 0 || org.Length == 0 || tagline.Length == 0)
        return Results.BadRequest(new { error = "Tüm zorunlu alanları doldur." });
    if (email.IndexOf('@') < 1)
        return Results.BadRequest(new { error = "Geçerli bir e-posta gir." });
    if (string.IsNullOrEmpty(req.Password) || req.Password.Length < 6)
        return Results.BadRequest(new { error = "Şifre en az 6 karakter olmalı." });
    if (req.Role != "founder" && req.Role != "tester")
        return Results.BadRequest(new { error = "Geçersiz rol." });

    var lower = email.ToLowerInvariant();
    if (await db.Users.AnyAsync(x => x.Email.ToLower() == lower))
        return Results.BadRequest(new { error = "Bu e-posta zaten kayıtlı." });

    var expertise = (req.ExpertiseCategories ?? [])
        .Where(c => RequestActions.ProductCategories.Contains(c)).Distinct().ToList();
    if (req.Role == "tester" && expertise.Count == 0)
        return Results.BadRequest(new { error = "En az bir uzmanlık alanı seç." });
    if (expertise.Contains(ProfileActions.OtherCategory) && string.IsNullOrWhiteSpace(req.ExpertiseOther))
        return Results.BadRequest(new { error = "\"Diğer\" seçtin — hangi alanı kastettiğini yaz." });

    var tr = new System.Globalization.CultureInfo("tr-TR");
    var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var initials = (parts[0][..1] + (parts.Length > 1 ? parts[^1][..1] : "")).ToUpper(tr);

    var u = new User
    {
        Id = await ledger.NextIdAsync("u"),
        Name = name,
        Initials = initials,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        Tagline = tagline,
        ExpertiseCategories = expertise,
        ExpertiseOther = string.IsNullOrWhiteSpace(req.ExpertiseOther) ? null : req.ExpertiseOther.Trim(),
        Role = req.Role,
        Status = "pending" // kapalı ekosistem: yönetim onayı gerekir
    };
    if (req.Role == "founder") { u.Startup = org; u.Sector = string.IsNullOrWhiteSpace(req.Sector) ? "Diğer" : req.Sector; }
    else { u.Title = org; }

    db.Users.Add(u);

    // Kayit da bir state degisikligidir: yeni basvuru yonetim panelinde gorunmelidir.
    // rev artirilmazsa onbellek bayat kalir ve basvuru kimseye gorunmez.
    var platform = await db.PlatformState.FirstAsync(x => x.Id == 1);
    platform.Rev += 1;

    await db.SaveChangesAsync();

    return Results.Ok(new { ok = true, message = "Başvurun alındı. Yönetim onayladıktan sonra giriş yapabilirsin." });
});

// ---------------- State (salt okunur) ----------------
// Yazma yolu kaldırıldı: istemci artık ham state gönderemez, yalnızca domain uçlarını çağırır.

app.MapGet("/api/state", async (ClaimsPrincipal p, BetakasDbContext db, StateService svc) =>
{
    var me = await DomainEndpoints.CurrentUserAsync(p, db);
    if (me is null) return Results.Unauthorized();
    return Results.Ok(await svc.GetStateAsync(me.Id));
}).RequireAuthorization();

app.MapGet("/api/state/rev", async (StateService svc) =>
    Results.Ok(new { rev = await svc.GetRevAsync() })).RequireAuthorization();

app.MapPost("/api/admin/reset", async (
    ClaimsPrincipal p, BetakasDbContext db, SeedService seed, StateCache cache) =>
{
    var me = await DomainEndpoints.CurrentUserAsync(p, db);
    if (me is null) return Results.Unauthorized();
    if (me.Role != "admin")
        return Results.Json(new { error = "Demoyu yalnızca yönetim sıfırlayabilir." }, statusCode: 403);

    await seed.ResetAsync();
    // Sıfırlama rev sayacını geri sardığı için önbellek açıkça boşaltılmalı.
    cache.Clear();
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

// ---------------- Domain uçları ----------------
app.MapDomainEndpoints();

// ---------------- Public (giriş gerektirmez) ----------------
app.MapPublicEndpoints();

app.MapGet("/api/health", () => Results.Ok(new { ok = true, provider = useSqlServer ? "SqlServer" : "Postgres" }));

// ---------------- SPA fallback ----------------
// React istemcide yönlendirme yapar; /panel, /talep/r1 gibi derin linkler sunucuda
// dosya olarak yoktur. API dışındaki eşleşmeyen yollar index.html'e düşer ki
// sayfa yenilendiğinde ya da link paylaşıldığında 404 alınmasın.
if (isSpa)
{
    var indexPath = Path.Combine(uiRoot, "index.html");
    app.MapFallback(async ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.SendFileAsync(indexPath);
    });
}

app.Run();

/// <summary>Tümleşik testlerin WebApplicationFactory ile bu sınıfa erişebilmesi için.</summary>
public partial class Program;
