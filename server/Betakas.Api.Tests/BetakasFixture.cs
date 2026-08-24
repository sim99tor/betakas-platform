using Xunit;

namespace Betakas.Api.Tests;

/// <summary>
/// Tüm test sınıfları tek bir sunucu örneğini ve tek bir test veritabanını paylaşır.
/// Testler aynı veritabanına yazdığı için koleksiyon sıralıdır (xUnit varsayılanı).
/// </summary>
public class BetakasFixture : IAsyncLifetime
{
    public BetakasFactory Factory { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Factory = new BetakasFactory();
        // İlk istemci oluşturma sunucuyu ayağa kaldırır ve migration'ları uygular.
        _ = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }

    public ApiClient NewClient() => new(Factory.CreateClient());

    public Task ResetAsync() => Factory.ResetAsync();
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<BetakasFixture>;
