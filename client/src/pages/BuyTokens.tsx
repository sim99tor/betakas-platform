import { useState } from "react";
import { Card, Empty, PageHead } from "../components/ui";
import { api } from "../lib/api";
import { SUBSCRIPTION_PLANS, TOKEN_PACKAGES } from "../lib/constants";
import { fmtDate, fmtTRY, packagePrice, planSplit, subscriptionOf } from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function BuyTokens() {
  const s = useAppState();
  const { me, run, busy } = useBetakas();
  const [tab, setTab] = useState<"once" | "sub">("once");
  const [card, setCard] = useState("");
  const [selected, setSelected] = useState<string | null>(null);

  if (!me) return null;
  const active = subscriptionOf(me);
  const invoices = s.purchases.filter((p) => p.userId === me.id).slice().reverse();

  return (
    <>
      <PageHead
        title="Token Satın Al"
        sub={`Ödemenin %${s.settings.feePct}'i platform komisyonu, kalanı testçi ödül havuzuna gider.`}
      />

      <div className="tabs">
        <button className={`tab ${tab === "once" ? "active" : ""}`} onClick={() => setTab("once")}>
          Tek seferlik
        </button>
        <button className={`tab ${tab === "sub" ? "active" : ""}`} onClick={() => setTab("sub")}>
          Abonelik
        </button>
      </div>

      {tab === "once" ? (
        <div className="plan-grid">
          {TOKEN_PACKAGES.map((pkg) => {
            const price = packagePrice(s, pkg);
            const fee = Math.round(price * s.settings.feePct / 100);
            return (
              <div key={pkg.id} className={`plan-card ${pkg.popular ? "popular" : ""} ${selected === pkg.id ? "sel" : ""}`}>
                {pkg.popular && <span className="ribbon">En çok tercih edilen</span>}
                <h3>{pkg.name}</h3>
                <div className="price">{fmtTRY(price)}</div>
                <div className="plan-meta">{pkg.tokens} token · ~{pkg.testers} testçi</div>
                <ul>{pkg.perks.map((p) => <li key={p}>{p}</li>)}</ul>
                <div className="split-note">
                  Komisyon {fmtTRY(fee)} · Ödül havuzu {fmtTRY(price - fee)}
                </div>
                <button className="btn primary" onClick={() => setSelected(pkg.id)}>Seç</button>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="plan-grid">
          {SUBSCRIPTION_PLANS.map((plan) => {
            const split = planSplit(s, plan);
            const isActive = active?.plan.id === plan.id;
            return (
              <div key={plan.id} className={`plan-card ${plan.popular ? "popular" : ""} ${isActive ? "sel" : ""}`}>
                {plan.popular && <span className="ribbon">Öncelikli eşleştirme</span>}
                <h3>{plan.name}</h3>
                <div className="price">{fmtTRY(plan.price)}<small>/ay</small></div>
                <div className="plan-meta">
                  {plan.tokens} token/ay · ~{plan.testers} testçi · {fmtTRY(plan.price / plan.tokens)}/token
                </div>
                <ul>{plan.perks.map((p) => <li key={p}>{p}</li>)}</ul>
                <div className="split-note">
                  Komisyon {fmtTRY(split.fee)} · Ödül havuzu {fmtTRY(split.pool)}
                </div>
                {isActive ? (
                  <button
                    className="btn ghost"
                    disabled={busy}
                    onClick={() => {
                      if (window.confirm(`${plan.name} aboneliğin iptal edilsin mi? Basılmış tokenler hesabında kalır.`)) {
                        run(() => api.cancelSubscription());
                      }
                    }}
                  >
                    Aboneliği iptal et
                  </button>
                ) : (
                  <button className="btn primary" disabled={!!active} onClick={() => setSelected(plan.id)}>
                    {active ? "Aktif aboneliğin var" : "Abone ol"}
                  </button>
                )}
              </div>
            );
          })}
        </div>
      )}

      {active && tab === "sub" && (
        <Card title="Aboneliğin">
          <p>
            <b>{active.plan.name}</b> · sonraki yenileme {fmtDate(active.renewsAt)}
          </p>
          <p className="help">
            Demo ortamında gerçek zamanlayıcı yok — bir dönemi elle ilerletebilirsin.
          </p>
          <button className="btn ghost" disabled={busy} onClick={() => run(() => api.renewSubscription())}>
            Bu ayı simüle et & yenile
          </button>
        </Card>
      )}

      {selected && (
        <Card title="Ödeme" className="form-card">
          <div className="field">
            <label>Kart numarası</label>
            <input
              value={card}
              onChange={(e) => setCard(e.target.value)}
              placeholder="4242 4242 4242 4242"
              inputMode="numeric"
            />
            <div className="help">Demo ödeme — gerçek bir sağlayıcı yok, kart saklanmaz.</div>
          </div>

          <button
            className="btn primary lg"
            disabled={busy}
            onClick={async () => {
              const ok = tab === "once"
                ? await run(() => api.buyPackage(selected, card))
                : await run(() => api.subscribe(selected, card));
              if (ok) { setSelected(null); setCard(""); }
            }}
          >
            {busy ? "İşleniyor…" : "Ödemeyi Tamamla"}
          </button>
        </Card>
      )}

      <Card title="Faturalar">
        {invoices.length === 0 ? (
          <Empty>Henüz satın alma yok.</Empty>
        ) : (
          <table className="table">
            <thead>
              <tr><th>Tarih</th><th>Fatura</th><th>Paket</th><th>Token</th><th>Tutar</th></tr>
            </thead>
            <tbody>
              {invoices.map((p) => (
                <tr key={p.id}>
                  <td>{fmtDate(p.ts)}</td>
                  <td>{p.invoiceNo}</td>
                  <td>{p.packageName}</td>
                  <td>{p.tokens}</td>
                  <td>{fmtTRY(p.gross)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </>
  );
}
