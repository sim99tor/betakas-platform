/**
 * Sunucu köprüsü. Vanilla sürümdeki `api.js` ile aynı sözleşme, TypeScript'e taşınmış hâli.
 *
 * İş kuralları sunucudadır; bu dosya yalnızca HTTP taşımasıdır:
 *   • oturum   → POST /api/auth/login    (kısa ömürlü JWT + yenileme jetonu)
 *   • tazeleme → POST /api/auth/refresh  (jeton döndürme)
 *   • okuma    → GET  /api/state
 *   • eylem    → dar domain uçları; her biri güncel state'i geri döndürür
 *
 * Erişim jetonu 20 dakikada dolar; 401 alınınca yenileme jetonuyla sessizce tazelenir
 * ve istek bir kez otomatik tekrarlanır.
 */

import { ApiError } from "./types";
import type { ActionResponse, AppState, AuthResponse, FixMap } from "./types";

const TOKEN_KEY = "betakas_token";
const REFRESH_KEY = "betakas_refresh";

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string | null): void {
  try {
    if (value) localStorage.setItem(key, value);
    else localStorage.removeItem(key);
  } catch {
    /* gizli sekmede localStorage kapalı olabilir */
  }
}

export const tokens = {
  access: () => read(TOKEN_KEY),
  refresh: () => read(REFRESH_KEY),
  isAuthed: () => !!read(TOKEN_KEY),
  store(auth: AuthResponse) {
    write(TOKEN_KEY, auth.token);
    write(REFRESH_KEY, auth.refreshToken);
    return auth;
  },
  clear() {
    write(TOKEN_KEY, null);
    write(REFRESH_KEY, null);
  },
};

/** Tek seferlik istek — tazeleme denemeden. */
async function raw<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  const access = tokens.access();
  if (access) headers.Authorization = `Bearer ${access}`;
  if (body !== undefined) headers["Content-Type"] = "application/json";

  const res = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (res.status === 204) return null as T;

  const text = await res.text();
  let data: unknown = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = { error: text };
    }
  }

  if (res.ok) return data as T;

  const payload = (data ?? {}) as Record<string, unknown>;
  throw new ApiError(
    (payload.error as string) ?? `HTTP ${res.status}`,
    res.status,
    payload,
  );
}

// Aynı anda birden çok istek 401 alırsa tek bir tazeleme yapılsın diye paylaşılan söz.
let refreshing: Promise<AuthResponse> | null = null;

function doRefresh(): Promise<AuthResponse> {
  if (refreshing) return refreshing;

  const refreshToken = tokens.refresh();
  if (!refreshToken) return Promise.reject(new ApiError("Oturum yok.", 401));

  refreshing = raw<AuthResponse>("POST", "/api/auth/refresh", { refreshToken })
    .then((res) => {
      refreshing = null;
      return tokens.store(res);
    })
    .catch((err) => {
      refreshing = null;
      tokens.clear();
      throw err;
    });

  return refreshing;
}

/** 401 alırsa bir kez tazeleyip tekrar dener. */
async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  try {
    return await raw<T>(method, path, body);
  } catch (err) {
    if (err instanceof ApiError && err.status === 401 && tokens.refresh()) {
      await doRefresh();
      return raw<T>(method, path, body);
    }
    throw err;
  }
}

// --- Eylem yükleri ---

export interface CreateRequestPayload {
  title: string;
  versionId: string;
  productCategory: string;
  stage: string;
  feedbackType: string;
  scenario: string;
  credits: number;
  slots: number;
  excludeSector: boolean;
}

export interface CreateVersionPayload {
  label: string;
  url: string;
  notes: string;
  fixes: FixMap;
}

export interface SubmitFeedbackPayload {
  fields: Record<string, string>;
  wouldUse: string;
  wouldUseWhy: string;
  proofUrl: string;
  durationMin: number;
}

export interface RegisterPayload {
  name: string;
  email: string;
  password: string;
  role: "founder" | "tester";
  org: string;
  tagline: string;
  sector?: string | null;
  expertiseCategories: string[];
  expertiseOther?: string | null;
}

