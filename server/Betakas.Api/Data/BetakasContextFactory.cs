using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Data;

/// <summary>
/// İstek başına birden çok DbContext üretir. Tek bir DbContext üzerinde eşzamanlı sorgu
/// çalıştırılamadığı için, state'i paralel toplarken her sorgu kendi context'ini (ve
/// dolayısıyla kendi bağlantısını) kullanır.
/// </summary>
public interface IBetakasContextFactory
{
    BetakasDbContext Create();
}

public class PostgresContextFactory(DbContextOptions<PostgresDbContext> options) : IBetakasContextFactory
{
    public BetakasDbContext Create() => new PostgresDbContext(options);
}

public class SqlServerContextFactory(DbContextOptions<SqlServerDbContext> options) : IBetakasContextFactory
{
    public BetakasDbContext Create() => new SqlServerDbContext(options);
}
