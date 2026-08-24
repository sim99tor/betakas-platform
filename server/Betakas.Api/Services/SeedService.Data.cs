using System.Text.Json.Nodes;
using Betakas.Api.Models;

namespace Betakas.Api.Services;

/// <summary>
/// Seed'in hacimli kısmı: tamamlanmış test oturumları (itibar puanlarının kaynağı),
/// token defteri ve para defteri. Değerler istemcideki <c>seedState()</c> ile birebir aynıdır.
/// </summary>
public partial class SeedService
{
    private static JsonObject Fb(params (string Key, string Value)[] fields)
    {
        var o = new JsonObject();
        foreach (var (k, v) in fields) o[k] = v;
        return o;
    }

    private void SeedSessions()
    {
        db.Sessions.AddRange(
            // --- Tarihçe: tamamlanmış testler ---
            new TestSession { Id = "s1", RequestId = "r90", TesterId = "u1", Status = "accepted",
                AppliedAt = DaysAgo(20), SubmittedAt = DaysAgo(19), Rating = 5, OwnerRating = 4,
                DurationMin = 35, ProofUrl = "https://loom.com/share/demo-s1",
                Feedback = Fb(
                    ("firstImpression", "Öğretici çok hızlı geçiyor; 2. bölümde mekaniği hâlâ tam anlamamıştım. Görsel stil çok başarılı, ilk açılışta 'kaliteli oyun' hissi veriyor."),
                    ("bugs", "1) 3. bölümde geri al butonu iki kez basınca oyunu donduruyor. 2) Bildirim izni reddedilince ayarlar ekranı boş geliyor."),
                    ("ux", "Zorluk 1-2. bölümde çok düşük, 3. bölümde aniden sıçrıyor. Aradaki basamak eksik. İpucu butonu çok gizli, sağ üstte fark edilmiyor."),
                    ("wouldUse", "belki"),
                    ("wouldUseWhy", "Oyun güzel ama bildirimsiz geri dönmeyi hatırlamam, retention kancası eksik.")) },

            new TestSession { Id = "s2", RequestId = "r90", TesterId = "u2", Status = "accepted",
                AppliedAt = DaysAgo(20), SubmittedAt = DaysAgo(18), Rating = 4, OwnerRating = 4,
                DurationMin = 25, ProofUrl = "https://loom.com/share/demo-s2",
                Feedback = Fb(
                    ("firstImpression", "Menü akışı temiz, oyuna giriş hızlı. Ses tasarımı ortalamanın üzerinde."),
                    ("bugs", "Tablet ekranında (iPad mini) alt menü kesiliyor; skor tablosu yatay modda taşıyor."),
                    ("ux", "Bölüm sonunda 'tekrar oyna' ve 'sonraki' butonları çok yakın, yanlışlıkla tekrar başlattım iki kez."),
                    ("wouldUse", "hayır"),
                    ("wouldUseWhy", "Hedef kitlesi değilim, ama çocuğum için indirirdim.")) },

            new TestSession { Id = "s3", RequestId = "r91", TesterId = "u1", Status = "accepted",
                AppliedAt = DaysAgo(13), SubmittedAt = DaysAgo(12), Rating = 5, OwnerRating = 5,
                DurationMin = 40, ProofUrl = "https://loom.com/share/demo-s3",
                Feedback = Fb(
                    ("firstImpression", "Barkod tarama beklediğimden hızlı. Kamera izni akışı sorunsuz."),
                    ("bugs", "1) Aynı barkodu 2 sn içinde iki kez okutunca ürün çift ekleniyor (kritik). 2) EAN-8 formatını tanımıyor. 3) Karanlık ortamda fener otomatik açılmıyor."),
                    ("ux", "Tarama sonrası onay ekranı gereksiz, ayar olarak kapatılabilmeli. Toplu giriş modu çok değerli ama menüde keşfedilmesi zor."),
                    ("wouldUse", "evet"),
                    ("wouldUseWhy", "Kendi işletmem olsa kullanırdım; çift kayıt bug'ı çözülürse üretime hazır.")) },

            new TestSession { Id = "s6", RequestId = "r92", TesterId = "u2", Status = "accepted",
                AppliedAt = DaysAgo(26), SubmittedAt = DaysAgo(25.5), Rating = 5, OwnerRating = 5,
                DurationMin = 22, ProofUrl = "https://loom.com/share/demo-s6",
                Feedback = Fb(
                    ("firstImpression", "Arayüz sade ve anlaşılır, ilk açılışta ne yapacağımı hemen kavradım."),
                    ("bugs", "1) Harcama tutarına virgüllü değer girince (12,50) kayıt 1250 TL olarak yazılıyor (kritik). 2) Aylık özet ekranı ay değiştirince eski ayın toplamını göstermeye devam ediyor. 3) Kategori silince o kategorideki harcamalar listeden tamamen kayboluyor."),
                    ("ux", "Menüler beklediğim yerde, öğrenme eğrisi çok düşük."),
                    ("wouldUse", "evet"),
                    ("wouldUseWhy", "Günlük harcama takibi için gerçekten kullanışlı görünüyor.")) },

            new TestSession { Id = "s7", RequestId = "r92", TesterId = "u3", Status = "accepted",
                AppliedAt = DaysAgo(26), SubmittedAt = DaysAgo(25), Rating = 4, OwnerRating = 4,
                DurationMin = 18, ProofUrl = "https://loom.com/share/demo-s7",
                Feedback = Fb(
                    ("firstImpression", "Renk paleti ve tipografi güven veriyor; bir finans uygulamasından beklediğim ciddiyette."),
                    ("bugs", "1) Banka bağlama akışında geri tuşuna basınca uygulama giriş ekranına atıyor, girilen bilgiler kayboluyor. 2) Tarih seçicide gelecek tarih seçilebiliyor, ileri tarihli harcama girilebiliyor."),
                    ("ux", "Harcama eklemek 4 dokunuş sürüyor; ana ekranda hızlı ekle butonu olmalı. Kategori ikonları birbirine çok benziyor."),
                    ("wouldUse", "belki"),
                    ("wouldUseWhy", "Manuel giriş yükü fazla; otomatik kategorilendirme gelirse günlük kullanırım.")) },

            // --- Tarihçe: profesyonel testçilerin tamamladığı testler ---
            new TestSession { Id = "s10", RequestId = "r93", TesterId = "t1", Status = "accepted",
                AppliedAt = DaysAgo(24), SubmittedAt = DaysAgo(22), Rating = 5, OwnerRating = 5,
                DurationMin = 45, ProofUrl = "https://loom.com/share/demo-s10",
                Feedback = Fb(
                    ("firstImpression", "Raporlama ekranı ilk bakışta profesyonel duruyor; filtre alanlarının üstte toplanmış olması işimi kolaylaştırdı. Yükleme süresi 3 sn civarında, kabul edilebilir."),
                    ("bugs", "1) Tarih aralığı 90 günü aşınca dışa aktarma sessizce boş CSV üretiyor (kritik). 2) Filtreyi temizle butonu kategori seçimini sıfırlamıyor. 3) Safari'de tablo başlıkları kaydırmada sabitlenmiyor."),
                    ("ux", "Dışa aktarma butonu sağ altta, tablo uzun olduğunda ekrandan çıkıyor; üste taşınmalı. Rapor önizlemesi ile indirilen dosyanın kolon sırası farklı, bu güven kırıyor."),
                    ("wouldUse", "evet"),
                    ("wouldUseWhy", "Depo yönetimi yapan bir ekipte çalışsam bu ekranı günlük kullanırdım; CSV bug'ı çözülmeli.")) },

            new TestSession { Id = "s11", RequestId = "r93", TesterId = "t2", Status = "accepted",
                AppliedAt = DaysAgo(24), SubmittedAt = DaysAgo(21.5), Rating = 4, OwnerRating = 4,
                DurationMin = 30, ProofUrl = "https://loom.com/share/demo-s11",
                Feedback = Fb(
                    ("firstImpression", "Ekranın bilgi yoğunluğu yüksek; ilk 30 saniyede nereye bakacağımı bilemedim. Renk kullanımı tutarlı ama vurgu eksik."),
                    ("bugs", "Grafikteki tooltip mobil görünümde parmağın altında kalıyor; küçük ekranda okunmuyor."),
                    ("ux", "Varsayılan olarak 'son 30 gün' seçili gelmeli, boş ekran yerine dolu bir rapor karşılamalı. Filtre etiketleri seçildikten sonra görünmüyor, ne filtrelediğimi hatırlamak zor."),
                    ("wouldUse", "belki"),
                    ("wouldUseWhy", "Kurulum sonrası değerli ama ilk açılış deneyimi öğretici gerektiriyor.")) },

            new TestSession { Id = "s12", RequestId = "r94", TesterId = "t1", Status = "accepted",
                AppliedAt = DaysAgo(17), SubmittedAt = DaysAgo(16), Rating = 5, OwnerRating = 5,
                DurationMin = 25, ProofUrl = "https://loom.com/share/demo-s12",
                Feedback = Fb(
                    ("firstImpression", "Mağaza görselleri oyunun ne olduğunu ilk ekranda anlatıyor, bu iyi. Başlık altındaki açıklama fazla uzun, ilk iki satırdan sonrasını okumadım."),
                    ("bugs", "Ekran görüntüsü galerisinde 4. görsel düşük çözünürlüklü yükleniyor, bulanık görünüyor."),
                    ("ux", "İndir butonunun üstünde sosyal kanıt (indirme sayısı / puan) yok; ikna için en büyük eksik bu. Açıklamanın ilk cümlesi fayda değil özellik anlatıyor."),
                    ("wouldUse", "evet"),
                    ("wouldUseWhy", "Bulmaca oyunu seven biri olarak indirirdim, mağaza sayfası ikna edici.")) },

            // --- Aktif demo senaryosu ---
            // Ayşe (testçi) Mert'in talebini test ediyor — teslim etmesi bekleniyor.
            new TestSession { Id = "s13", RequestId = "r1", TesterId = "t1", Status = "approved",
                AppliedAt = DaysAgo(1), ProofUrl = "" },

            // Burak (testçi) Elif'in talebine başvurdu — onay bekliyor.
            new TestSession { Id = "s14", RequestId = "r3", TesterId = "t2", Status = "applied",
                AppliedAt = DaysAgo(0.4), ProofUrl = "" },

            // Mert'in açık talebine Zeynep teslim etti, onay bekliyor.
            new TestSession { Id = "s4", RequestId = "r1", TesterId = "u3", Status = "submitted",
                AppliedAt = DaysAgo(2), SubmittedAt = DaysAgo(1),
                DurationMin = 30, ProofUrl = "https://loom.com/share/demo-s4",
                Feedback = Fb(
                    ("firstImpression", "Kayıt formu uzun ama ilerleme çubuğu olduğu için katlanılabilir. Depo ekleme adımında ne beklendiği net değildi."),
                    ("bugs", "1) Vergi numarası alanına harf girilebiliyor, sonraki adımda sessizce hata veriyor. 2) 'Geri' tuşu 3. adımda formu sıfırlıyor."),
                    ("ux", "5 ürün ekleme adımında toplu içe aktarma seçeneği yok; tek tek girmek onboarding için çok uzun. İlk depo için hazır şablon önerilebilir."),
                    ("wouldUse", "belki"),
                    ("wouldUseWhy", "Oyun tarafındayım ama e-ticarete geçen tanıdıklarıma önerirdim.")) },

            // Admin demosu: itirazlı oturum (tek cümlelik çöp feedback + kanıt yok).
            new TestSession { Id = "s5", RequestId = "r3", TesterId = "u3", Status = "disputed",
                AppliedAt = DaysAgo(1), SubmittedAt = DaysAgo(0.5),
                DurationMin = 4, ProofUrl = "",
                DisputeNote = "Ekran kaydı yok, form tek cümlelik. Test yapıldığına dair kanıt bulunmuyor.",
                Feedback = Fb(
                    ("firstImpression", "Güzel uygulama olmuş elinize sağlık."),
                    ("bugs", "Bug görmedim."),
                    ("ux", "Gayet iyi."),
                    ("wouldUse", "evet"),
                    ("wouldUseWhy", "İyi fikir.")) });
    }

