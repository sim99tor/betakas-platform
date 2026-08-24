using System.Text.Json.Nodes;
using Betakas.Api.Data;
using Betakas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Betakas.Api.Services;

/// <summary>
/// İstemcideki <c>seedState()</c> fonksiyonunun sunucu tarafı karşılığı. Demo hesapları,
/// ürün sürümlerini, talepleri, oturumları ve iki defteri (token + ₺) birebir aynı
/// değerlerle üretir; böylece localStorage sürümüyle aynı sunum senaryosu çalışır.
/// </summary>
public partial class SeedService(BetakasDbContext db, ILogger<SeedService> log, StateCache cache)
{
    public const string DemoPassword = "betakas";

    private const decimal DefaultTokenPrice = 10m;
    private const int DefaultFeePct = 20;
    private const int SeedVersion = 7;
    private const long Day = 24L * 60 * 60 * 1000;

    private readonly long _now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private long DaysAgo(double n) => _now - (long)(n * Day);

    /// <summary>Veritabanı boşsa demo veriyi yazar. Doluysa hiçbir şey yapmaz.</summary>
    public async Task EnsureSeededAsync()
    {
        if (await db.PlatformState.AnyAsync())
        {
            log.LogInformation("Veritabanı zaten dolu, tohumlama atlandı.");
            return;
        }

        log.LogInformation("Boş veritabanı — demo veri tohumlanıyor.");
        await ResetAsync();
    }

    /// <summary>
    /// Tüm tabloları temizleyip demo veriyi yeniden yazar ("Demoyu Sıfırla").
    ///
    /// Uzak veritabanlarında (ör. Supabase) geçici ağ hataları için yeniden deneme açıktır;
    /// bu durumda elle açılan transaction'ın tamamı yürütme stratejisi içinde çalışmalıdır,
    /// aksi halde EF "user-initiated transaction desteklenmiyor" hatası verir.
    /// </summary>
    public async Task ResetAsync()
    {
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            // Sıra FK bağımlılıklarına göre: önce yapraklar.
            db.Sessions.RemoveRange(db.Sessions);
            db.Requests.RemoveRange(db.Requests);
            db.Versions.RemoveRange(db.Versions);
            db.Ledger.RemoveRange(db.Ledger);
            db.CashLedger.RemoveRange(db.CashLedger);
            db.Purchases.RemoveRange(db.Purchases);
            db.Withdrawals.RemoveRange(db.Withdrawals);
            db.Users.RemoveRange(db.Users);
            db.PlatformState.RemoveRange(db.PlatformState);
            await db.SaveChangesAsync();

            Build();
            await db.SaveChangesAsync();
            await ApplyDemoFixesAsync();
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        // Sifirlama rev sayacini geri sardigi icin onbellek burada gecersizlesmeli.
        // Gecersizlestirme mutasyonun yaninda durur ki hangi yoldan cagrilirsa cagrilsin
        // (uc nokta, testler, betik) atlanmasin.
        cache.Clear();
    }

    /// <summary>
    /// Demo verisinde sürüm notu tiklerini üretir: her sürümün yanıt verdiği bug listesinin
    /// çoğu "düzeltildi", son maddesi "sonraki sürüme", sondan ikincisi (liste yeterince
    /// uzunsa) "devam ediyor" işaretlenir. Böylece sunum açılışında "3/5 bug düzeltildi"
    /// tablosu dolu gelir. Gerçek tikler kurucunun kendi işaretlemeleridir.
    /// </summary>
    private async Task ApplyDemoFixesAsync()
    {
        var extractor = new BugExtractor(db);
        var versions = await db.Versions.OrderBy(v => v.CreatedAt).ToListAsync();

        foreach (var v in versions)
        {
            if (v.Fixes != null) continue;

            var source = await extractor.BugSourceForAsync(v);
            if (source is null) { v.Fixes = new JsonObject(); continue; }

            var bugs = await extractor.VersionBugsAsync(source.Id);
            var fixes = new JsonObject();

            for (var i = 0; i < bugs.Count; i++)
            {
                var fromEnd = bugs.Count - 1 - i;
                fixes[bugs[i].Id] = bugs.Count >= 3 && fromEnd == 0 ? "later"
                    : bugs.Count >= 5 && fromEnd == 1 ? "wip"
                    : "fixed";
            }

            v.Fixes = fixes;
        }
    }

