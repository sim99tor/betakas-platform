import { useState } from "react";
import { Avatar } from "../components/Avatar";
import { Card, Empty, PageHead, Tile, Tiles } from "../components/ui";
import { api } from "../lib/api";
import {
  acctName, activeSubscribers, fmtDate, fmtTRY, getUser, mrr, payoutRate,
  subscriptionOf, tokenEconomy,
} from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function Revenue() {
  const s = useAppState();
  const { run, busy } = useBetakas();
  const eco = tokenEconomy(s);
  const subs = activeSubscribers(s);

  const [fee, setFee] = useState(String(s.settings.feePct));
  const [price, setPrice] = useState(String(s.settings.tokenPrice));

  const pendingWithdrawals = s.withdrawals.filter((w) => w.status === "pending");
  const paidWithdrawals = s.withdrawals.filter((w) => w.status !== "pending");
  const subRevenue = s.purchases.filter((p) => p.kind === "subscription");

  return (
    <>
      <PageHead
        title="Gelir & Token Ekonomisi"
        sub={`Kurucular tokeni gerçek parayla alır: tutarın %${s.settings.feePct}'i platform geliri, kalanı testçi ödül havuzuna ayrılır.`}
      />

      <Tiles>
        <Tile cap="Toplam Token Satışı" value={fmtTRY(eco.gross)} foot={`${s.purchases.length} satın alma · ${eco.sold} token`} />
        <Tile cap="Platform Geliri" value={fmtTRY(eco.revenue)} tone="ok" foot={`efektif komisyon %${eco.effectiveFeePct}`} />
        <Tile cap="Ödül Havuzu" value={fmtTRY(eco.pool)} tone="violet" foot="testçilere karşı yükümlülük" />
        <Tile
          cap="Testçilere Ödenen"
          value={fmtTRY(eco.paidOut)}
          tone="credit"
          foot={`${fmtTRY(eco.pendingWithdrawals)} çekim onay bekliyor`}
        />
      </Tiles>

      <Tiles>
        <Tile cap="Aktif Abonelik" value={subs.length} />
        <Tile cap="Aylık Yinelenen Gelir (MRR)" value={fmtTRY(mrr(s))} tone="ok" foot={`yıllıklandırılmış ${fmtTRY(mrr(s) * 12)}`} />
        <Tile cap="Abonelik Tahsilatı" value={fmtTRY(subRevenue.reduce((a, p) => a + p.gross, 0))} foot={`${subRevenue.length} dönem faturalandı`} />
        <Tile cap="Abonelikten Komisyon" value={fmtTRY(subRevenue.reduce((a, p) => a + p.fee, 0))} foot="kalanı ödül havuzuna" />
      </Tiles>

      <Card title="Aboneler">
        {subs.length === 0 ? (
          <Empty>Aktif abonelik yok.</Empty>
        ) : (
          <table className="table">
            <thead><tr><th>Kurucu</th><th>Plan</th><th>Aylık</th><th>Token/ay</th><th>Sonraki yenileme</th></tr></thead>
            <tbody>
              {subs.map((u) => {
                const sub = subscriptionOf(u)!;
                return (
                  <tr key={u.id}>
                    <td>
                      <div className="user-cell">
                        <Avatar user={u} size="sm" />
                        <div><b>{u.name}</b><span>{u.startup}</span></div>
                      </div>
                    </td>
                    <td>
                      {sub.plan.name}
                      {sub.plan.priority && <span className="badge violet">öncelikli</span>}
                    </td>
                    <td>{fmtTRY(sub.plan.price)}</td>
                    <td>{sub.plan.tokens}</td>
                    <td>{fmtDate(sub.renewsAt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </Card>

      <Card title="Fiyatlandırma & Komisyon">
        <div className="two-col">
          <div className="field">
            <label>Platform komisyonu (%)</label>
            <div className="inline-form">
              <input value={fee} onChange={(e) => setFee(e.target.value)} inputMode="decimal" />
              <button
                className="btn primary sm"
                disabled={busy}
                onClick={() => run(() => api.setFeePct(parseFloat(fee)))}
              >
                Kaydet
              </button>
            </div>
            <div className="help">
              Satın alma anında kesilir. Yeni satışlar ve ödemeler bu orana göre işlenir.
            </div>
          </div>

          <div className="field">
            <label>Token liste fiyatı (₺)</label>
            <div className="inline-form">
              <input value={price} onChange={(e) => setPrice(e.target.value)} inputMode="decimal" />
              <button
                className="btn primary sm"
                disabled={busy}
                onClick={() => run(() => api.setTokenPrice(parseFloat(price)))}
              >
                Kaydet
              </button>
            </div>
            <div className="help">Paket fiyatları bu değerden türetilir.</div>
          </div>
        </div>

        <div className="split-bar">
          <div className="platform" style={{ width: `${s.settings.feePct}%` }} />
          <div className="pool" style={{ width: `${100 - s.settings.feePct}%` }} />
        </div>
        <div className="split-legend">
          <span>Platform payı %{s.settings.feePct} — {fmtTRY(s.settings.tokenPrice * s.settings.feePct / 100)}/token</span>
          <span>Testçi payı %{100 - s.settings.feePct} — {fmtTRY(payoutRate(s))}/token</span>
        </div>
      </Card>

      <Card title="Ekonomi Sağlığı">
        <div className="health-rows">
          <div className="health-row">
            <div>
              <b>Karşılığı ödenmiş token oranı</b>
              <span>{eco.sold} satılan · {eco.granted} promosyon/bonus token (nakit karşılığı yok)</span>
            </div>
            <span className={`badge ${eco.backedPct >= 60 ? "ok" : "warn"}`}>%{eco.backedPct}</span>
          </div>
          <div className="health-row">
            <div>
              <b>Havuz karşılama durumu</b>
              <span>Havuzda {fmtTRY(eco.pool)} · onay bekleyen çekim {fmtTRY(eco.pendingWithdrawals)}</span>
            </div>
            <span className={`badge ${eco.pool >= eco.pendingWithdrawals ? "ok" : "critical"}`}>
              {eco.pool >= eco.pendingWithdrawals ? "Yeterli" : "Yetersiz"}
            </span>
          </div>
        </div>
      </Card>

      <Card title={`Çekim Talepleri (${pendingWithdrawals.length})`}>
        {pendingWithdrawals.length === 0 ? (
          <Empty>Bekleyen çekim talebi yok.</Empty>
        ) : (
          <div className="rows">
            {pendingWithdrawals.map((w) => {
              const u = getUser(s, w.userId);
              return (
                <div key={w.id} className="row">
                  <Avatar user={u} />
                  <div className="row-main">
                    <b>{u?.name} · {fmtTRY(w.amount)}</b>
                    <span>{fmtDate(w.requestedAt)} · IBAN {w.iban}</span>
                  </div>
                  <button
                    className="btn primary sm"
                    disabled={busy}
                    onClick={() => run(() => api.resolveWithdrawal(w.id, "paid", null))}
                  >
                    Ödemeyi Onayla
                  </button>
                  <button
                    className="btn ghost sm"
                    disabled={busy}
                    onClick={() => {
                      const note = window.prompt("Ret gerekçesi (testçiye gösterilir):", "IBAN bilgisi doğrulanamadı.");
                      if (note) run(() => api.resolveWithdrawal(w.id, "rejected", note));
                    }}
                  >
                    Reddet
                  </button>
                </div>
              );
            })}
          </div>
        )}
      </Card>

      {paidWithdrawals.length > 0 && (
        <Card title="Çekim Geçmişi">
          <table className="table">
            <thead><tr><th>Tarih</th><th>Testçi</th><th>Tutar</th><th>Durum</th></tr></thead>
            <tbody>
              {paidWithdrawals.map((w) => (
                <tr key={w.id}>
                  <td>{fmtDate(w.resolvedAt)}</td>
                  <td>{getUser(s, w.userId)?.name}</td>
                  <td>{fmtTRY(w.amount)}</td>
                  <td>
                    <span className={`badge ${w.status === "paid" ? "ok" : "critical"}`}>
                      {w.status === "paid" ? "Ödendi" : "Reddedildi"}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      <Card title="Token Satışları">
        <table className="table">
          <thead><tr><th>Tarih</th><th>Fatura</th><th>Kurucu</th><th>Paket</th><th>Token</th><th>Tutar</th><th>Komisyon</th><th>Havuz</th></tr></thead>
          <tbody>
            {s.purchases.slice().reverse().map((p) => (
              <tr key={p.id}>
                <td>{fmtDate(p.ts)}</td>
                <td>{p.invoiceNo}</td>
                <td>{getUser(s, p.userId)?.name}</td>
                <td>{p.packageName}</td>
                <td>{p.tokens}</td>
                <td>{fmtTRY(p.gross)}</td>
                <td className="ok">{fmtTRY(p.fee)}</td>
                <td>{fmtTRY(p.pool)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      <Card title="Para Defteri (₺)">
        <p className="card-hint">
          Token defteriyle aynı mantık: her ₺ hareketi kimden-kime satırıdır, bakiyeler defterden türetilir.
        </p>
        <table className="table">
          <thead><tr><th>Tarih</th><th>İşlem</th><th>Gönderen</th><th>Alan</th><th>Tutar</th><th>Açıklama</th></tr></thead>
          <tbody>
            {s.cashLedger.slice().reverse().map((e) => (
              <tr key={e.id}>
                <td>{fmtDate(e.ts)}</td>
                <td>{e.type}</td>
                <td>{acctName(s, e.from)}</td>
                <td>{acctName(s, e.to)}</td>
                <td>{fmtTRY(e.amount)}</td>
                <td>{e.note}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </>
  );
}
