using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Betakas.Api.Data;

/// <summary>PostgreSQL migration seti: Migrations/Postgres.</summary>
public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : BetakasDbContext(options);

/// <summary>SQL Server migration seti: Migrations/SqlServer.</summary>
public class SqlServerDbContext(DbContextOptions<SqlServerDbContext> options) : BetakasDbContext(options);

/// <summary>
/// `dotnet ef migrations add` çalışırken uygulamayı ayağa kaldırmadan context üretir.
/// Bağlantı dizesi yalnızca şema karşılaştırması için gerekir; migration üretimi
/// veritabanına bağlanmaz.
/// </summary>
public class PostgresDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args)
    {
        // Sırayla: ortam değişkeni → user-secrets → yerel varsayılan.
        // user-secrets de okunur ki `supabase-kur.ps1` ile kaydedilen bağlantı,
        // ayrı bir terminalde `dotnet ef` çalıştırıldığında da bulunabilsin.
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");

        if (string.IsNullOrWhiteSpace(cs))
        {
            var secrets = new ConfigurationBuilder()
                .AddUserSecrets<PostgresDbContextFactory>(optional: true)
                .Build();
            cs = secrets["ConnectionStrings:Postgres"];
        }

        cs ??= "Host=localhost;Port=5432;Database=betakas;Username=postgres;Password=betakas123";

        // Uygulamayla aynı normalizasyon: Supabase gibi uzak veritabanlarında TLS ve
        // havuz ayarları burada da geçerli olmalı ki `dotnet ef database update` bağlanabilsin.
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        cs = Services.PostgresConnection.Normalize(cs, loggerFactory.CreateLogger("ef"));

        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(cs).Options;
        return new PostgresDbContext(options);
    }
}

public class SqlServerDbContextFactory : IDesignTimeDbContextFactory<SqlServerDbContext>
{
    public SqlServerDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer")
                 ?? "Server=localhost\\SQLEXPRESS;Database=betakas;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<SqlServerDbContext>().UseSqlServer(cs).Options;
        return new SqlServerDbContext(options);
    }
}