    // ---- Çift kayıtlı token defteri (kronolojik) ----
    private int _lid;

    private void L(long ts, string from, string to, int amount, string type, string? @ref, string note) =>
        db.Ledger.Add(new LedgerEntry
        {
            Id = "l" + (++_lid), Ts = ts, From = from, To = to,
            Amount = amount, Type = type, Ref = @ref, Note = note
        });

    private void SeedLedger()
    {
        // Kuruluş üyesi bonusları.
        L(DaysAgo(30), "system", "u1", 100, "bonus", null, "Kuruluş üyesi başlangıç tokeni");
        L(DaysAgo(30), "system", "u2", 100, "bonus", null, "Kuruluş üyesi başlangıç tokeni");
        L(DaysAgo(29), "system", "u3", 100, "bonus", null, "Kuruluş üyesi başlangıç tokeni");

        // r92: Elif 2×15 bloke etti; Mert ve Zeynep test etti
        // (karşılıklı yüksek puan deseni — admin analitiğinde şüpheli olarak görünür).
        L(DaysAgo(27), "u1", "escrow", 30, "escrow_lock", "r92", "Test talebi açıldı: FinBütçe ilk izlenim (2 slot × 15)");
        L(DaysAgo(25), "escrow", "u2", 15, "escrow_release", "r92", "Feedback onaylandı → Mert Kaya (5★)");
        L(DaysAgo(24.5), "escrow", "u3", 15, "escrow_release", "r92", "Feedback onaylandı → Zeynep Demir (4★)");

        // r90: Zeynep 2×15 bloke etti, iki test onaylandı.
        L(DaysAgo(21), "u3", "escrow", 30, "escrow_lock", "r90", "Test talebi açıldı: MindPuzzle bulmaca dengesi (2 slot × 15)");
        L(DaysAgo(19), "escrow", "u1", 15, "escrow_release", "r90", "Feedback onaylandı → Elif Aydın (5★)");
        L(DaysAgo(18), "escrow", "u2", 15, "escrow_release", "r90", "Feedback onaylandı → Mert Kaya (4★)");

        // r91: Mert 1×20 bloke etti, test onaylandı.
        L(DaysAgo(14), "u2", "escrow", 20, "escrow_lock", "r91", "Test talebi açıldı: StokPro barkod modülü (1 slot × 20)");
        L(DaysAgo(12), "escrow", "u1", 20, "escrow_release", "r91", "Feedback onaylandı → Elif Aydın (5★)");

        // r93: Mert 2×20 bloke etti, iki profesyonel testçi teslim etti.
        L(DaysAgo(24), "u2", "escrow", 40, "escrow_lock", "r93", "Test talebi açıldı: StokPro raporlama ekranı (2 slot × 20)");
        L(DaysAgo(22), "escrow", "t1", 20, "escrow_release", "r93", "Feedback onaylandı → Ayşe Yıldırım (5★)");
        L(DaysAgo(22), "system", "t1", 2, "rep_bonus", "r93", "İtibar çarpanı bonusu (1.1×) → Ayşe Yıldırım");
        L(DaysAgo(21.5), "escrow", "t2", 20, "escrow_release", "r93", "Feedback onaylandı → Burak Şen (4★)");

        // r94: Zeynep 1×15 bloke etti, Ayşe test etti.
        L(DaysAgo(17), "u3", "escrow", 15, "escrow_lock", "r94", "Test talebi açıldı: MindPuzzle mağaza sayfası (1 slot × 15)");
        L(DaysAgo(16), "escrow", "t1", 15, "escrow_release", "r94", "Feedback onaylandı → Ayşe Yıldırım (5★)");
        L(DaysAgo(16), "system", "t1", 3, "rep_bonus", "r94", "İtibar çarpanı bonusu (1.2×) → Ayşe Yıldırım");

        // Hâlâ açık olan taleplerin escrow blokeleri.
        L(DaysAgo(3), "u2", "escrow", 30, "escrow_lock", "r1", "Test talebi açıldı: StokPro onboarding (2 slot × 15)");
        L(DaysAgo(2), "u3", "escrow", 20, "escrow_lock", "r2", "Test talebi açıldı: MindPuzzle ilk 5 dk (1 slot × 20)");
        L(DaysAgo(1), "u1", "escrow", 50, "escrow_lock", "r3", "Test talebi açıldı: FinBütçe kategorilendirme (2 slot × 25)");
        L(DaysAgo(0.5), "u1", "escrow", 30, "escrow_lock", "r4", "Test talebi açıldı: FinBütçe premium konsept (2 slot × 15)");
    }

