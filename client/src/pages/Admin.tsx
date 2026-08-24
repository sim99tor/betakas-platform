import { Avatar } from "../components/Avatar";
import { Card, Empty, PageHead, Tile, Tiles } from "../components/ui";
import { api } from "../lib/api";
import { getRequest, getUser, reputation, subtitleOf } from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function Admin() {
  const s = useAppState();
  const { run, busy } = useBetakas();

  const pendingUsers = s.users.filter((u) => u.status === "pending");
  const disputes = s.sessions.filter((x) => x.status === "disputed");
  const activeUsers = s.users.filter((u) => u.status === "active" && u.role !== "admin");

  return (
    <>
      <PageHead
        title="Yönetim Paneli"
        sub="Kapalı ekosistem: üyelik onayları, anlaşmazlık çözümü ve kalite kontrolü."
      />

      <Tiles>
        <Tile cap="Bekleyen Üyelik" value={pendingUsers.length} tone={pendingUsers.length ? "warn" : undefined} />
        <Tile cap="Açık Anlaşmazlık" value={disputes.length} tone={disputes.length ? "warn" : undefined} />
        <Tile cap="Aktif Üye" value={activeUsers.length} tone="credit" />
        <Tile cap="Toplam Test" value={s.sessions.filter((x) => x.status === "accepted").length} tone="ok" />
      </Tiles>

      <Card title={`Üyelik Başvuruları (${pendingUsers.length})`}>
        {pendingUsers.length === 0 ? (
          <Empty>Bekleyen başvuru yok.</Empty>
        ) : (
          <div className="rows">
            {pendingUsers.map((u) => (
              <div key={u.id} className="row">
                <Avatar user={u} />
                <div className="row-main">
                  <b>{u.name}</b>
                  <span>
                    {u.role === "founder" ? "Kurucu" : "Testçi"} · {subtitleOf(u)} · {u.email}
                  </span>
                  <span className="help">{u.tagline}</span>
                </div>
                <button className="btn primary sm" disabled={busy} onClick={() => run(() => api.approveUser(u.id))}>
                  Onayla (+100 token)
                </button>
                <button className="btn ghost sm" disabled={busy} onClick={() => run(() => api.rejectUser(u.id))}>
                  Reddet
                </button>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Card title={`Anlaşmazlıklar (${disputes.length})`}>
        {disputes.length === 0 ? (
          <Empty>Açık anlaşmazlık yok.</Empty>
        ) : (
          <div className="session-list">
            {disputes.map((x) => {
              const request = getRequest(s, x.requestId);
              const tester = getUser(s, x.testerId);
              const owner = getUser(s, request?.ownerId);
              return (
                <div key={x.id} className="session-item">
                  <div className="session-head">
                    <Avatar user={tester} />
                    <div className="who">
                      <b>{tester?.name}</b>
                      <span>{request?.title} · {owner?.startup ?? owner?.name} · {request?.credits} token</span>
                    </div>
                  </div>

                  <p className="dispute-note">Kurucunun itirazı: {x.disputeNote}</p>

                  <div className="evidence">
                    <div>
                      <b>Test süresi</b>
                      <span>{x.durationMin ?? 0} dk</span>
                    </div>
                    <div>
                      <b>Ekran kaydı</b>
                      <span>
                        {x.proofUrl
                          ? <a href={x.proofUrl} target="_blank" rel="noreferrer">var</a>
                          : <span className="badge critical">yok</span>}
                      </span>
                    </div>
                    <div>
                      <b>Testçi itibarı</b>
                      <span>{reputation(s, x.testerId).avg.toFixed(1)}★</span>
                    </div>
                  </div>

                  {x.feedback && (
                    <div className="feedback">
                      {Object.entries(x.feedback)
                        .filter(([k]) => k !== "wouldUse" && k !== "wouldUseWhy")
                        .map(([k, v]) => (
                          <div key={k} className="fb-block">
                            <b>{k}</b>
                            <p>{v}</p>
                          </div>
                        ))}
                    </div>
                  )}

                  <div className="session-actions">
                    <button
                      className="btn primary sm"
                      disabled={busy}
                      onClick={() => run(() => api.resolveDispute(x.id, "release"))}
                    >
                      Testçiye ver (token serbest)
                    </button>
                    <button
                      className="btn ghost sm"
                      disabled={busy}
                      onClick={() => run(() => api.resolveDispute(x.id, "refund"))}
                    >
                      Kurucuya iade et (itibar cezası)
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Card>

      <Card title="Şüpheli Puanlama">
        <CollusionTable />
      </Card>

      <Card title="Üyeler">
        <table className="table">
          <thead><tr><th>Üye</th><th>Rol</th><th>Test</th><th>Puan</th><th>Katılım</th></tr></thead>
          <tbody>
            {activeUsers.map((u) => {
              const rep = reputation(s, u.id);
              return (
                <tr key={u.id}>
                  <td>
                    <div className="user-cell">
                      <Avatar user={u} size="sm" />
                      <div><b>{u.name}</b><span>{u.email}</span></div>
                    </div>
                  </td>
                  <td>{u.role === "founder" ? "Kurucu" : "Testçi"}</td>
                  <td>{rep.completed}</td>
                  <td>{rep.avg ? rep.avg.toFixed(1) : "—"}</td>
                  <td>{subtitleOf(u)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </Card>
    </>
  );
}

/**
 * Karşılıklı yüksek puan deseni: iki kullanıcı birbirini tekrar tekrar 5★ verirse
 * danışıklı puanlama şüphesi doğar. Karar vermez, yalnızca işaretler.
 */
function CollusionTable() {
  const s = useAppState();
  const pairs = new Map<string, { a: string; b: string; ratings: number[] }>();

  for (const session of s.sessions) {
    if (session.status !== "accepted" || !session.rating) continue;
    const request = getRequest(s, session.requestId);
    if (!request) continue;

    const [a, b] = [session.testerId, request.ownerId].sort();
    const key = `${a}|${b}`;
    const entry = pairs.get(key) ?? { a, b, ratings: [] };
    entry.ratings.push(session.rating);
    pairs.set(key, entry);
  }

  const flagged = [...pairs.values()]
    .filter((p) => p.ratings.length >= 2)
    .map((p) => ({
      ...p,
      avg: p.ratings.reduce((x, y) => x + y, 0) / p.ratings.length,
    }))
    .filter((p) => p.avg >= 4.5);

  if (flagged.length === 0) return <Empty>Şüpheli desen bulunmadı.</Empty>;

  return (
    <>
      <p className="card-hint">
        Karşılıklı en az 2 test ve ortalama 4.5★ üzeri — otomatik bir karar değil, incelenmesi gereken desen.
      </p>
      <table className="table">
        <thead><tr><th>Taraf A</th><th>Taraf B</th><th>Test</th><th>Ortalama</th><th>Risk</th></tr></thead>
        <tbody>
          {flagged.map((p) => (
            <tr key={`${p.a}|${p.b}`}>
              <td>{getUser(s, p.a)?.name}</td>
              <td>{getUser(s, p.b)?.name}</td>
              <td>{p.ratings.length}</td>
              <td>{p.avg.toFixed(1)}★</td>
              <td>
                <span className={`badge ${p.ratings.length >= 4 ? "critical" : "warn"}`}>
                  {p.ratings.length >= 4 ? "yüksek" : "izle"}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
