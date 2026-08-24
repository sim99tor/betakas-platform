/**
 * Görüntüleme sabitleri. Bunların iş kuralı karşılığı sunucudadır
 * (EconomyService, FeedbackTemplates, RequestActions); buradaki kopyalar
 * yalnızca ekranları çizmek ve anında geri bildirim vermek içindir.
 */

import type { FixState, Role } from "./types";

export const DEMO_PASSWORD = "betakas";

export const SECTORS = [
  "Fintek", "B2B SaaS", "Mobil Oyun", "E-ticaret", "Sağlık", "Yapay Zeka", "Diğer",
];

export const PRODUCT_CATEGORIES = [
  "SaaS / B2B", "Mobil Uygulama", "E-ticaret", "AI Aracı",
  "Tüketici Uygulaması (B2C)", "Diğer",
];

export const OTHER_CATEGORY = "Diğer";

export const FEEDBACK_TYPES = [
  "Bug Avı", "UX / Kullanılabilirlik", "İlk İzlenim", "Onboarding Akışı",
];

export const PRODUCT_STAGES = ["Fikir/Prototip", "MVP", "Büyüme"];
export const DEFAULT_STAGE = "MVP";

export const BOOST_COST = 10;
export const MIN_WITHDRAWAL = 150;
export const TOKENS_PER_TEST = 15;
export const MONTH_MS = 30 * 24 * 60 * 60 * 1000;

export const ROLE_LABEL: Record<Role, string> = {
  founder: "Kurucu",
  tester: "Testçi",
  admin: "Yönetim",
};

export interface TokenPackage {
  id: string;
  name: string;
  testers: number;
  tokens: number;
  popular?: boolean;
  perks: string[];
}

export const TOKEN_PACKAGES: TokenPackage[] = [
  { id: "p1", name: "Başlangıç", testers: 3, tokens: 45,
    perks: ["Tek üründe hızlı doğrulama", "Sentez raporu"] },
  { id: "p2", name: "Büyüme", testers: 10, tokens: 150, popular: true,
    perks: ["1 ücretsiz öne çıkarma (10 token)", "Öncelikli testçi eşleşmesi", "Sentez raporu + CSV"] },
  { id: "p3", name: "Ölçek", testers: 25, tokens: 375,
    perks: ["3 ücretsiz öne çıkarma (30 token)", "Öncelikli testçi eşleşmesi", "Sprint başına danışmanlık görüşmesi"] },
];

export interface SubscriptionPlan {
  id: string;
  name: string;
  price: number;
  tokens: number;
  testers: number;
  priority: boolean;
  popular?: boolean;
  perks: string[];
}

export const SUBSCRIPTION_PLANS: SubscriptionPlan[] = [
  { id: "sp1", name: "Başlangıç", price: 600, tokens: 60, testers: 4, priority: false,
    perks: ["Her ay 60 token (~4 testçi)", "Sentez raporu", "İstediğin ay iptal"] },
  { id: "sp2", name: "Büyüme", price: 1800, tokens: 200, testers: 13, priority: true, popular: true,
    perks: ["Her ay 200 token (~13 testçi)", "Öncelikli testçi eşleştirmesi", "Sentez raporu + CSV", "İstediğin ay iptal"] },
];

// --- Teslim formu şablonları (aşamaya göre) ---

export interface TemplateField {
  key: string;
  label: string;
  min: number;
  placeholder: string;
}

export interface FeedbackTemplate {
  hint: string;
  linkHint: string;
  intentLabel: string;
  fields: TemplateField[];
  choice: {
    label: string;
    options: { v: string; l: string }[];
    whyLabel: string;
  };
}

const CHOICES = (a: string, b: string, c: string) => [
  { v: "evet", l: a }, { v: "belki", l: b }, { v: "hayir", l: c },
];