    // ---- Para defteri (₺) ----
    // Hesaplar: kullanıcı id'leri · "revenue" (platform geliri) · "pool" (ödül havuzu) · "bank".
    private int _cid;
    private int _pid;

    private void C(long ts, string from, string to, decimal amount, string type, string? @ref, string note) =>
        db.CashLedger.Add(new CashLedgerEntry
        {
            Id = "c" + (++_cid), Ts = ts, From = from, To = to,
            Amount = amount, Type = type, Ref = @ref, Note = note
        });

    private record Pkg(string Id, string Name, int Testers, int Tokens);
    private record Plan(string Id, string Name, decimal Price, int Tokens, int Testers);

    private static readonly Pkg[] Packages =
    [
        new("p1", "Başlangıç", 3, 45),
        new("p2", "Büyüme", 10, 150),
        new("p3", "Ölçek", 25, 375)
    ];

    private static readonly Plan[] Plans =
    [
        new("sp1", "Başlangıç", 600m, 60, 4),
        new("sp2", "Büyüme", 1800m, 200, 13)
    ];

    /// <summary>Token paketi satın alımı: token basılır, para komisyon + havuz olarak ikiye ayrılır.</summary>
    private void Buy(long ts, string userId, string pkgId, string userName)
    {
        var pkg = Packages.First(p => p.Id == pkgId);
        var gross = pkg.Tokens * DefaultTokenPrice;
        var fee = Math.Round(gross * DefaultFeePct / 100m, MidpointRounding.AwayFromZero);
        var pool = gross - fee;

        db.Purchases.Add(new Purchase
        {
            Id = "pu" + (++_pid), Ts = ts, UserId = userId, PackageId = pkg.Id, PackageName = pkg.Name,
            Tokens = pkg.Tokens, Testers = pkg.Testers, Gross = gross, Fee = fee, Pool = pool,
            InvoiceNo = "BTK-" + DateTimeOffset.FromUnixTimeMilliseconds(ts).Year + "-" + (1000 + _pid)
        });

        C(ts, userId, "revenue", fee, "commission", pkg.Id,
            $"Platform komisyonu (%{DefaultFeePct}) · {pkg.Name} paketi → {userName}");
        C(ts, userId, "pool", pool, "purchase", pkg.Id,
            $"Testçi ödül havuzuna aktarıldı · {pkg.Name} paketi → {userName}");
        L(ts, "system", userId, pkg.Tokens, "token_purchase", null,
            $"{pkg.Name} paketi satın alındı ({pkg.Tokens} token · {pkg.Testers} testçi)");
    }

