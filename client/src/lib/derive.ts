/**
 * State'ten türetilen salt-okunur hesaplar. Hiçbiri veriyi değiştirmez —
 * her biri `state`'i alıp sonuç döndürür.
 *
 * Bakiyeler asla bir alanda tutulmaz, her zaman defterden türetilir; bu, vanilla
 * sürümdeki davranışın aynısıdır ve sunucudaki LedgerService ile aynı matematiği kullanır.
 */

import {
  DEFAULT_STAGE, MIN_WITHDRAWAL, OTHER_CATEGORY, PRODUCT_STAGES,
  SUBSCRIPTION_PLANS, TOKEN_PACKAGES,
} from "./constants";
import type { SubscriptionPlan, TokenPackage } from "./constants";
import type { AppState, ProductVersion, TestRequest, User } from "./types";

// ---------------- Aramalar ----------------

export const getUser = (s: AppState, id: string | null | undefined) =>
  id ? s.users.find((u) => u.id === id) : undefined;

export const getRequest = (s: AppState, id: string | null | undefined) =>
  id ? s.requests.find((r) => r.id === id) : undefined;

export const getSession = (s: AppState, id: string | null | undefined) =>
  id ? s.sessions.find((x) => x.id === id) : undefined;

export const getVersion = (s: AppState, id: string | null | undefined) =>
  id ? s.versions.find((v) => v.id === id) : undefined;

export const currentUser = (s: AppState) => getUser(s, s.authUserId);

// ---------------- Ekonomi ----------------

export const tokenPrice = (s: AppState) => s.settings.tokenPrice;
export const feePct = (s: AppState) => s.settings.feePct;

/** Testçiye ödenen ₺/token — komisyon düşüldükten sonra kalan. */
export const payoutRate = (s: AppState) =>
  Math.round(tokenPrice(s) * (100 - feePct(s))) / 100;

export const packagePrice = (s: AppState, pkg: TokenPackage) => pkg.tokens * tokenPrice(s);

export const getPlan = (id: string | null | undefined) =>
  SUBSCRIPTION_PLANS.find((p) => p.id === id);

export const getPackage = (id: string) => TOKEN_PACKAGES.find((p) => p.id === id);

/**
 * Abonelikte havuz payı önce sabitlenir: basılan tokenin testçi karşılığı havuza
 * yatar, kalan platform gelirine yazılır (indirim marjdan karşılanır).
 */
export function planSplit(s: AppState, plan: SubscriptionPlan) {
  const pool = Math.min(plan.price, Math.round(plan.tokens * payoutRate(s)));
  return { gross: plan.price, fee: plan.price - pool, pool };
}

export function subscriptionOf(user: User | undefined) {
  const sub = user?.subscription;
  if (!sub?.planId || !sub.active) return null;
  const plan = getPlan(sub.planId);
  return plan ? { plan, renewsAt: sub.renewsAt, active: sub.active } : null;
}

export const hasMatchPriority = (s: AppState, userId: string) =>
  subscriptionOf(getUser(s, userId))?.plan.priority ?? false;

export const activeSubscribers = (s: AppState) =>
  s.users.filter((u) => subscriptionOf(u) !== null);

/** Aylık yinelenen gelir. */
export const mrr = (s: AppState) =>
  activeSubscribers(s).reduce((sum, u) => sum + (subscriptionOf(u)?.plan.price ?? 0), 0);

// ---------------- Defterler ----------------

export function balance(s: AppState, account: string): number {
  let total = 0;
  for (const e of s.ledger) {
    if (e.to === account) total += e.amount;
    if (e.from === account) total -= e.amount;
  }
  return total;
}

export function cashBalance(s: AppState, account: string): number {
  let total = 0;
  for (const e of s.cashLedger) {
    if (e.to === account) total += e.amount;
    if (e.from === account) total -= e.amount;
  }
  return total;
}