export const FEEDBACK_TEMPLATES: Record<string, FeedbackTemplate> = {
  "Fikir/Prototip": {
    hint: "Çalışan ürün yok: Figma/mockup üzerinden değer önerisi ve anlaşılırlık test edilir.",
    linkHint: "Sürüm linki bir Figma / mockup / tanıtım sayfası olabilir.",
    intentLabel: "Para öderdim",
    fields: [
      { key: "firstImpression", label: "İlk İzlenim", min: 60,
        placeholder: "Ekranı ilk gördüğünde ne anladın, ürün sana ne vaat ediyor gibi göründü?" },
      { key: "valueProp", label: "Değer önerisi net mi?", min: 60,
        placeholder: "Bu ürün kimin hangi problemini çözüyor? Bunu ekrandan anlayabildin mi?" },
      { key: "confusing", label: "Kafa karıştıran noktalar", min: 40,
        placeholder: "Hangi başlık, buton ya da akış belirsizdi? Numaralandırarak yaz: 1) ... 2) ..." },
    ],
    choice: {
      label: "Bu ürün için para öder miydin?",
      options: CHOICES("Evet, öderdim", "Belki", "Hayır"),
      whyLabel: "Neden? (fiyat beklentin varsa yaz)",
    },
  },
  MVP: {
    hint: "Çalışan ürün: bug avı ve kullanılabilirlik odaklı klasik test formu.",
    linkHint: "Sürüm linki çalışan ürünün adresi olmalı.",
    intentLabel: "Kullanırdım",
    fields: [
      { key: "firstImpression", label: "İlk İzlenim", min: 60,
        placeholder: "Ürünü ilk açtığında ne hissettin, ne anladın?" },
      { key: "bugs", label: "Bulunan Bug'lar", min: 30,
        placeholder: "Numaralandırarak yaz: 1) ... 2) ..." },
      { key: "ux", label: "UX Sorunları & Öneriler", min: 60,
        placeholder: "Nerede takıldın, ne kafa karıştırıcıydı, ne önerirsin?" },
    ],
    choice: {
      label: "Bu ürünü kullanır mıydın?",
      options: CHOICES("Evet, kullanırdım", "Belki", "Hayır"),
      whyLabel: "Neden?",
    },
  },
  "Büyüme": {
    hint: "Yerleşmiş ürün: dönüşüm ve elde tutma odaklı — nerede terk ediliyor, ne eksik?",
    linkHint: "Sürüm linki canlı ürünün adresi olmalı.",
    intentLabel: "Devam ederdim",
    fields: [
      { key: "dropOff", label: "Nerede vazgeçtin / terk ettin?", min: 60,
        placeholder: "Hangi adımda durdun, çıkmak istedin ya da sıkıldın?" },
      { key: "bestFeature", label: "En değerli özellik", min: 40,
        placeholder: "Geri dönmeni sağlayacak tek şey ne olurdu?" },
      { key: "missingFeature", label: "Eksik özellik önerisi", min: 40,
        placeholder: "Olsaydı kullanmaya devam ederdim dediğin şey ne?" },
    ],
    choice: {
      label: "Kullanmaya devam eder miydin?",
      options: CHOICES("Evet, devam ederdim", "Belki", "Hayır"),
      whyLabel: "Neden?",
    },
  },
};

export const STAGE_SCENARIOS: Record<string, string> = {
  "Fikir/Prototip":
    "Bu bir prototip — tıklanmayan yerler olabilir, sorun değil. Ekranları sırayla gez ve şunları yaz: " +
    "ürün sence kime, hangi problemi çözmek için yapılmış? Hangi ekran ya da ifade kafanı karıştırdı? " +
    "Böyle bir ürün için para öder miydin, öderdin de ne kadar?",
  "Büyüme":
    "Ürünü belirli bir görevi tamamlamak için kullan. Sıkıldığın, durakladığın ya da kapatmak istediğin anı " +
    "tam olarak not et. Sonra şunları yaz: hangi özellik seni geri getirir, hangi eksik özellik yüzünden bırakırdın?",
};

// --- Sürüm notu tikleri ---

export const FIX_STATES: Record<FixState, { icon: string; label: string }> = {
  fixed: { icon: "✅", label: "Düzeltildi" },
  wip: { icon: "🔄", label: "Devam ediyor" },
  later: { icon: "⏳", label: "Sonraki sürüme" },
  norepro: { icon: "❌", label: "Tekrarlanamadı" },
};

export const FIX_ORDER: FixState[] = ["fixed", "wip", "later", "norepro"];

export const STATUS_LABEL: Record<string, string> = {
  applied: "Başvuru bekliyor",
  approved: "Onaylandı — test edilebilir",
  submitted: "Teslim edildi — onay bekliyor",
  accepted: "Kabul edildi",
  disputed: "İtiraz edildi",
  rejected: "Reddedildi",
};
