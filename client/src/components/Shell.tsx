import { NavLink, useNavigate } from "react-router-dom";
import type { ReactNode } from "react";
import { Avatar } from "./Avatar";
import { api } from "../lib/api";
import { ROLE_LABEL } from "../lib/constants";
import { balance, subtitleOf } from "../lib/derive";
import { useBetakas } from "../state/BetakasProvider";
import type { Role } from "../lib/types";

interface NavEntry {
  to: string;
  label: string;
  badge?: number;
}

/** Rolün görebileceği menü. Sunucu da aynı yetkileri uçlarda ayrıca doğrular. */
function navFor(role: Role, counts: { disputes: number; pendingUsers: number }): {
  main: NavEntry[];
  eco: NavEntry[];
} {
  if (role === "admin") {
    return {
      main: [
        { to: "/yonetim", label: "Yönetim Paneli", badge: counts.disputes + counts.pendingUsers },
        { to: "/gelir", label: "Gelir & Ekonomi" },
      ],
      eco: [
        { to: "/defter", label: "Token Defteri" },
        { to: "/radar", label: "Radar & Liderlik" },
      ],
    };
  }

  if (role === "tester") {
    return {
      main: [
        { to: "/panel", label: "Testçi Paneli" },
        { to: "/kesfet", label: "Açık Testler" },
        { to: "/gorevlerim", label: "Görevlerim" },
        { to: "/profil", label: "Profilim" },
      ],
      eco: [
        { to: "/defter", label: "Kazançlarım" },
        { to: "/radar", label: "Radar & Liderlik" },
      ],
    };
  }

  return {
    main: [
      { to: "/panel", label: "Panelim" },
      { to: "/kesfet", label: "Keşfet" },
      { to: "/surumler", label: "Ürün Sürümleri" },
      { to: "/yeni-talep", label: "Yeni Test Talebi" },
      { to: "/token-satin-al", label: "Token Satın Al" },
      { to: "/profil", label: "Profilim" },
    ],
    eco: [
      { to: "/defter", label: "Token Defteri" },
      { to: "/radar", label: "Radar & Liderlik" },
    ],
  };
}

export function Shell({ children }: { children: ReactNode }) {
  const { state, me, logout, refresh, toast } = useBetakas();
  const navigate = useNavigate();

  if (!state || !me) return null;

  const disputes = state.sessions.filter((s) => s.status === "disputed").length;
  const pendingUsers = state.users.filter((u) => u.status === "pending").length;
  const nav = navFor(me.role, { disputes, pendingUsers });

  async function handleLogout() {
    await logout();
    navigate("/giris", { replace: true });
    toast("Oturum kapatıldı.", "warn");
  }

  async function handleReset() {
    if (me!.role !== "admin") {
      toast("Veriler ortak veritabanında — demoyu yalnızca yönetim sıfırlayabilir.", "warn");
      return;
    }
    const ok = window.confirm(
      "Demo verileri başlangıç durumuna sıfırlansın mı?\n\n" +
      "BU ORTAK VERİTABANINI SIFIRLAR: tüm kullanıcıların verisi silinir ve oturumun kapanır.",
    );
    if (!ok) return;

    try {
      await api.reset();
      await logout();
      navigate("/giris", { replace: true });
      toast("Demo sıfırlandı ✓ Giriş ekranındasın.", "good");
    } catch (err) {
      toast(err instanceof Error ? err.message : "Sıfırlanamadı.", "bad");
    }
  }

  const roleView =
    me.role === "admin" ? "YÖNETİM GÖRÜNÜMÜ"
      : me.role === "tester" ? "TESTÇİ GÖRÜNÜMÜ"
        : "KURUCU GÖRÜNÜMÜ";

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="logo">B</span> Betakas
        </div>
        <div className="nav-cap">{roleView}</div>

        {nav.main.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}
          >
            {item.label}
            {!!item.badge && <span className="badge critical" style={{ marginLeft: "auto" }}>{item.badge}</span>}
          </NavLink>
        ))}

        <div className="nav-cap">EKOSİSTEM</div>
        {nav.eco.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}
          >
            {item.label}
          </NavLink>
        ))}

        <div className="spacer" />

        <NavLink to="/tanitim" className="nav-item">Tanıtım Sayfası</NavLink>
        <button className="nav-item" onClick={handleLogout}>Çıkış Yap</button>
        {me.role === "admin" && (
          <button className="nav-item" onClick={handleReset}>Demoyu Sıfırla</button>
        )}
      </aside>

      <div className="main">
        <div className="topbar">
          <button className="btn ghost sm" onClick={() => navigate(-1)}>← Geri</button>
          <span className="sprint-chip">Test Sprinti #{state.sprintNo} · aktif</span>

          <div className="spacer" />

          <button className="btn ghost sm" onClick={() => refresh()} title="Sunucudan tazele">
            ⟳
          </button>

          <div className="account-chip">
            <Avatar user={me} />
            <div className="who">
              <b>{me.name}</b>
              <span>{subtitleOf(me)}</span>
            </div>
            <span className={`role-tag ${me.role}`}>{ROLE_LABEL[me.role].toLocaleUpperCase("tr-TR")}</span>
            {me.role !== "admin" && (
              <span className="token-chip" title="Kullanılabilir token">
                {balance(state, me.id)} token
              </span>
            )}
            <button className="btn ghost sm" onClick={handleLogout}>Çıkış</button>
          </div>
        </div>

        <div className="content">{children}</div>
      </div>
    </div>
  );
}