/** Testçinin çekilebilir bakiyesi: kazandığı − çektiği − onay bekleyen talepler. */
export function withdrawable(s: AppState, userId: string): number {
  const pending = s.withdrawals
    .filter((w) => w.userId === userId && w.status === "pending")
    .reduce((a, w) => a + w.amount, 0);
  return cashBalance(s, userId) - pending;
}

export const canWithdraw = (s: AppState, userId: string) =>
  withdrawable(s, userId) >= MIN_WITHDRAWAL;

/** Bir talep için escrow'da hâlâ kilitli token. */
export function escrowRemaining(s: AppState, requestId: string): number {
  let remaining = 0;
  for (const e of s.ledger) {
    if (e.ref !== requestId) continue;
    if (e.to === "escrow") remaining += e.amount;
    if (e.from === "escrow") remaining -= e.amount;
  }
  return remaining;
}

/** Kullanıcının açık taleplerinde bloke duran toplam token. */
export function blockedFor(s: AppState, userId: string): number {
  return s.requests
    .filter((r) => r.ownerId === userId)
    .reduce((sum, r) => sum + escrowRemaining(s, r.id), 0);
}

export function tokenEconomy(s: AppState) {
  const sold = s.purchases.reduce((a, p) => a + p.tokens, 0);
  const granted = s.ledger
    .filter((e) => e.from === "system" && (e.type === "bonus" || e.type === "rep_bonus"))
    .reduce((a, e) => a + e.amount, 0);
  const gross = s.purchases.reduce((a, p) => a + p.gross, 0);
  const revenue = cashBalance(s, "revenue");

  return {
    sold,
    granted,
    gross,
    revenue,
    pool: cashBalance(s, "pool"),
    paidOut: cashBalance(s, "bank"),
    pendingWithdrawals: s.withdrawals
      .filter((w) => w.status === "pending")
      .reduce((a, w) => a + w.amount, 0),
    // Karşılığı gerçekten ödenmiş token oranı (promosyon tokenleri nakde çevrilmez).
    backedPct: sold + granted ? Math.round((sold / (sold + granted)) * 100) : 0,
    effectiveFeePct: gross ? Math.round((revenue / gross) * 1000) / 10 : feePct(s),
  };
}

// ---------------- İtibar ----------------

export interface Reputation {
  avg: number;
  count: number;
  completed: number;
  penalties: number;
}

/** Kaybedilen itiraz ortalamayı (ve dolayısıyla token çarpanını) düşürür. */
export function reputation(s: AppState, userId: string): Reputation {
  let sum = 0, count = 0, completed = 0, penalties = 0;

  for (const session of s.sessions) {
    if (session.testerId !== userId) continue;
    if (session.status === "accepted" && session.rating) {
      sum += session.rating; count++; completed++;
    } else if (session.status === "rejected" && session.disputeOutcome === "refund") {
      sum += 1; count++; penalties++;
    }
  }

  return { avg: count ? sum / count : 0, count, completed, penalties };
}

/** Talep sahibi olarak alınan puan: testçiler süreci nasıl değerlendirdi. */
export function founderRating(s: AppState, userId: string) {
  let sum = 0, count = 0;
  for (const session of s.sessions) {
    if (session.status !== "accepted" || !session.ownerRating) continue;
    const request = getRequest(s, session.requestId);
    if (request?.ownerId === userId) { sum += session.ownerRating; count++; }
  }
  return { avg: count ? sum / count : 0, count };
}

export function repMultiplier(s: AppState, userId: string): number {
  const r = reputation(s, userId);
  if (r.count >= 2 && r.avg >= 4.5) return 1.2;
  if (r.count >= 1 && r.avg >= 4.0) return 1.1;
  return 1.0;
}

export interface RankEntry {
  user: User;
  rep: Reputation;
  score: number;
  position: number;
  total: number;
  pct: number;
}

/**
 * Sıralama anahtarı: ortalama puan baskın, eşitliği tamamlanan test sayısı bozar,
 * cezalar negatif etki eder. Eşit puanlılar aynı sırayı paylaşır.
 */
