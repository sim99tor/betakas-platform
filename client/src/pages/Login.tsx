import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Avatar } from "../components/Avatar";
import { api } from "../lib/api";
import { DEMO_PASSWORD, OTHER_CATEGORY, PRODUCT_CATEGORIES, ROLE_LABEL, SECTORS } from "../lib/constants";
import { ApiError } from "../lib/types";
import type { DemoAccount, Role } from "../lib/types";
import { useBetakas } from "../state/BetakasProvider";

type Tab = "founder" | "tester" | "admin";
type Mode = "login" | "register";

const AUTH_COPY: Record<Tab, {
  title: string; sub: string; emailPh: string; cta: string; altQ: string; altA: string;
}> = {
  founder: {
    title: "Kurucu Girişi",
    sub: "Ürünün için test talebi aç, tokenini escrow'da blokede tut, gelen feedback'i puanla.",
    emailPh: "kurucu@startupun.co",
    cta: "Kurucu olarak giriş yap",
    altQ: "Kurucu hesabın yok mu?",
    altA: "Ekosisteme başvur",
  },
  tester: {
    title: "Testçi Girişi",
    sub: "Açık test taleplerine başvur, yapılandırılmış feedback teslim et, token kazan.",
    emailPh: "sen@testci.co",
    cta: "Testçi olarak giriş yap",
    altQ: "Testçi hesabın yok mu?",
    altA: "Testçi olarak başvur",
  },
  admin: {
    title: "Yönetim Girişi",
    sub: "Üyelik onayları, anlaşmazlık çözümü ve ekonomi sağlığı paneli.",
    emailPh: "yonetim@betakas.co",
    cta: "Yönetim paneline gir",
    altQ: "",
    altA: "",
  },
};

