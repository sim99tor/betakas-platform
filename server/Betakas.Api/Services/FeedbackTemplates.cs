namespace Betakas.Api.Services;

public record FeedbackField(string Key, string Label, int Min);

public record FeedbackTemplate(string Stage, string IntentLabel, string WhyLabel, FeedbackField[] Fields);

/// <summary>
/// Teslim formunun kalite kapısı. Ürün aşamasına göre hangi alanların doldurulacağını ve
/// her alanın minimum uzunluğunu belirler — istemcideki FEEDBACK_TEMPLATES ile birebir aynı.
/// Doğrulama artık sunucuda yapıldığı için tarayıcıdan kısa/boş feedback geçirilemez.
/// </summary>
public static class FeedbackTemplates
{
    public static readonly string[] Choices = ["evet", "belki", "hayir"];

    private static readonly Dictionary<string, FeedbackTemplate> All = new()
    {
        ["Fikir/Prototip"] = new("Fikir/Prototip", "Para öderdim", "Neden? (fiyat beklentin varsa yaz)",
        [
            new("firstImpression", "İlk İzlenim", 60),
            new("valueProp", "Değer önerisi net mi?", 60),
            new("confusing", "Kafa karıştıran noktalar", 40)
        ]),

        ["MVP"] = new("MVP", "Kullanırdım", "Neden?",
        [
            new("firstImpression", "İlk İzlenim", 60),
            new("bugs", "Bulunan Bug'lar", 30),
            new("ux", "UX Sorunları & Öneriler", 60)
        ]),

        ["Büyüme"] = new("Büyüme", "Devam ederdim", "Neden?",
        [
            new("dropOff", "Nerede vazgeçtin / terk ettin?", 60),
            new("bestFeature", "En değerli özellik", 40),
            new("missingFeature", "Eksik özellik önerisi", 40)
        ])
    };

    /// <summary>Aşama bilinmiyorsa (eski kayıtlar) MVP şablonu kullanılır.</summary>
    public static FeedbackTemplate For(string? stage) =>
        stage != null && All.TryGetValue(stage, out var t) ? t : All["MVP"];
}