export function testerRanking(s: AppState): RankEntry[] {
  const scored = s.users
    .filter((u) => u.status === "active" && u.role !== "admin" && reputation(s, u.id).completed > 0)
    .map((user) => {
      const rep = reputation(s, user.id);
      return { user, rep, score: rep.avg * 100 + rep.completed - rep.penalties * 5 };
    })
    .sort((a, b) => b.score - a.score);

  let position = 1;
  return scored.map((entry, i) => {
    if (i > 0 && entry.score < scored[i - 1].score) position = i + 1;
    return {
      ...entry,
      position,
      total: scored.length,
      pct: Math.max(1, Math.ceil((position / scored.length) * 100)),
    };
  });
}

export const rankOf = (s: AppState, userId: string) =>
  testerRanking(s).find((x) => x.user.id === userId) ?? null;

/**
 * Yüzdelik dilim tek başına küçük havuzlarda ayırt etmediği için sıra koşulu ve
 * asgari puan eşiği birlikte aranır.
 */
export function reputationBadge(rank: RankEntry | null) {
  if (!rank) return null;
  const avg = rank.rep.avg;
  if ((rank.pct <= 10 || (rank.position === 1 && rank.total >= 3)) && avg >= 4.5)
    return { label: "Top Testçi", cls: "credit" };
  if ((rank.pct <= 25 || (rank.position <= 3 && rank.total >= 3)) && avg >= 4.0)
    return { label: "Güvenilir Testçi", cls: "violet" };
  return { label: "Aktif Testçi", cls: "cat" };
}

export function testerLevel(s: AppState, userId: string) {
  const done = reputation(s, userId).completed;
  if (done >= 10) return { name: "Usta Testçi", next: null, done };
  if (done >= 5) return { name: "Uzman Testçi", next: 10, done };
  if (done >= 2) return { name: "Deneyimli Testçi", next: 5, done };
  return { name: "Çaylak Testçi", next: 2, done };
}

// ---------------- Kazanç ----------------

/** Escrow'da onay bekleyen (henüz serbest kalmamış) token. */
export function pendingEarnings(s: AppState, userId: string): number {
  return s.sessions
    .filter((x) => x.testerId === userId && (x.status === "submitted" || x.status === "disputed"))
    .reduce((sum, x) => sum + (getRequest(s, x.requestId)?.credits ?? 0), 0);
}

export function totalEarned(s: AppState, userId: string): number {
  return s.ledger
    .filter((e) => e.to === userId && (e.type === "escrow_release" || e.type === "rep_bonus"))
    .reduce((a, e) => a + e.amount, 0);
}

// ---------------- Talepler ----------------

export const requestSessions = (s: AppState, requestId: string) =>
  s.sessions.filter((x) => x.requestId === requestId);

/** Reddedilmemiş her oturum bir slot tutar. */
export const slotsTaken = (s: AppState, requestId: string) =>
  requestSessions(s, requestId).filter((x) => x.status !== "rejected").length;

export const slotsLeft = (s: AppState, r: TestRequest) => r.slots - slotsTaken(s, r.id);

/** Eski kayıtlarda alan yoksa "Diğer" sayılır. */
export const productCategoryOf = (r: TestRequest | undefined) =>
  r?.productCategory || OTHER_CATEGORY;

/** Eski kayıtlarda aşama yoksa MVP sayılır. */
export const stageOf = (r: TestRequest | undefined) =>
  r?.stage && PRODUCT_STAGES.includes(r.stage) ? r.stage : DEFAULT_STAGE;

export const expertiseOf = (u: User | undefined) => u?.expertiseCategories ?? [];

export const categoryMatch = (u: User | undefined, r: TestRequest) =>
  expertiseOf(u).includes(productCategoryOf(r));

export function expertiseLabels(u: User | undefined): string[] {
  const list = expertiseOf(u);
  const other = u?.expertiseOther?.trim();
  return list.map((c) => (c === OTHER_CATEGORY && other ? `${OTHER_CATEGORY}: ${other}` : c));
}