    /// <summary>
    /// Abonelik dönemi. Pakettekinden farklı olarak havuz payı önce sabitlenir: basılan
    /// tokenin testçi karşılığı havuza yatar, kalan tutar platform gelirine yazılır — böylece
    /// abonelik indirimi platform marjından karşılanır, testçi yükümlülüğü tam fonlanır.
    /// </summary>
    private void Sub(long ts, string userId, string planId, string userName, List<User> users)
    {
        var plan = Plans.First(p => p.Id == planId);
        var rate = DefaultTokenPrice * (100 - DefaultFeePct) / 100m;
        var pool = Math.Min(plan.Price, Math.Round(plan.Tokens * rate, MidpointRounding.AwayFromZero));
        var fee = plan.Price - pool;

        db.Purchases.Add(new Purchase
        {
            Id = "pu" + (++_pid), Ts = ts, UserId = userId, Kind = "subscription",
            PackageId = planId, PackageName = plan.Name + " aboneliği",
            Tokens = plan.Tokens, Testers = plan.Testers, Gross = plan.Price, Fee = fee, Pool = pool,
            InvoiceNo = "BTK-" + DateTimeOffset.FromUnixTimeMilliseconds(ts).Year + "-" + (1000 + _pid)
        });

        C(ts, userId, "revenue", fee, "subscription_fee", planId,
            $"Abonelik komisyonu · {plan.Name} → {userName}");
        C(ts, userId, "pool", pool, "subscription", planId,
            $"Testçi ödül havuzuna aktarıldı · {plan.Name} aboneliği → {userName}");
        L(ts, "system", userId, plan.Tokens, "subscription_renewal", planId,
            $"{plan.Name} aboneliği yenilendi ({plan.Tokens} token)");

        var u = users.First(x => x.Id == userId);
        u.SubscriptionPlanId = planId;
        u.SubscriptionRenewsAt = ts + 30 * Day;
        u.SubscriptionActive = true;
    }

