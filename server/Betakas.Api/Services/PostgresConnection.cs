using Npgsql;

namespace Betakas.Api.Services;

/// <summary>
/// PostgreSQL bağlantı dizesini normalize eder. Supabase de PostgreSQL olduğu için ayrı bir
/// sağlayıcı gerekmez — yalnızca birkaç bağlantı ayarı farklıdır ve burada otomatik uygulanır:
///
/// • <b>TLS</b> — Supabase şifresiz bağlantı kabul etmez; SslMode verilmemişse Require yapılır.
/// • <b>Havuz (PgBouncer)</b> — Supabase'in "Transaction pooler" adresi 6543 portundadır ve
///   hazırlanmış ifadeleri (prepared statements) desteklemez. Npgsql varsayılan olarak bunları
///   kullandığından, bu portta otomatik hazırlama kapatılır ve bağlantı sıfırlaması devre dışı
///   bırakılır; aksi halde "prepared statement already exists" hataları alınır.
/// • <b>Zaman aşımı</b> — uzak veritabanına bağlanırken yerelden daha uzun süre gerekir.
/// </summary>
public static class PostgresConnection
{
    /// <summary>Supabase'in işlem (transaction) havuzu portu.</summary>
    private const int TransactionPoolerPort = 6543;

    public static string Normalize(string connectionString, ILogger logger)
    {
        // Yapilandirma kaynaklari (ozellikle kabuk borulari) dizenin basina BOM veya
        // bosluk birakabilir; Npgsql bunu anahtar adinin parcasi sayip patlar.
        connectionString = connectionString.Trim().TrimStart('﻿');

        var b = new NpgsqlConnectionStringBuilder(connectionString);

        var isRemote = !IsLocal(b.Host);

        // Uzak veritabanlarında TLS zorunlu kabul edilir; yerelde kullanıcının seçimi korunur.
        if (isRemote && b.SslMode is SslMode.Disable or SslMode.Allow)
        {
            b.SslMode = SslMode.Require;
            logger.LogInformation("Uzak veritabanı: SSL Mode=Require olarak ayarlandı.");
        }

        if (b.Port == TransactionPoolerPort)
        {
            // PgBouncer işlem modunda oturum durumu paylaşıldığı için bu ikisi şart.
            b.MaxAutoPrepare = 0;
            b.NoResetOnClose = true;
            logger.LogInformation(
                "İşlem havuzu (port {Port}) algılandı: hazırlanmış ifadeler kapatıldı. " +
                "Migration uygularken oturum havuzunu (5432) kullan.", TransactionPoolerPort);
        }

        if (isRemote && b.Timeout < 30) b.Timeout = 30;
        if (isRemote && b.CommandTimeout < 60) b.CommandTimeout = 60;

        // Uzak veritabaninda her yeni baglanti TLS el sikismasi demektir; bir kac baglantiyi
        // acik tutmak sayfa yuklemelerindeki ilk gecikmeyi ortadan kaldirir. State paralel
        // toplandigi icin havuzun ayni anda ~10 baglantiyi karsilamasi gerekir.
        if (isRemote)
        {
            if (b.MinPoolSize < 4) b.MinPoolSize = 4;
            if (b.MaxPoolSize < 20) b.MaxPoolSize = 20;
            if (b.KeepAlive == 0) b.KeepAlive = 30;
        }

        return b.ConnectionString;
    }

    private static bool IsLocal(string? host) =>
        string.IsNullOrEmpty(host)
        || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host is "127.0.0.1" or "::1";

    /// <summary>Günlüğe basmak için parolayı gizler.</summary>
    public static string Describe(string connectionString)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(connectionString);
            return $"{b.Host}:{b.Port}/{b.Database} (kullanıcı: {b.Username}, ssl: {b.SslMode})";
        }
        catch
        {
            return "(bağlantı dizesi çözümlenemedi)";
        }
    }
}
