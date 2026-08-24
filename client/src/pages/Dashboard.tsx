import { Link } from "react-router-dom";
import { Card, Empty, PageHead, Tile, Tiles } from "../components/ui";
import { SessionStatusBadge } from "../components/ui";
import {
  balance, blockedFor, cashBalance, categoryMatch, escrowRemaining, fmtDate, fmtTRY,
  getRequest, getVersion, pendingEarnings, repMultiplier, reputation, slotsLeft,
  slotsTaken, starStr, testerLevel, versionsOf, versionStatus, withdrawable,
} from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function Dashboard() {
  const { me } = useBetakas();
  return me?.role === "tester" ? <TesterDashboard /> : <FounderDashboard />;
}

// ---------------- Kurucu ----------------

function FounderDashboard() {
  const s = useAppState();
  const { me } = useBetakas();
  if (!me) return null;

  const rep = reputation(s, me.id);
  const mult = repMultiplier(s, me.id);
  const available = balance(s, me.id);
  const blocked = blockedFor(s, me.id);

  const myTests = s.sessions.filter(
    (x) => x.testerId === me.id && x.status !== "accepted" && x.status !== "rejected",
  );
  const myRequests = s.requests.filter((r) => r.ownerId === me.id && r.status === "open");
  const myVersions = versionsOf(s, me.id).slice(-3).reverse();
  const recentLedger = s.ledger
    .filter((e) => e.from === me.id || e.to === me.id)
    .slice(-4).reverse();

  return (
    <>
      <PageHead
        title={`Merhaba, ${me.name.split(" ")[0]}`}
        sub={`${me.startup ?? ""} · ${me.tagline ?? ""}`}
      />

      <Tiles>
        <Tile
          cap="Kullanılabilir Token"
          value={available}
          tone="credit"
          foot={`≈ ${Math.floor(available / 15)} test · ${fmtTRY(available * s.settings.tokenPrice)} değerinde`}
        />
        <Tile cap="Escrow'da Bloke" value={blocked} tone="warn" foot="açık taleplerin için" />
        <Tile
          cap="İtibar"
          value={rep.avg ? rep.avg.toFixed(1) : "—"}
          foot={rep.avg ? `${starStr(rep.avg)} · çarpan ${mult}×` : "henüz puan yok"}
        />
        <Tile cap="Tamamlanan Test" value={rep.completed} foot="onaylanmış feedback" />
      </Tiles>

      <Card title="Üzerimdeki Testler">
        {myTests.length === 0 ? (
          <Empty>
            Şu an üzerinde test yok. <Link to="/kesfet">Keşfet</Link>'ten bir ürün seç, test et, token kazan.
          </Empty>
        ) : (
          <div className="rows">
            {myTests.map((x) => {
              const r = getRequest(s, x.requestId);
              return (
                <Link key={x.id} to={`/talep/${x.requestId}`} className="row">
                  <div className="row-main">
                    <b>{r?.title}</b>
                    <span>{r?.credits} token · {fmtDate(x.appliedAt)}</span>
                  </div>
                  <SessionStatusBadge status={x.status} />
                </Link>
              );
            })}
          </div>
        )}
      </Card>

      <Card title="Açık Taleplerim">
        {myRequests.length === 0 ? (
          <Empty>
            Açık talebin yok. <Link to="/yeni-talep">Yeni test talebi</Link> açabilirsin.
          </Empty>
        ) : (
          <div className="rows">
            {myRequests.map((r) => {
              const pending = s.sessions.filter(
                (x) => x.requestId === r.id && x.status === "applied",
              ).length;
              return (
                <Link key={r.id} to={`/talep/${r.id}`} className="row">
                  <div className="row-main">
                    <b>{r.title}</b>
                    <span>
                      {slotsTaken(s, r.id)}/{r.slots} slot dolu · {escrowRemaining(s, r.id)} token blokede
                      {pending > 0 && <> · <b className="credit">{pending} yeni başvuru</b></>}
                    </span>
                  </div>
                  {r.visibility === "exclude-sector" && <span className="badge muted">Sektöre kapalı</span>}
                  <span className="btn ghost sm">Yönet</span>
                </Link>
              );
            })}
          </div>
        )}
      </Card>

      <Card
        title="Ürün Sürümlerin"
        actions={<Link className="btn ghost sm" to="/surumler">Tümü →</Link>}
      >
        {myVersions.length === 0 ? (
          <Empty>Henüz sürüm çıkarmadın. <Link to="/surumler">İlk sürümünü ekle</Link>.</Empty>
        ) : (
          <div className="rows">
            {myVersions.map((v) => {
              const st = versionStatus(s, v.id);
              const tested = s.sessions.filter(
                (x) => x.status === "accepted" &&
                  s.requests.some((r) => r.versionId === v.id && r.id === x.requestId),
              ).length;
              return (
                <Link key={v.id} to={`/surum/${v.id}`} className="row">
                  <span className="ver-badge">{v.label}</span>
                  <div className="row-main">
                    <b>{st.label}</b>
                    <span>{tested} onaylı test{tested === 0 && " — henüz geri bildirim yok"}</span>
                  </div>
                  <span className="btn ghost sm">Karne</span>
                </Link>
              );
            })}
          </div>
        )}
      </Card>

      <Card
        title="Son Token Hareketleri"
        actions={<Link className="btn ghost sm" to="/defter">Tüm defteri gör →</Link>}
      >
        <LedgerRows entries={recentLedger} meId={me.id} />
      </Card>
    </>
  );
}

