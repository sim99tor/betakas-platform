using System.Text.Json;
using System.Text.Json.Nodes;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Betakas.Api.Data;

/// <summary>
/// Ortak model. Migration setleri context tipine bağlı olduğu için her sağlayıcı kendi
/// türetilmiş context'ini kullanır (bkz. <see cref="PostgresDbContext"/>,
/// <see cref="SqlServerDbContext"/>); böylece iki sağlayıcının migration'ları çakışmaz.
/// </summary>
public abstract class BetakasDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<PlatformState> PlatformState => Set<PlatformState>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProductVersion> Versions => Set<ProductVersion>();
    public DbSet<TestRequest> Requests => Set<TestRequest>();
    public DbSet<TestSession> Sessions => Set<TestSession>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<CashLedgerEntry> CashLedger => Set<CashLedgerEntry>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    // ---- Serbest JSON alanları (feedback, sürüm notu tikleri) ----
    // İfade ağaçları isteğe bağlı parametreli çağrı içeremediği için (CS0854)
    // ToJsonString/Parse doğrudan lambda içinde değil, bu yardımcılar üzerinden çağrılır.
    private static string? ToJson(JsonObject? v) => v?.ToJsonString();

    private static JsonObject? FromJson(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : JsonNode.Parse(v)!.AsObject();

    private static bool JsonEquals(JsonObject? a, JsonObject? b) => ToJson(a) == ToJson(b);

    private static int JsonHash(JsonObject? v) => ToJson(v)?.GetHashCode() ?? 0;

    private static JsonObject? JsonSnapshot(JsonObject? v) => FromJson(ToJson(v));

    private static readonly ValueConverter<JsonObject?, string?> JsonObjectConverter =
        new(v => ToJson(v), v => FromJson(v));

    private static readonly ValueComparer<JsonObject?> JsonObjectComparer =
        new((a, b) => JsonEquals(a, b), v => JsonHash(v), v => JsonSnapshot(v));

    // ---- String listeleri ----
    // PostgreSQL'de yerel text[] sütunu kullanılır. SQL Server'da dizi tipi olmadığı için
    // liste JSON metnine serileştirilir; her iki durumda da model tarafı List<string> kalır.
    private static string ListToJson(List<string> v) => JsonSerializer.Serialize(v ?? []);

    private static List<string> ListFromJson(string? v) =>
        string.IsNullOrWhiteSpace(v) ? [] : (JsonSerializer.Deserialize<List<string>>(v) ?? []);

    private static bool ListEquals(List<string>? a, List<string>? b) =>
        (a ?? []).SequenceEqual(b ?? []);

    private static int ListHash(List<string>? v) => ListToJson(v ?? []).GetHashCode();

    private static List<string> ListSnapshot(List<string>? v) => [.. (v ?? [])];

    private static readonly ValueConverter<List<string>, string> StringListConverter =
        new(v => ListToJson(v), v => ListFromJson(v));

    private static readonly ValueComparer<List<string>> StringListComparer =
        new((a, b) => ListEquals(a, b), v => ListHash(v), v => ListSnapshot(v));

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Sağlayıcıya göre değişen tek şey sütun tipleridir; şema ve ilişkiler aynıdır.
        var isPg = Database.IsNpgsql();
        var jsonType = isPg ? "jsonb" : "nvarchar(max)";

        b.Entity<PlatformState>(e =>
        {
            e.ToTable("platform_state");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TokenPrice).HasColumnType("numeric(12,2)");
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();

            if (isPg)
            {
                e.Property(x => x.Skills).HasColumnType("text[]");
                e.Property(x => x.ExpertiseCategories).HasColumnType("text[]");
            }
            else
            {
                e.Property(x => x.Skills).HasConversion(StringListConverter)
                    .HasColumnType("nvarchar(max)").Metadata.SetValueComparer(StringListComparer);
                e.Property(x => x.ExpertiseCategories).HasConversion(StringListConverter)
                    .HasColumnType("nvarchar(max)").Metadata.SetValueComparer(StringListComparer);
            }
        });

        b.Entity<ProductVersion>(e =>
        {
            e.ToTable("versions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Fixes).HasConversion(JsonObjectConverter).HasColumnType(jsonType)
                .Metadata.SetValueComparer(JsonObjectComparer);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OwnerId);
        });

        b.Entity<TestRequest>(e =>
        {
            e.ToTable("requests");
            e.HasKey(x => x.Id);
            // SQL Server birden fazla cascade yolunu reddeder; sahip silinince talepler
            // PostgreSQL'de olduğu gibi düşsün diye cascade yalnızca burada tutulur.
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Version).WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.OwnerId);
            e.HasIndex(x => x.Status);
        });

        b.Entity<TestSession>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Feedback).HasConversion(JsonObjectConverter).HasColumnType(jsonType)
                .Metadata.SetValueComparer(JsonObjectComparer);
            e.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
            // Testçi tarafı Restrict: talep zaten cascade siliyor, iki yol SQL Server'da hata verir.
            e.HasOne(x => x.Tester).WithMany().HasForeignKey(x => x.TesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.RequestId);
            e.HasIndex(x => x.TesterId);
        });

        // Defter satırları kasıtlı olarak FK'sız: "system" / "escrow" / "revenue" / "pool" /
        // "bank" gibi sanal hesaplar da from/to alanlarında geçer.
        b.Entity<LedgerEntry>(e =>
        {
            e.ToTable("ledger");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Ts);
            e.HasIndex(x => x.Ref);
        });

        b.Entity<CashLedgerEntry>(e =>
        {
            e.ToTable("cash_ledger");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("numeric(14,2)");
            e.HasIndex(x => x.Ts);
            e.HasIndex(x => x.Ref);
        });

        b.Entity<Purchase>(e =>
        {
            e.ToTable("purchases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Gross).HasColumnType("numeric(14,2)");
            e.Property(x => x.Fee).HasColumnType("numeric(14,2)");
            e.Property(x => x.Pool).HasColumnType("numeric(14,2)");
            e.HasIndex(x => x.UserId);
        });

        b.Entity<Withdrawal>(e =>
        {
            e.ToTable("withdrawals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("numeric(14,2)");
            e.HasIndex(x => x.UserId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LoginAttempt>(e =>
        {
            e.ToTable("login_attempts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Key, x.At });
        });
    }
}