/** Talebin testçinin serbest etiketleriyle örtüşen kısmı (ikincil sinyal). */
export function skillMatches(u: User | undefined, r: TestRequest): string[] {
  const hay = `${productCategoryOf(r)} ${r.feedbackType ?? ""} ${r.title}`.toLocaleLowerCase("tr-TR");
  return (u?.skills ?? []).filter((sk) => hay.includes(sk.toLocaleLowerCase("tr-TR")));
}

// ---------------- Sürümler ----------------

export const versionsOf = (s: AppState, ownerId: string) =>
  s.versions.filter((v) => v.ownerId === ownerId).sort((a, b) => a.createdAt - b.createdAt);

export const versionRequests = (s: AppState, versionId: string) =>
  s.requests.filter((r) => r.versionId === versionId);

export function versionStatus(s: AppState, versionId: string) {
  const requests = versionRequests(s, versionId);
  if (!requests.length) return { label: "Teste sokulmadı", cls: "muted" };
  if (requests.some((r) => r.status === "open")) return { label: "Testte", cls: "credit" };
  return { label: "Test tamamlandı", cls: "ok" };
}

/** Sonraki yama ve sonraki ana sürüm etiketi ("v1.1" → v1.2 / v2.0). */
export function nextVersionLabels(label: string) {
  const m = /^v?(\d+)(?:\.(\d+))?/i.exec(label || "");
  if (!m) return { patch: "", major: "" };
  const major = parseInt(m[1], 10);
  const minor = m[2] === undefined ? 0 : parseInt(m[2], 10);
  return { patch: `v${major}.${minor + 1}`, major: `v${major + 1}.0` };
}

/** Testçi bu ürünün daha önceki bir sürümünü test etmiş mi (regresyon rozeti). */
export function testedEarlierVersions(s: AppState, userId: string, versionId: string): ProductVersion[] {
  const version = getVersion(s, versionId);
  if (!version) return [];

  return versionsOf(s, version.ownerId)
    .filter((other) => other.createdAt < version.createdAt)
    .filter((other) => {
      const ids = versionRequests(s, other.id).map((r) => r.id);
      return s.sessions.some(
        (x) => x.testerId === userId && x.status === "accepted" && ids.includes(x.requestId),
      );
    });
}

// ---------------- Biçimlendirme ----------------

export const subtitleOf = (u: User | undefined) =>
  (u?.role === "tester" ? u.title : u?.startup) ?? "";

export function acctName(s: AppState, account: string): string {
  const virtual: Record<string, string> = {
    system: "Sistem",
    escrow: "Escrow (bloke)",
    revenue: "Platform Geliri",
    pool: "Testçi Ödül Havuzu",
    bank: "Banka (Dış Ödeme)",
  };
  return virtual[account] ?? getUser(s, account)?.name ?? account;
}

export function fmtTRY(n: number | null | undefined): string {
  const value = Math.round((n ?? 0) * 100) / 100;
  return `₺${value.toLocaleString("tr-TR", { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;
}

export function fmtDate(ts: number | null | undefined): string {
  if (!ts) return "—";
  return new Date(ts).toLocaleString("tr-TR", {
    day: "numeric", month: "short", hour: "2-digit", minute: "2-digit",
  });
}

export function timeAgo(ts: number | null | undefined): string {
  if (!ts) return "—";
  const diff = Date.now() - ts;
  const mins = Math.round(diff / 60000);
  if (mins < 1) return "az önce";
  if (mins < 60) return `${mins} dk önce`;
  const hours = Math.round(mins / 60);
  if (hours < 24) return `${hours} saat önce`;
  return `${Math.round(hours / 24)} gün önce`;
}

export const starStr = (n: number) => "★".repeat(Math.round(n)) + "☆".repeat(Math.max(0, 5 - Math.round(n)));

/** Avatar rengini id'den türetir ki aynı kişi hep aynı renkte görünsün. */
export function avatarCls(id: string): string {
  const palette = ["a", "b", "c", "d", "e", "f"];
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return palette[hash % palette.length];
}

export const sessionsOfTester = (s: AppState, userId: string) =>
  s.sessions.filter((x) => x.testerId === userId);