// ---------------- Testçi ----------------

function TesterDashboard() {
  const s = useAppState();
  const { me } = useBetakas();
  if (!me) return null;

  const rep = reputation(s, me.id);
  const level = testerLevel(s, me.id);
  const pending = pendingEarnings(s, me.id);
  const cash = cashBalance(s, me.id);

  const active = s.sessions.filter(
    (x) => x.testerId === me.id && (x.status === "applied" || x.status === "approved" || x.status === "submitted"),
  );

  // Uzmanlık eşleşmesi olanlar önce.
  const open = s.requests
    .filter((r) => r.status === "open" && r.ownerId !== me.id && slotsLeft(s, r) > 0)
    .filter((r) => !s.sessions.some((x) => x.requestId === r.id && x.testerId === me.id && x.status !== "rejected"))
    .sort((a, b) => Number(categoryMatch(me, b)) - Number(categoryMatch(me, a)))
    .slice(0, 4);

  return (
    <>
      <PageHead
        title={`Merhaba, ${me.name.split(" ")[0]}`}
        sub={`${me.title ?? ""} · ${level.name}`}
      />

      <Tiles>
        <Tile cap="Kullanılabilir Token" value={balance(s, me.id)} tone="credit" />
        <Tile cap="Bekleyen Kazanç" value={pending} tone="warn" foot="escrow'da onay bekliyor" />
        <Tile
          cap="Nakit Cüzdan"
          value={fmtTRY(cash)}
          tone="ok"
          foot={`çekilebilir: ${fmtTRY(withdrawable(s, me.id))}`}
        />
        <Tile
          cap="İtibar"
          value={rep.avg ? rep.avg.toFixed(1) : "—"}
          foot={rep.avg ? `${starStr(rep.avg)} · çarpan ${repMultiplier(s, me.id)}×` : "henüz puan yok"}
        />
      </Tiles>

      <Card
        title="Görevlerim"
        actions={<Link className="btn ghost sm" to="/gorevlerim">Tümü →</Link>}
      >
        {active.length === 0 ? (
          <Empty>Aktif görevin yok. <Link to="/kesfet">Açık Testler</Link>'e göz at.</Empty>
        ) : (
          <div className="rows">
            {active.map((x) => {
              const r = getRequest(s, x.requestId);
              return (
                <Link key={x.id} to={`/talep/${x.requestId}`} className="row">
                  <div className="row-main">
                    <b>{r?.title}</b>
                    <span>
                      {r?.credits} token
                      {r && ` · kazancın ≈ ${Math.round(r.credits * repMultiplier(s, me.id))} token`}
                    </span>
                  </div>
                  <SessionStatusBadge status={x.status} />
                </Link>
              );
            })}
          </div>
        )}
      </Card>

      <Card
        title="Sana Uygun Açık Testler"
        actions={<Link className="btn ghost sm" to="/kesfet">Tümü →</Link>}
      >
        {open.length === 0 ? (
          <Empty>Şu an uygun açık test yok.</Empty>
        ) : (
          <div className="rows">
            {open.map((r) => {
              const version = getVersion(s, r.versionId);
              return (
                <Link key={r.id} to={`/talep/${r.id}`} className="row">
                  <div className="row-main">
                    <b>{r.title}</b>
                    <span>
                      {r.productCategory} · {r.feedbackType}
                      {version && <> · <span className="ver-badge">{version.label}</span></>}
                    </span>
                  </div>
                  {categoryMatch(me, r) && <span className="badge violet">Uzmanlığınla eşleşiyor</span>}
                  <span className="badge credit">{r.credits} token</span>
                </Link>
              );
            })}
          </div>
        )}
      </Card>
    </>
  );
}

// ---------------- Ortak ----------------

function LedgerRows({ entries, meId }: {
  entries: { id: string; ts: number; from: string; to: string; amount: number; note?: string | null }[];
  meId: string;
}) {
  if (entries.length === 0) return <Empty>Henüz hareket yok.</Empty>;

  return (
    <div className="rows">
      {entries.map((e) => {
        const incoming = e.to === meId;
        return (
          <div key={e.id} className="row">
            <div className="row-main">
              <b>{e.note}</b>
              <span>{fmtDate(e.ts)}</span>
            </div>
            <span className={`amount ${incoming ? "in" : "out"}`}>
              {incoming ? "+" : "−"}{e.amount}
            </span>
          </div>
        );
      })}
    </div>
  );
}
