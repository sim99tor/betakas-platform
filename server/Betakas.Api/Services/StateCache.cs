using Betakas.Api.Dto;

namespace Betakas.Api.Services;

/// <summary>
/// Kullanıcıdan bağımsız state anlık görüntüsü. Kişiye özel alanlar (oturumdaki kullanıcı,
/// kendi IBAN'ı) burada TUTULMAZ — istek anında eklenir, böylece önbellek herkes için ortaktır.
/// </summary>
public record StateSnapshot(StateDto Shared, IReadOnlyDictionary<string, string?> Ibans);

/// <summary>
/// State'i revizyon numarasına göre önbellekler.
///
/// Sunucu tek yazardır ve her başarılı eylem <c>rev</c>'i artırır; dolayısıyla rev değişmediği
/// sürece veri de değişmemiştir. Uzak bir veritabanında (ör. Supabase) bu, her sayfa yüklemesinde
/// 9 gidiş-dönüşü tek bir ucuz <c>rev</c> sorgusuna indirir.
///
/// Sıfırlama rev'i geri sardığı için orada önbellek açıkça temizlenir (bkz. Clear).
/// </summary>
public class StateCache
{
    private readonly Lock _gate = new();
    private long _rev = -1;
    private StateSnapshot? _snapshot;

    public StateSnapshot? Get(long rev)
    {
        lock (_gate)
        {
            return _rev == rev ? _snapshot : null;
        }
    }

    public void Set(long rev, StateSnapshot snapshot)
    {
        lock (_gate)
        {
            _rev = rev;
            _snapshot = snapshot;
        }
    }

    /// <summary>Demo sıfırlandığında çağrılır — rev geri sarabileceği için şart.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _rev = -1;
            _snapshot = null;
        }
    }
}