    private static string Hash(string pw) => BCrypt.Net.BCrypt.HashPassword(pw);

    private void Build()
    {
        var pw = Hash(DemoPassword);

        // ---- Kullanıcılar ----
        var users = new List<User>
        {
            // Kurucular: kendi ürünü için test talebi açar, token harcar.
            new() { Id = "u1", Name = "Elif Aydın", Initials = "EA", Email = "elif@finbutce.co", PasswordHash = pw,
                Startup = "FinBütçe", Tagline = "Gençler için otomatik bütçe asistanı", Sector = "Fintek",
                Skills = ["Web", "Mobil", "Fintek"],
                ExpertiseCategories = ["Tüketici Uygulaması (B2C)", "Mobil Uygulama"],
                Role = "founder", Status = "active" },
            new() { Id = "u2", Name = "Mert Kaya", Initials = "MK", Email = "mert@stokpro.co", PasswordHash = pw,
                Startup = "StokPro", Tagline = "KOBİ'ler için akıllı stok yönetimi", Sector = "B2B SaaS",
                Skills = ["Web", "B2B", "API"],
                ExpertiseCategories = ["SaaS / B2B", "E-ticaret"],
                Role = "founder", Status = "active" },
            new() { Id = "u3", Name = "Zeynep Demir", Initials = "ZD", Email = "zeynep@mindpuzzle.co", PasswordHash = pw,
                Startup = "MindPuzzle", Tagline = "Beyin egzersizi mobil oyunu", Sector = "Mobil Oyun",
                Skills = ["Mobil", "Oyun", "UX"],
                ExpertiseCategories = ["Mobil Uygulama"],
                Role = "founder", Status = "active" },
            new() { Id = "u4", Name = "Deniz Şahin", Initials = "DŞ", Email = "deniz@rotakurye.co", PasswordHash = pw,
                Startup = "RotaKurye", Tagline = "Kurye filoları için rota optimizasyonu", Sector = "B2B SaaS",
                Skills = ["Web", "Harita"],
                ExpertiseCategories = ["SaaS / B2B"],
                Role = "founder", Status = "pending" },

            // Testçiler: ürün sahibi değil; test eder, token + nakit kazanır.
            new() { Id = "t1", Name = "Ayşe Yıldırım", Initials = "AY", Email = "ayse@testci.co", PasswordHash = pw,
                Title = "Kıdemli QA Uzmanı", Tagline = "6 yıl mobil & ödeme akışı test deneyimi",
                Skills = ["Mobil", "Ödeme", "Regresyon"],
                ExpertiseCategories = ["Mobil Uygulama", "Tüketici Uygulaması (B2C)"],
                Role = "tester", Status = "active" },
            new() { Id = "t2", Name = "Burak Şen", Initials = "BŞ", Email = "burak@testci.co", PasswordHash = pw,
                Title = "Ürün Tasarımcısı (UX)", Tagline = "Onboarding ve kullanılabilirlik odaklı geri bildirim",
                Skills = ["UX", "Onboarding", "Web"],
                ExpertiseCategories = ["SaaS / B2B", "E-ticaret"],
                Role = "tester", Status = "active" },
            new() { Id = "t3", Name = "Selin Arda", Initials = "SA", Email = "selin@testci.co", PasswordHash = pw,
                Title = "Full-stack Geliştirici", Tagline = "API ve entegrasyon tarafında bug avcısı",
                Skills = ["API", "Web", "Bug Avı"],
                ExpertiseCategories = ["SaaS / B2B", "AI Aracı"],
                Role = "tester", Status = "pending" },

            new() { Id = "admin", Name = "Platform Yönetimi", Initials = "PY", Email = "yonetim@betakas.co", PasswordHash = pw,
                Startup = "Betakas", Tagline = "Ekosistem operasyonu", Sector = "-",
                Role = "admin", Status = "active" }
        };
        db.Users.AddRange(users);

        // ---- Ürün sürümleri: kurucu v1 → v2 → v3 çıkarır, her sürümü ayrı teste sokar ----
        db.Versions.AddRange(
            new ProductVersion { Id = "v1", OwnerId = "u1", Label = "v1.0", Url = "https://finbutce.example.app", CreatedAt = DaysAgo(29),
                Notes = "İlk yayınlanabilir sürüm: harcama girişi, basit kategori listesi, aylık özet." },
            new ProductVersion { Id = "v2", OwnerId = "u1", Label = "v1.1", Url = "https://finbutce.example.app/v1-1", CreatedAt = DaysAgo(2),
                Notes = "Otomatik kategorilendirme motoru eklendi, banka bağlantı akışı yeniden yazıldı. v1.0 testlerinde en çok şikâyet edilen manuel giriş yükü hedeflendi." },

            new ProductVersion { Id = "v3", OwnerId = "u2", Label = "v1.0", Url = "https://stokpro.example.app", CreatedAt = DaysAgo(30),
                Notes = "Barkod tarama ve temel stok kartları." },
            new ProductVersion { Id = "v4", OwnerId = "u2", Label = "v1.2", Url = "https://stokpro.example.app/rapor", CreatedAt = DaysAgo(25),
                Notes = "Raporlama ekranı eklendi; barkod çift okuma hatası düzeltildi." },
            new ProductVersion { Id = "v5", OwnerId = "u2", Label = "v2.0", Url = "https://stokpro.example.app/onboarding", CreatedAt = DaysAgo(4),
                Notes = "Onboarding baştan tasarlandı: 7 adım 3 adıma indirildi, ilerleme çubuğu ve toplu ürün içe aktarma eklendi." },

            new ProductVersion { Id = "v6", OwnerId = "u3", Label = "v1.0", Url = "https://mindpuzzle.example.app", CreatedAt = DaysAgo(28),
                Notes = "İlk 20 bölüm, temel öğretici." },
            new ProductVersion { Id = "v7", OwnerId = "u3", Label = "v1.5", Url = "https://mindpuzzle.example.app/v15", CreatedAt = DaysAgo(3),
                Notes = "Zorluk eğrisi yeniden dengelendi, öğretici 3 ekrana yayıldı, ipucu butonu görünür hâle getirildi." });

        // ---- Test talepleri ----
        db.Requests.AddRange(
            // Kapanmış (tarihçe) talepler — itibar puanlarının kaynağı.
            new TestRequest { Id = "r90", OwnerId = "u3", VersionId = "v6", Title = "MindPuzzle bulmaca zorluk dengesi", Url = "https://mindpuzzle.example.app",
                ProductCategory = "Mobil Uygulama", FeedbackType = "İlk İzlenim",
                Scenario = "İlk 3 bölümü oynayın, zorluk eğrisini ve öğreticiyi değerlendirin.",
                Credits = 15, Slots = 2, Visibility = "public", Status = "closed", CreatedAt = DaysAgo(21) },
            new TestRequest { Id = "r91", OwnerId = "u2", VersionId = "v3", Title = "StokPro barkod tarama modülü", Url = "https://stokpro.example.app",
                ProductCategory = "SaaS / B2B", FeedbackType = "Bug Avı",
                Scenario = "Barkod tarama ile 10 ürün girin, hatalı okuma senaryolarını deneyin.",
                Credits = 20, Slots = 1, Visibility = "public", Status = "closed", CreatedAt = DaysAgo(14) },
            new TestRequest { Id = "r92", OwnerId = "u1", VersionId = "v1", Title = "FinBütçe ilk sürüm genel izlenim", Url = "https://finbutce.example.app",
                ProductCategory = "Tüketici Uygulaması (B2C)", FeedbackType = "İlk İzlenim",
                Scenario = "Uygulamayı ilk kez açan biri gibi gezin, genel izlenimini ve ilk tepkini paylaş.",
                Credits = 15, Slots = 2, Visibility = "public", Status = "closed", CreatedAt = DaysAgo(27), Boosted = false },
            new TestRequest { Id = "r93", OwnerId = "u2", VersionId = "v4", Title = "StokPro raporlama ekranı kullanılabilirlik testi", Url = "https://stokpro.example.app/rapor",
                ProductCategory = "SaaS / B2B", FeedbackType = "UX / Kullanılabilirlik",
                Scenario = "Aylık stok raporunu filtreleyip dışa aktarmayı deneyin; hangi adımda tereddüt ettiğinizi not edin.",
                Credits = 20, Slots = 2, Visibility = "public", Status = "closed", CreatedAt = DaysAgo(24) },
            new TestRequest { Id = "r94", OwnerId = "u3", VersionId = "v6", Title = "MindPuzzle mağaza sayfası ilk izlenim", Url = "https://mindpuzzle.example.app/store",
                ProductCategory = "Mobil Uygulama", FeedbackType = "İlk İzlenim",
                Scenario = "Mağaza sayfasını inceleyin: ekran görüntüleri ve açıklama sizi indirmeye ikna ediyor mu?",
                Credits = 15, Slots = 1, Visibility = "public", Status = "closed", CreatedAt = DaysAgo(17) },

            // Açık talepler.
            new TestRequest { Id = "r1", OwnerId = "u2", VersionId = "v5", Title = "StokPro yeni onboarding akışı", Url = "https://stokpro.example.app/onboarding",
                ProductCategory = "SaaS / B2B", FeedbackType = "Onboarding Akışı",
                Scenario = "Sıfırdan hesap açın, ilk depo ve 5 ürünü ekleyin. Nerede takıldığınızı, hangi adımın gereksiz olduğunu not edin.",
                Credits = 15, Slots = 2, Visibility = "public", Status = "open", CreatedAt = DaysAgo(3) },
            // Büyüme aşaması: senaryo terk etme anını sorar, teslim formu da ona göre açılır.
            new TestRequest { Id = "r2", OwnerId = "u3", VersionId = "v7", Title = "MindPuzzle ilk 5 dakika deneyimi", Url = "https://mindpuzzle.example.app",
                ProductCategory = "Mobil Uygulama", Stage = "Büyüme", FeedbackType = "İlk İzlenim",
                Scenario = "Uygulamayı hiç bilmeyen biri gibi açın. İlk 5 dakikada ne anladınız, ne sıkıcıydı, nerede kapatma isteği duydunuz?",
                Credits = 20, Slots = 1, Visibility = "public", Status = "open", CreatedAt = DaysAgo(2) },
            new TestRequest { Id = "r3", OwnerId = "u1", VersionId = "v2", Title = "FinBütçe harcama kategorilendirme testi", Url = "https://finbutce.example.app",
                ProductCategory = "Tüketici Uygulaması (B2C)", Stage = "MVP", FeedbackType = "Bug Avı",
                Scenario = "Demo hesapla 20 harcama girin, otomatik kategorilendirmenin hatalarını listeleyin. Banka bağlantı akışını da deneyin.",
                Credits = 25, Slots = 2, Visibility = "exclude-sector", Status = "open", CreatedAt = DaysAgo(1) },
            // Fikir/Prototip aşaması: henüz çalışan ekran yok, Figma linki üzerinden test edilir.
            new TestRequest { Id = "r4", OwnerId = "u1", VersionId = "v2", Title = "FinBütçe premium plan ekranı konsept testi", Url = "https://figma.com/proto/finbutce-premium",
                ProductCategory = "Tüketici Uygulaması (B2C)", Stage = "Fikir/Prototip", FeedbackType = "İlk İzlenim",
                Scenario = "Bu bir Figma prototipi — tıklanmayan yerler olabilir. Ekranları gezip premium planın ne vaat ettiğini, hangi ifadelerin kafanı karıştırdığını ve böyle bir özellik için para ödeyip ödemeyeceğini yaz.",
                Credits = 15, Slots = 2, Visibility = "public", Status = "open", CreatedAt = DaysAgo(0.5) });

        SeedSessions();
        SeedLedger();
        SeedCash();

        db.PlatformState.Add(new PlatformState
        {
            Id = 1,
            Rev = 1,
            TokenPrice = DefaultTokenPrice,
            FeePct = DefaultFeePct,
            SprintNo = 4,
            Seq = 100,
            SeedVersion = SeedVersion
        });
    }
}
