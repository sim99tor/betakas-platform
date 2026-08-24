import { Link } from "react-router-dom";
import { Card } from "../components/ui";
import { SUBSCRIPTION_PLANS, TOKEN_PACKAGES } from "../lib/constants";
import { useBetakas } from "../state/BetakasProvider";

/** Tanıtım sayfası — giriş gerektirmez. */
export function Landing() {
  const { isLoggedIn, me } = useBetakas();
  const backTo = isLoggedIn ? (me?.role === "admin" ? "/yonetim" : "/panel") : "/giris";

  return (
    <div className="public-wrap">
      <div className="public-head">
        <div className="brand"><span className="logo">B</span> Betakas</div>
        <Link className="btn primary sm" to={backTo}>
          {isLoggedIn ? "Panele dön" : "Giriş yap"}
        </Link>
      </div>

      <section className="landing-hero">
        <h1>Erken aşama ürünler için <span className="grad-founder">gerçek</span> test ekosistemi</h1>
        <p className="lead">
          Kurucular ürünlerini test ettirmek için token harcar, testçiler yapılandırılmış geri
          bildirim vererek kazanır. Tokenler teslimat onaylanana kadar escrow'da bloke kalır —
          iki taraf da korunur.
        </p>
      </section>

      <div className="landing-grid">
        <Card title="Problem">
          <p>
            Yeni bir ürün çıkardığında ilk kullanıcıyı bulmak zordur. Arkadaşlarına sorarsın,
            "güzel olmuş" derler; işine yaramaz. Gerçek, yapılandırılmış geri bildirim pahalıdır.
          </p>
        </Card>

        <Card title="Çözüm: takas ekonomisi">
          <p>
            Başka bir kurucunun ürününü test edersin, token kazanırsın. Kazandığın tokenle kendi
            ürününe test alırsın. Beklemek istemiyorsan token satın alırsın.
          </p>
        </Card>

        <Card title="Kalite garantileri">
          <ul>
            <li>Minimum karakter eşikli yapılandırılmış form</li>
            <li>Ekran kaydı zorunluluğu ve en az 5 dakika test süresi</li>
            <li>Escrow: token yalnızca onaylanınca serbest kalır</li>
            <li>İtiraz mekanizması ve itibar cezası</li>
            <li>Danışıklı puanlama tespiti</li>
          </ul>
        </Card>

        <Card title="İtibar çarpanı">
          <p>
            Kaliteli geri bildirim veren testçi aynı test için daha fazla token kazanır
            (1.1× ve 1.2×). Bonus sistemden basılır, escrow matematiği bozulmaz.
          </p>
        </Card>
      </div>

      <section>
        <h2>Token paketleri</h2>
        <div className="plan-grid">
          {TOKEN_PACKAGES.map((p) => (
            <div key={p.id} className={`plan-card ${p.popular ? "popular" : ""}`}>
              {p.popular && <span className="ribbon">En çok tercih edilen</span>}
              <h3>{p.name}</h3>
              <div className="plan-meta">{p.tokens} token · ~{p.testers} testçi</div>
              <ul>{p.perks.map((x) => <li key={x}>{x}</li>)}</ul>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h2>Abonelik</h2>
        <div className="plan-grid">
          {SUBSCRIPTION_PLANS.map((p) => (
            <div key={p.id} className={`plan-card ${p.popular ? "popular" : ""}`}>
              {p.popular && <span className="ribbon">Öncelikli eşleştirme</span>}
              <h3>{p.name}</h3>
              <div className="plan-meta">{p.tokens} token/ay · ~{p.testers} testçi</div>
              <ul>{p.perks.map((x) => <li key={x}>{x}</li>)}</ul>
            </div>
          ))}
        </div>
      </section>

      <div className="landing-footer">
        Betakas · Kurucular ve testçiler için iki taraflı test ekosistemi.
      </div>
    </div>
  );
}
