/**
 * Testçi metinlerinden numaralı bug listesi ve sürüm notu tikleri.
 * Sunucudaki BugExtractor.cs ile aynı kuralları uygular — tik anahtarları
 * (`<oturumId>:<sıra>`) iki tarafta birebir aynı üretilmelidir.
 */

import { getUser, versionRequests, versionsOf } from "./derive";
import type { AppState, FixMap, FixState, ProductVersion } from "./types";

/** "Bug görmedim" tarzı cümleler madde sayılmaz. */
const NO_BUG_RE =
  /^(belirgin\s+)?(bir\s+)?(bug|hata|sorun|problem)\S*\s*(yok|görmedim|görmüyorum|yaşamadım|çıkmadı|rastlamadım)/i;

const CRITICAL_RE = /kritik|çöküyor|donuyor|donduruyor|veri kayb|çöktü/i;

export interface BugItem {
  id: string;
  no: string;
  text: string;
  reporter: string;
  critical: boolean;
}

/**
 * Serbest metni maddelere böler. "1) … 2) …" kalıbında yalnızca 1'den başlayıp
 * sırayla artan numaralar bölme noktası sayılır — metnin içindeki "3. bölümde"
 * gibi ifadeler madde sanılmaz.
 */
export function splitBugItems(text: string | null | undefined): string[] {
  const t = (text ?? "").trim();
  if (!t) return [];

  const marks: { cut: number; start: number }[] = [];
  let want = 1;
  const re = /(?:^|[\s;])(\d{1,2})\s*[).]\s+/g;
  let m: RegExpExecArray | null;

  while ((m = re.exec(t)) !== null) {
    if (parseInt(m[1], 10) !== want) continue;
    marks.push({ cut: m.index, start: m.index + m[0].length });
    want++;
  }

  let parts: string[];
  if (marks.length) {
    parts = marks.map((mk, i) =>
      t.slice(mk.start, i + 1 < marks.length ? marks[i + 1].cut : t.length));
  } else if (/(?:^|\n)\s*[-•*]\s+/.test(t)) {
    parts = t.split(/(?:^|\n)\s*[-•*]\s+/);
  } else {
    parts = t.split(/\n+/);
  }

  return parts
    .map((p) => p.trim().replace(/^[,;]\s*/, ""))
    .filter((p) => p.length >= 8 && !NO_BUG_RE.test(p));
}

const pad2 = (n: number) => (n < 10 ? `0${n}` : String(n));

/** Bir sürüme gelen onaylı testlerden numaralanmış bug listesi. */
export function versionBugs(s: AppState, versionId: string): BugItem[] {
  const requestIds = versionRequests(s, versionId).map((r) => r.id);
  if (!requestIds.length) return [];

  const sessions = s.sessions
    .filter((x) => x.status === "accepted" && x.feedback && requestIds.includes(x.requestId))
    .sort((a, b) => (a.submittedAt ?? 0) - (b.submittedAt ?? 0));

  const out: BugItem[] = [];
  for (const session of sessions) {
    const reporter = getUser(s, session.testerId)?.name ?? "Testçi";
    splitBugItems(session.feedback?.bugs).forEach((text, i) => {
      out.push({
        id: `${session.id}:${i}`,
        no: pad2(out.length + 1),
        text,
        reporter,
        critical: CRITICAL_RE.test(text),
      });
    });
  }
  return out;
}

export const fixesOf = (v: ProductVersion | undefined): FixMap => v?.fixes ?? {};

/**
 * Bir sürümün yanıt verdiği bug kaynağı: kendisinden önceki, bug BİLDİRİLMİŞ en yeni
 * sürüm. Araya teste hiç sokulmamış bir sürüm girse bile açık maddeler kaybolmasın diye
 * doğrudan bir öncekine değil, geriye doğru bakılır.
 */
export function bugSourceFor(s: AppState, version: ProductVersion): ProductVersion | null {
  const siblings = versionsOf(s, version.ownerId);
  const idx = siblings.findIndex((v) => v.id === version.id);
  for (let i = idx - 1; i >= 0; i--) {
    if (versionBugs(s, siblings[i].id).length) return siblings[i];
  }
  return null;
}

/**
 * Kaynak sürümden itibaren `upTo` dahil tüm sürümlerin işaretlerini üst üste bindirir:
 * sonraki sürümün işareti öncekini ezer, dokunulmamışsa eski durum korunur. Böylece
 * "v1.1'de düzelttim" bilgisi v1.2 notunda da görünmeye devam eder.
 */
export function accumulatedFixes(
  s: AppState, ownerId: string, source: ProductVersion, upTo?: ProductVersion,
): FixMap {
  const versions = versionsOf(s, ownerId);
  const from = versions.findIndex((v) => v.id === source.id) + 1;
  const to = upTo ? versions.findIndex((v) => v.id === upTo.id) + 1 : versions.length;

  const state: FixMap = {};
  for (let i = from; i < to; i++) {
    Object.assign(state, fixesOf(versions[i]));
  }
  return state;
}

export interface ChangelogRow {
  bug: BugItem;
  state: FixState | null;
}

export interface Changelog {
  source: ProductVersion;
  rows: ChangelogRow[];
  fixed: number;
  total: number;
  pct: number;
}

/** Sürüm karnesindeki "3/5 bug düzeltildi" tablosu. */
export function changelogOf(s: AppState, version: ProductVersion): Changelog | null {
  const source = bugSourceFor(s, version);
  if (!source) return null;

  const bugs = versionBugs(s, source.id);
  if (!bugs.length) return null;

  const state = accumulatedFixes(s, version.ownerId, source, version);
  const rows: ChangelogRow[] = bugs.map((bug) => ({ bug, state: state[bug.id] ?? null }));
  const fixed = rows.filter((r) => r.state === "fixed").length;

  return {
    source,
    rows,
    fixed,
    total: rows.length,
    pct: rows.length ? Math.round((fixed / rows.length) * 100) : 0,
  };
}

/** Yeni sürüm formunda karşıya çıkacak, hâlâ açık bug'lar. */
export function pendingBugs(s: AppState, ownerId: string): { source: ProductVersion; rows: ChangelogRow[] } | null {
  const versions = versionsOf(s, ownerId);
  if (!versions.length) return null;

  const latest = versions[versions.length - 1];
  const source = versionBugs(s, latest.id).length ? latest : bugSourceFor(s, latest);
  if (!source) return null;

  const bugs = versionBugs(s, source.id);
  if (!bugs.length) return null;

  const applied = accumulatedFixes(s, ownerId, source);
  return {
    source,
    rows: bugs.map((bug) => ({ bug, state: applied[bug.id] ?? null })),
  };
}

/** Kapanmış sayılan işaretler: madde açık listeden çıkar. */
export const isClosed = (state: FixState | null) => state === "fixed" || state === "norepro";