export function Login() {
  const { state, login, toast } = useBetakas();
  const navigate = useNavigate();

  const [tab, setTab] = useState<Tab>("founder");
  const [mode, setMode] = useState<Mode>("login");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const copy = AUTH_COPY[tab];
  const demoAccounts = (state?.demoAccounts ?? []).filter((a) => a.role === tab);

  async function handleLogin(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    if (!email.trim() || !password) {
      setError("E-posta ve şifre zorunlu.");
      return;
    }

    setPending(true);
    try {
      const user = await login(email.trim(), password, tab);
      toast(`Hoş geldin ${user.name.split(" ")[0]} · ${ROLE_LABEL[user.role]} olarak giriş yaptın.`, "good");
      navigate(user.role === "admin" ? "/yonetim" : "/panel", { replace: true });
    } catch (err) {
      // Sunucu "yanlış sekme" durumunu ayrı işaretler: doğru sekmeye geçirip tekrar denetiriz.
      if (err instanceof ApiError && err.data.error === "wrong-tab" && err.data.role) {
        const role = err.data.role as Role;
        setTab(role as Tab);
        setError(`Bu hesap bir ${ROLE_LABEL[role]} hesabı. Doğru sekmeye geçirdik, tekrar dene.`);
      } else {
        setError(err instanceof Error ? err.message : "Giriş başarısız.");
      }
    } finally {
      setPending(false);
    }
  }

  function fillDemo(account: DemoAccount) {
    setEmail(account.email);
    setPassword(DEMO_PASSWORD);
    setError("");
  }

  return (
    <div className="auth-wrap">
      <div className="auth-hero">
        <div className="brand">
          <span className="logo">B</span> Betakas
        </div>
        <h1>
          İki taraflı ekosistem:<br />
          <span className="grad-founder">kurucular</span> ve{" "}
          <span className="grad-tester">testçiler</span>.
        </h1>
        <p className="lead">
          Kurucu token harcar, gerçek kullanıcıdan yapılandırılmış test alır. Testçi emeğinin
          karşılığını token olarak kazanır. Tokenler teslimat onaylanana kadar escrow'da bloke
          kalır — iki taraf da korunur.
        </p>

        <div className="hero-card">
          <b>Kurucu hesabı</b>
          <span>Test talebi açar, başvuruları onaylar, gelen feedback'i puanlar ve sentez raporunu görür.</span>
        </div>
        <div className="hero-card">
          <b>Testçi hesabı</b>
          <span>Açık testleri görür, başvurur, ekran kaydı + form ile teslim eder; onaylanınca tokeni serbest kalır.</span>
        </div>

        <div className="spacer" />
        <div className="foot">
          Veriler ortak sunucuda (PostgreSQL) tutulur ·{" "}
          <button className="btn ghost sm" style={{ padding: "3px 10px" }} onClick={() => navigate("/tanitim")}>
            Betakas nedir?
          </button>
        </div>
      </div>

      <div className="auth-panel">
        <div className="auth-card">
          <div className="auth-tabs">
            <button
              className={`auth-tab founder ${tab === "founder" ? "active" : ""}`}
              onClick={() => { setTab("founder"); setError(""); }}
            >
              Kurucu
            </button>
            <button
              className={`auth-tab tester ${tab === "tester" ? "active" : ""}`}
              onClick={() => { setTab("tester"); setError(""); }}
            >
              Testçi
            </button>
          </div>

          <h2>{copy.title}</h2>
          <p className="auth-sub">{copy.sub}</p>

          {mode === "register" ? (
            <RegisterForm
              tab={tab === "admin" ? "founder" : tab}
              error={error}
              setError={setError}
              onDone={(registeredEmail) => {
                setMode("login");
                setEmail(registeredEmail);
                setPassword("");
                setError("");
                toast("Başvurun alındı ✓ Yönetim onayladıktan sonra giriş yapabilirsin.", "good");
              }}
              onCancel={() => { setMode("login"); setError(""); }}
            />
          ) : (
            <>
              <form className="auth-form" onSubmit={handleLogin}>
                <div className="field">
                  <label htmlFor="lg-email">E-posta</label>
                  <input
                    id="lg-email"
                    type="email"
                    autoComplete="username"
                    placeholder={copy.emailPh}
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                  />
                </div>

                <div className="field">
                  <label htmlFor="lg-pw">Şifre</label>
                  <input
                    id="lg-pw"
                    type="password"
                    autoComplete="current-password"
                    placeholder="••••••••"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                  <div className="help">
                    Demo şifresi tüm hesaplar için: <strong>{DEMO_PASSWORD}</strong>
                  </div>
                </div>

                {error && <div className="auth-error">{error}</div>}

                <button className="btn primary lg" type="submit" disabled={pending}>
                  {pending ? "Giriş yapılıyor…" : `${copy.cta} →`}
                </button>

                {copy.altQ && (
                  <div className="auth-alt">
                    {copy.altQ}{" "}
                    <button type="button" onClick={() => { setMode("register"); setError(""); }}>
                      {copy.altA}
                    </button>
                  </div>
                )}
              </form>

              {demoAccounts.length > 0 && (
                <div className="auth-demo">
                  <div className="cap">Demo hesapları · tıkla, alanlar dolsun</div>
                  <div className="auth-accounts">
                    {demoAccounts.map((a) => (
                      <button key={a.id} className="auth-account" onClick={() => fillDemo(a)}>
                        <Avatar user={{ id: a.id, initials: a.initials, name: a.name }} />
                        <div className="who">
                          <b>{a.name}</b>
                          <span>
                            {a.subtitle} ·{" "}
                            {a.role === "tester"
                              ? a.completed
                                ? `${a.completed} test · ${a.avg.toFixed(1)}★`
                                : "yeni"
                              : `${a.balance} token`}
                          </span>
                        </div>
                        <span className="go">{a.email}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              <div className="auth-admin-link">
                <button onClick={() => { setTab(tab === "admin" ? "founder" : "admin"); setError(""); }}>
                  {tab === "admin" ? "← Kurucu / testçi girişine dön" : "Platform yöneticisi olarak gir"}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

// ---------------- Kayıt formu ----------------

interface RegisterProps {
  tab: "founder" | "tester";
  error: string;
  setError: (msg: string) => void;
  onDone: (email: string) => void;
  onCancel: () => void;
}

function RegisterForm({ tab, error, setError, onDone, onCancel }: RegisterProps) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [org, setOrg] = useState("");
  const [tagline, setTagline] = useState("");
  const [sector, setSector] = useState(SECTORS[0]);
  const [expertise, setExpertise] = useState<string[]>([]);
  const [expertiseOther, setExpertiseOther] = useState("");
  const [pending, setPending] = useState(false);

  const toggleCategory = (c: string) =>
    setExpertise((list) => (list.includes(c) ? list.filter((x) => x !== c) : [...list, c]));

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    // Sunucu aynı kuralları tekrar uygular; bunlar yalnızca anında geri bildirim.
    if (!name.trim() || !email.trim() || !org.trim() || !tagline.trim()) {
      setError("Tüm zorunlu alanları doldur."); return;
    }
    if (password.length < 6) { setError("Şifre en az 6 karakter olmalı."); return; }
    if (tab === "tester" && expertise.length === 0) {
      setError("En az bir uzmanlık alanı seç."); return;
    }
    if (expertise.includes(OTHER_CATEGORY) && !expertiseOther.trim()) {
      setError('"Diğer" seçtin — hangi alanı kastettiğini yaz.'); return;
    }

    setPending(true);
    try {
      await api.register({
        name: name.trim(),
        email: email.trim(),
        password,
        role: tab,
        org: org.trim(),
        tagline: tagline.trim(),
        sector: tab === "founder" ? sector : null,
        expertiseCategories: expertise,
        expertiseOther: expertiseOther.trim() || null,
      });
      onDone(email.trim());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Başvuru gönderilemedi.");
    } finally {
      setPending(false);
    }
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <div className="field">
        <label>Ad Soyad <span className="req-star">*</span></label>
        <input placeholder="Adın ve soyadın" value={name} onChange={(e) => setName(e.target.value)} />
      </div>

      <div className="field">
        <label>E-posta <span className="req-star">*</span></label>
        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
      </div>

      <div className="field">
        <label>Şifre <span className="req-star">*</span></label>
        <input
          type="password"
          autoComplete="new-password"
          placeholder="En az 6 karakter"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <div className="help">Hesabına bu şifreyle gireceksin.</div>
      </div>

      {tab === "founder" ? (
        <>
          <div className="field">
            <label>Startup adı <span className="req-star">*</span></label>
            <input placeholder="Örn: FinBütçe" value={org} onChange={(e) => setOrg(e.target.value)} />
          </div>
          <div className="field">
            <label>Sektör</label>
            <select value={sector} onChange={(e) => setSector(e.target.value)}>
              {SECTORS.map((x) => <option key={x}>{x}</option>)}
            </select>
          </div>
        </>
      ) : (
        <>
          <div className="field">
            <label>Uzmanlık başlığın <span className="req-star">*</span></label>
            <input placeholder="Örn: Kıdemli QA Uzmanı" value={org} onChange={(e) => setOrg(e.target.value)} />
          </div>
          <div className="field">
            <label>Uzmanlık alanların <span className="req-star">*</span></label>
            <div className="cat-picker">
              {PRODUCT_CATEGORIES.map((c) => (
                <button
                  type="button"
                  key={c}
                  className={`cat-chip ${expertise.includes(c) ? "on" : ""}`}
                  onClick={() => toggleCategory(c)}
                >
                  {c}
                </button>
              ))}
            </div>
            {expertise.includes(OTHER_CATEGORY) && (
              <input
                style={{ marginTop: 8 }}
                maxLength={60}
                placeholder="Örn: Oyun, IoT, Masaüstü yazılım"
                value={expertiseOther}
                onChange={(e) => setExpertiseOther(e.target.value)}
              />
            )}
            <div className="help">
              Birden fazla seçebilirsin — Açık Testler sayfasında bu kategorilerdeki talepler üstte gösterilir.
            </div>
          </div>
        </>
      )}

      <div className="field">
        <label>Kısa tanıtım <span className="req-star">*</span></label>
        <input
          placeholder={tab === "founder" ? "Ürünün bir cümlede ne yapıyor?" : "Hangi konularda test edersin?"}
          value={tagline}
          onChange={(e) => setTagline(e.target.value)}
        />
      </div>

      {error && <div className="auth-error">{error}</div>}

      <button className="btn primary lg" type="submit" disabled={pending}>
        {pending ? "Gönderiliyor…" : "Başvuruyu Gönder"}
      </button>

      <div className="auth-alt">
        Kapalı ekosistem: başvurun yönetim onayından sonra aktifleşir.{" "}
        <button type="button" onClick={onCancel}>Girişe dön</button>
      </div>
    </form>
  );
}
