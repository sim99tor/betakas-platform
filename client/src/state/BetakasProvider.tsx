/**
 * Uygulama durumu tek yerden yönetilir.
 *
 * İş kuralları sunucudadır; burada state YAZILMAZ. Her eylem bir domain ucuna gider,
 * sunucu kuralı doğrular ve güncel state'i döndürür — biz de onu yerleştiririz.
 * Bu yüzden optimistic update yoktur: ekranda gördüğün her zaman sunucunun gerçeğidir.
 */

import {
  createContext, useCallback, useContext, useEffect, useMemo, useRef, useState,
} from "react";
import type { ReactNode } from "react";
import { api, tokens } from "../lib/api";
import { currentUser } from "../lib/derive";
import { ApiError } from "../lib/types";
import type { ActionResponse, AppState, User } from "../lib/types";

export type ToastKind = "good" | "warn" | "bad";

export interface Toast {
  id: number;
  message: string;
  kind: ToastKind;
}

interface BetakasContextValue {
  state: AppState | null;
  me: User | undefined;
  isLoggedIn: boolean;
  booting: boolean;
  busy: boolean;

  /** Bir domain eylemini çalıştırır, dönen state'i yerleştirir ve mesajı gösterir. */
  run: (action: () => Promise<ActionResponse>) => Promise<boolean>;

  login: (email: string, password: string, role: string) => Promise<User>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;

  toasts: Toast[];
  toast: (message: string, kind?: ToastKind) => void;
  dismissToast: (id: number) => void;
}

const BetakasContext = createContext<BetakasContextValue | null>(null);

export function useBetakas(): BetakasContextValue {
  const ctx = useContext(BetakasContext);
  if (!ctx) throw new Error("useBetakas, BetakasProvider içinde kullanılmalı.");
  return ctx;
}

/** State kesin varken kullanılır (korumalı ekranlar) — her seferinde null kontrolü gerekmesin. */
export function useAppState(): AppState {
  const { state } = useBetakas();
  if (!state) throw new Error("State henüz yüklenmedi.");
  return state;
}

const POLL_MS = 5000;

export function BetakasProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AppState | null>(null);
  const [booting, setBooting] = useState(true);
  const [busy, setBusy] = useState(false);
  const [toasts, setToasts] = useState<Toast[]>([]);

  const toastId = useRef(0);
  // Yoklama sırasında güncel rev'e erişmek için — efekt bağımlılığı olmasın diye ref.
  const revRef = useRef(0);
  const busyRef = useRef(false);

  useEffect(() => { revRef.current = state?.rev ?? 0; }, [state?.rev]);
  useEffect(() => { busyRef.current = busy; }, [busy]);

  const toast = useCallback((message: string, kind: ToastKind = "good") => {
    const id = ++toastId.current;
    setToasts((list) => [...list, { id, message, kind }]);
    setTimeout(() => setToasts((list) => list.filter((t) => t.id !== id)), 5200);
  }, []);

  const dismissToast = useCallback((id: number) => {
    setToasts((list) => list.filter((t) => t.id !== id));
  }, []);

  /** Oturum varsa tam state, yoksa redakte public state. */
  const load = useCallback(async () => {
    if (tokens.isAuthed()) {
      try {
        setState(await api.getState());
        return;
      } catch {
        // Jeton geçersiz/süresi dolmuş → public state'e düş.
        tokens.clear();
      }
    }
    setState(await api.getPublicState());
  }, []);

  const refresh = useCallback(async () => { await load(); }, [load]);

  // --- Açılış ---
  useEffect(() => {
    let alive = true;
    (async () => {
      try {
        await load();
      } catch (err) {
        if (alive) {
          toast(
            err instanceof Error ? err.message : "Sunucuya ulaşılamıyor.",
            "bad",
          );
        }
      } finally {
        if (alive) setBooting(false);
      }
    })();
    return () => { alive = false; };
  }, [load, toast]);

  // --- Değişiklik yoklaması ---
  // Başka bir tarayıcıdaki kullanıcı yazdığında rev artar; kullanıcı bir alana
  // yazmıyorsa ekranı tazeleriz — sunum sırasında iki pencere canlı kalır.
  useEffect(() => {
    if (!state || !tokens.isAuthed()) return;

    const timer = setInterval(async () => {
      if (busyRef.current) return;

      const tag = document.activeElement?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return;

      try {
        const { rev } = await api.getRev();
        if (rev !== revRef.current) setState(await api.getState());
      } catch {
        /* geçici ağ hatası — bir sonraki turda tekrar denenir */
      }
    }, POLL_MS);

    return () => clearInterval(timer);
  }, [state !== null, state?.authUserId]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleError = useCallback(async (err: unknown) => {
    if (err instanceof ApiError && err.status === 401) {
      tokens.clear();
      await load();
      toast("Oturumun sona erdi, tekrar giriş yap.", "warn");
      return;
    }

    // 409: aradaki bir değişiklik yüzünden işlem artık geçerli değil (slot doldu,
    // talep kapandı…). Ekranı tazeleyip nedenini gösteririz.
    const message = err instanceof Error ? err.message : "İşlem tamamlanamadı.";
    if (err instanceof ApiError && err.status === 409 && tokens.isAuthed()) {
      try { setState(await api.getState()); } catch { /* yok say */ }
      toast(message, "warn");
      return;
    }

    toast(message, "bad");
  }, [load, toast]);

  const run = useCallback(async (action: () => Promise<ActionResponse>) => {
    setBusy(true);
    try {
      const res = await action();
      setState(res.state);
      if (res.message) toast(res.message, "good");
      return true;
    } catch (err) {
      await handleError(err);
      return false;
    } finally {
      setBusy(false);
    }
  }, [handleError, toast]);

  const login = useCallback(async (email: string, password: string, role: string) => {
    const auth = await api.login(email, password, role);
    setState(await api.getState());
    return auth.user;
  }, []);

  const logout = useCallback(async () => {
    await api.logout();
    setState(await api.getPublicState());
  }, []);

  const me = state ? currentUser(state) : undefined;

  const value = useMemo<BetakasContextValue>(() => ({
    state,
    me,
    isLoggedIn: !!(state?.authUserId && me),
    booting,
    busy,
    run,
    login,
    logout,
    refresh,
    toasts,
    toast,
    dismissToast,
  }), [state, me, booting, busy, run, login, logout, refresh, toasts, toast, dismissToast]);

  return <BetakasContext.Provider value={value}>{children}</BetakasContext.Provider>;
}