export const api = {
  // --- Oturum ---
  login: (email: string, password: string, role: string) =>
    raw<AuthResponse>("POST", "/api/auth/login", { email, password, role }).then(tokens.store),

  register: (payload: RegisterPayload) =>
    raw<{ ok: boolean; message: string }>("POST", "/api/auth/register", payload),

  async logout() {
    const refreshToken = tokens.refresh();
    tokens.clear();
    if (!refreshToken) return;
    // Sunucudaki jetonu da iptal et; hata olsa da oturum yerelde kapanmış olur.
    try {
      await raw("POST", "/api/auth/logout", { refreshToken });
    } catch {
      /* yok say */
    }
  },

  // --- Okuma ---
  getState: () => request<AppState>("GET", "/api/state"),
  getPublicState: () => raw<AppState>("GET", "/api/public/state"),
  getRev: () => request<{ rev: number }>("GET", "/api/state/rev"),
  reset: () => request<{ ok: boolean }>("POST", "/api/admin/reset"),

  // --- Talepler ---
  createRequest: (p: CreateRequestPayload) =>
    request<ActionResponse>("POST", "/api/requests", p),
  boostRequest: (id: string) =>
    request<ActionResponse>("POST", `/api/requests/${id}/boost`),
  closeRequest: (id: string) =>
    request<ActionResponse>("POST", `/api/requests/${id}/close`),

  // --- Ürün sürümleri ---
  createVersion: (p: CreateVersionPayload) =>
    request<ActionResponse>("POST", "/api/versions", p),
  saveFixes: (id: string, fixes: FixMap) =>
    request<ActionResponse>("PUT", `/api/versions/${id}/fixes`, { fixes }),

  // --- Oturumlar: testçi ---
  apply: (requestId: string) =>
    request<ActionResponse>("POST", `/api/requests/${requestId}/apply`),
  submitFeedback: (id: string, p: SubmitFeedbackPayload) =>
    request<ActionResponse>("POST", `/api/sessions/${id}/submit`, p),
  rateOwner: (id: string, rating: number) =>
    request<ActionResponse>("POST", `/api/sessions/${id}/rate-owner`, { rating }),

  // --- Oturumlar: kurucu ---
  approveApp: (id: string) =>
    request<ActionResponse>("POST", `/api/sessions/${id}/approve`),
  rejectApp: (id: string) =>
    request<ActionResponse>("POST", `/api/sessions/${id}/reject`),
  acceptFeedback: (id: string, rating: number) =>
    request<ActionResponse>("POST", `/api/sessions/${id}/accept`, { rating }),
  disputeFeedback: (id: string, note: string) =>
    request<ActionResponse>("POST", `/api/sessions/${id}/dispute`, { note }),

  // --- Faturalama ---
  buyPackage: (packageId: string, card: string) =>
    request<ActionResponse>("POST", "/api/billing/buy", { packageId, card }),
  subscribe: (planId: string, card: string) =>
    request<ActionResponse>("POST", "/api/billing/subscribe", { planId, card }),
  cancelSubscription: () =>
    request<ActionResponse>("POST", "/api/billing/cancel"),
  renewSubscription: () =>
    request<ActionResponse>("POST", "/api/billing/renew"),

  // --- Çekimler ---
  requestWithdrawal: (amount: number, iban: string) =>
    request<ActionResponse>("POST", "/api/withdrawals", { amount, iban }),
  resolveWithdrawal: (id: string, outcome: string, note: string | null) =>
    request<ActionResponse>("POST", `/api/withdrawals/${id}/resolve`, { outcome, note }),

  // --- Profil ---
  saveExpertise: (categories: string[], other: string | null) =>
    request<ActionResponse>("PUT", "/api/me/expertise", { categories, other }),

  // --- Yönetim ---
  resolveDispute: (id: string, outcome: "release" | "refund") =>
    request<ActionResponse>("POST", `/api/sessions/${id}/resolve-dispute`, { outcome }),
  approveUser: (id: string) =>
    request<ActionResponse>("POST", `/api/admin/users/${id}/approve`),
  rejectUser: (id: string) =>
    request<ActionResponse>("POST", `/api/admin/users/${id}/reject`),
  setFeePct: (value: number) =>
    request<ActionResponse>("PUT", "/api/admin/settings/fee", { value }),
  setTokenPrice: (value: number) =>
    request<ActionResponse>("PUT", "/api/admin/settings/token-price", { value }),
};
