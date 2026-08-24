import { useState } from "react";
import { Card, Empty, PageHead, Tile, Tiles } from "../components/ui";
import { api } from "../lib/api";
import { MIN_WITHDRAWAL } from "../lib/constants";
import {
  acctName, balance, blockedFor, cashBalance, fmtDate, fmtTRY, totalEarned, withdrawable,
} from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

/** Token defteri; testçilerde ayrıca nakit cüzdan ve çekim akışı gösterilir. */
export function Ledger() {
  const s = useAppState();
  const { me, run, busy } = useBetakas();
  if (!me) return null;

  const isAdmin = me.role === "admin";
  const isTester = me.role === "tester";

  const rows = s.ledger
    .filter((e) => isAdmin || e.from === me.id || e.to === me.id)
    .slice().reverse();

  const myWithdrawals = s.withdrawals.filter((w) => w.userId === me.id).slice().reverse();
  const available = withdrawable(s, me.id);

  function exportCsv() {
    const header = ["Tarih", "Tür", "Gönderen", "Alan", "Token", "Açıklama"];
    const body = rows.map((e) => [
      fmtDate(e.ts), e.type, acctName(s, e.from), acctName(s, e.to), String(e.amount), e.note ?? "",
    ]);
    const csv = [header, ...body]
      .map((row) => row.map((cell) => {
        const v = cell.replace(/"/g, '""');
        return /[",\n]/.test(v) ? `"${v}"` : v;
      }).join(","))
      .join("\r\n");

    const blob = new Blob(["﻿" + csv], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "betakas-token-defteri.csv";
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <>
      <PageHead
        title={isTester ? "Kazançlarım" : "Token Defteri"}
        sub="Her hareket kimden-kime satırıdır. Bakiyeler her zaman bu defterden türetilir, elle değiştirilmez."
        actions={<button className="btn ghost sm" onClick={exportCsv}>CSV indir</button>}
      />

      <Tiles>
        <Tile cap="Kullanılabilir Token" value={balance(s, me.id)} tone="credit" />
        {isTester ? (
          <>
            <Tile cap="Toplam Kazanılan" value={totalEarned(s, me.id)} />
            <Tile cap="Nakit Cüzdan" value={fmtTRY(cashBalance(s, me.id))} tone="ok" />
            <Tile cap="Çekilebilir" value={fmtTRY(available)} foot={`min ${fmtTRY(MIN_WITHDRAWAL)}`} />
          </>
        ) : (
          <>
            <Tile cap="Escrow'da Bloke" value={blockedFor(s, me.id)} tone="warn" />
            <Tile cap="Toplam Hareket" value={rows.length} />
          </>
        )}
      </Tiles>

      {isTester && (
        <Card title="Nakit Çekim">
          <p className="card-hint">
            Minimum çekim tutarı {fmtTRY(MIN_WITHDRAWAL)}. Talep yönetim onayından sonra ödenir.
          </p>
          <WithdrawForm
            available={available}
            iban={me.iban ?? ""}
            busy={busy}
            onSubmit={(amount, iban) => run(() => api.requestWithdrawal(amount, iban))}
          />

          {myWithdrawals.length > 0 && (
            <table className="table" style={{ marginTop: 16 }}>
              <thead><tr><th>Tarih</th><th>Tutar</th><th>IBAN</th><th>Durum</th></tr></thead>
              <tbody>
                {myWithdrawals.map((w) => (
                  <tr key={w.id}>
                    <td>{fmtDate(w.requestedAt)}</td>
                    <td>{fmtTRY(w.amount)}</td>
                    <td>{w.iban}</td>
                    <td>
                      <span className={`badge ${w.status === "paid" ? "ok" : w.status === "rejected" ? "critical" : "warn"}`}>
                        {w.status === "paid" ? "Ödendi" : w.status === "rejected" ? "Reddedildi" : "Onay bekliyor"}
                      </span>
                      {w.note && <div className="help">{w.note}</div>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}

      <Card title="Token Hareketleri">
        {rows.length === 0 ? (
          <Empty>Henüz hareket yok.</Empty>
        ) : (
          <table className="table">
            <thead>
              <tr><th>Tarih</th><th>İşlem</th><th>Gönderen</th><th>Alan</th><th>Token</th><th>Açıklama</th></tr>
            </thead>
            <tbody>
              {rows.map((e) => (
                <tr key={e.id}>
                  <td>{fmtDate(e.ts)}</td>
                  <td>{e.type}</td>
                  <td>{acctName(s, e.from)}</td>
                  <td>{acctName(s, e.to)}</td>
                  <td className={e.to === me.id ? "in" : e.from === me.id ? "out" : ""}>{e.amount}</td>
                  <td>{e.note}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </>
  );
}

function WithdrawForm({ available, iban: initialIban, busy, onSubmit }: {
  available: number; iban: string; busy: boolean;
  onSubmit: (amount: number, iban: string) => void;
}) {
  const [amount, setAmount] = useState(String(Math.max(0, available)));
  const [iban, setIban] = useState(initialIban);

  const value = Math.round(parseFloat(amount.replace(",", ".")) * 100) / 100;
  const valid = value >= MIN_WITHDRAWAL && value <= available && iban.trim().length >= 8;

  return (
    <div className="two-col">
      <div className="field">
        <label>Tutar (₺)</label>
        <input value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" />
      </div>
      <div className="field">
        <label>IBAN</label>
        <input value={iban} onChange={(e) => setIban(e.target.value)} placeholder="TR** **** **** ****" />
      </div>
      <div className="field" style={{ alignSelf: "end" }}>
        <button
          className="btn primary"
          disabled={busy || !valid}
          onClick={() => onSubmit(value, iban.trim())}
        >
          Çekim Talebi Oluştur
        </button>
      </div>
    </div>
  );
}