    private void SeedCash()
    {
        var users = db.Users.Local.ToList();

        Buy(DaysAgo(28), "u1", "p2", "Elif Aydın");
        Buy(DaysAgo(26), "u2", "p1", "Mert Kaya");
        Buy(DaysAgo(23), "u3", "p1", "Zeynep Demir");

        // Mert (StokPro) Büyüme planına abone: iki dönem faturalandı, öncelikli eşleştirmesi var.
        Sub(DaysAgo(35), "u2", "sp2", "Mert Kaya", users);
        Sub(DaysAgo(5), "u2", "sp2", "Mert Kaya", users);
        // Zeynep (MindPuzzle) Başlangıç planında.
        Sub(DaysAgo(12), "u3", "sp1", "Zeynep Demir", users);

        // Onaylanan testlerin parasal karşılığı havuzdan testçiye geçer.
        // (İtibar bonusu tokenlerinin nakit karşılığı yoktur — havuzdan ödeme yapılmaz.)
        var rate = DefaultTokenPrice * (100 - DefaultFeePct) / 100m;
        C(DaysAgo(22), "pool", "t1", 20 * rate, "test_payout", "r93", $"Test ödemesi: StokPro raporlama ekranı (20 token × {rate} ₺)");
        C(DaysAgo(21.5), "pool", "t2", 20 * rate, "test_payout", "r93", $"Test ödemesi: StokPro raporlama ekranı (20 token × {rate} ₺)");
        C(DaysAgo(16), "pool", "t1", 15 * rate, "test_payout", "r94", $"Test ödemesi: MindPuzzle mağaza sayfası (15 token × {rate} ₺)");

        // Ayşe'nin ödenmiş çekimi + Burak'ın onay bekleyen çekim talebi (admin demosu).
        db.Withdrawals.Add(new Withdrawal { Id = "w1", UserId = "t1", Amount = 200m, Status = "paid",
            RequestedAt = DaysAgo(11), ResolvedAt = DaysAgo(10), Iban = "TR** **** **** 8842" });
        C(DaysAgo(10), "t1", "bank", 200m, "withdrawal", "w1", "Çekim ödendi → Ayşe Yıldırım");

        db.Withdrawals.Add(new Withdrawal { Id = "w2", UserId = "t2", Amount = 160m, Status = "pending",
            RequestedAt = DaysAgo(0.3), ResolvedAt = null, Iban = "TR** **** **** 4417" });
    }
}
