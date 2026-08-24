import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Card, Empty, PageHead, Tile, Tiles } from "../components/ui";
import { api } from "../lib/api";
import { changelogOf, versionBugs } from "../lib/bugs";
import { FIX_ORDER, FIX_STATES } from "../lib/constants";
import {
  fmtDate, getUser, getVersion, starStr, versionRequests, versionStatus, versionsOf,
} from "../lib/derive";
import type { FixMap, FixState } from "../lib/types";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function VersionDetail() {
  const { versionId } = useParams();
  const s = useAppState();
  const { me, run, busy } = useBetakas();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<FixMap>({});

  const version = getVersion(s, versionId);
  if (!version) return <Card><Empty>Sürüm bulunamadı.</Empty></Card>;

  const owner = getUser(s, version.ownerId);
  const isOwner = owner?.id === me?.id;
  const st = versionStatus(s, version.id);
  const log = changelogOf(s, version);
  const bugs = versionBugs(s, version.id);

  // Bu sürüme gelen onaylı testlerin istatistikleri.
  const requestIds = versionRequests(s, version.id).map((r) => r.id);
  const sessions = s.sessions.filter(
    (x) => x.status === "accepted" && requestIds.includes(x.requestId),
  );
  const avg = sessions.length
    ? sessions.reduce((a, x) => a + (x.rating ?? 0), 0) / sessions.length
    : 0;
  const wouldUse = sessions.filter((x) => x.feedback?.wouldUse === "evet").length;

  // Önceki sürümle karşılaştırma.
  const siblings = versionsOf(s, version.ownerId);
  const idx = siblings.findIndex((v) => v.id === version.id);
  const prev = idx > 0 ? siblings[idx - 1] : null;

  async function saveFixes() {
    const ok = await run(() => api.saveFixes(version!.id, { ...currentFixes(), ...draft }));
    if (ok) { setEditing(false); setDraft({}); }
  }

  function currentFixes(): FixMap {
    const map: FixMap = {};
    log?.rows.forEach((r) => { if (r.state) map[r.bug.id] = r.state; });
    return map;
  }

  return (
    <>
      <PageHead
        title={`${version.label} — Sürüm Karnesi`}
        sub={<>{owner?.startup ?? owner?.name} · {fmtDate(version.createdAt)}</>}
        actions={<Link className="btn ghost" to="/surumler">← Sürümler</Link>}
      />

      <Tiles>
        <Tile cap="Durum" value={st.label} />
        <Tile cap="Onaylı Test" value={sessions.length} tone="credit" />
        <Tile
          cap="Ortalama Puan"
          value={avg ? avg.toFixed(1) : "—"}
          foot={avg ? starStr(avg) : "henüz puan yok"}
        />
        <Tile
          cap="Kullanırdım Oranı"
          value={sessions.length ? `%${Math.round((wouldUse / sessions.length) * 100)}` : "—"}
          tone="ok"
        />
      </Tiles>

      {version.url && (
        <Card title="Sürüm linki">
          <a href={version.url} target="_blank" rel="noreferrer">{version.url}</a>
        </Card>
      )}

      <Card title="Değişiklik Notu">
        <p>{version.notes}</p>
      </Card>

      {log && (
        <Card
          title="Sürüm Notları"
          actions={isOwner && (
            editing
              ? <>
                  <button className="btn ghost sm" onClick={() => { setEditing(false); setDraft({}); }}>Vazgeç</button>
                  <button className="btn primary sm" disabled={busy} onClick={saveFixes}>Kaydet</button>
                </>
              : <button className="btn ghost sm" onClick={() => setEditing(true)}>İşaretleri düzenle</button>
          )}
        >
          <p className="card-hint">
            {log.source.label} sürümünde bildirilen bug'lara bu sürümde ne olduğu.
          </p>

          <div className="progress">
            <div className="bar" style={{ width: `%${log.pct}`.replace("%", "") + "%" }} />
            <span>{log.fixed}/{log.total} bug düzeltildi · %{log.pct}</span>
          </div>

          <table className="table">
            <thead>
              <tr><th>#</th><th>Bildiren</th><th>Bug</th><th>Durum</th></tr>
            </thead>
            <tbody>
              {log.rows.map(({ bug, state }) => {
                const shown = draft[bug.id] ?? state;
                return (
                  <tr key={bug.id}>
                    <td>#{bug.no}</td>
                    <td>{bug.reporter}</td>
                    <td>
                      {bug.text}
                      {bug.critical && <span className="badge critical">kritik</span>}
                    </td>
                    <td>
                      {editing ? (
                        <div className="fix-picker">
                          {FIX_ORDER.map((fs) => (
                            <button
                              key={fs}
                              className={`fix-opt ${shown === fs ? "on" : ""}`}
                              title={FIX_STATES[fs].label}
                              onClick={() => setDraft((d) => ({ ...d, [bug.id]: fs as FixState }))}
                            >
                              {FIX_STATES[fs].icon}
                            </button>
                          ))}
                        </div>
                      ) : shown ? (
                        <span className={`fix-status ${shown}`}>
                          {FIX_STATES[shown].icon} {FIX_STATES[shown].label}
                        </span>
                      ) : (
                        <span className="fix-status none">⬜ Açık</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </Card>
      )}

      <Card title={`Bu Sürümde Bildirilen Bug'lar (${bugs.length})`}>
        {bugs.length === 0 ? (
          <Empty>Bu sürüme henüz bug bildirilmedi.</Empty>
        ) : (
          <div className="bug-list">
            {bugs.map((bug) => (
              <div key={bug.id} className="bug-row">
                <span className="no">#{bug.no}</span>
                <div className="bug-text">
                  {bug.text}
                  <span className="bug-meta">
                    {bug.reporter}
                    {bug.critical && <span className="badge critical">kritik</span>}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      {prev && (
        <Card title={`Önceki Sürümle Karşılaştırma (${prev.label} → ${version.label})`}>
          <CompareTable s={s} prevId={prev.id} currentAvg={avg} currentCount={sessions.length} />
        </Card>
      )}
    </>
  );
}

function CompareTable({ s, prevId, currentAvg, currentCount }: {
  s: ReturnType<typeof useAppState>; prevId: string; currentAvg: number; currentCount: number;
}) {
  const prevRequestIds = versionRequests(s, prevId).map((r) => r.id);
  const prevSessions = s.sessions.filter(
    (x) => x.status === "accepted" && prevRequestIds.includes(x.requestId),
  );
  const prevAvg = prevSessions.length
    ? prevSessions.reduce((a, x) => a + (x.rating ?? 0), 0) / prevSessions.length
    : 0;

  const delta = currentAvg - prevAvg;

  return (
    <table className="table">
      <thead><tr><th>Ölçüt</th><th>Önceki</th><th>Bu sürüm</th><th>Değişim</th></tr></thead>
      <tbody>
        <tr>
          <td>Ortalama puan</td>
          <td>{prevAvg ? prevAvg.toFixed(1) : "—"}</td>
          <td>{currentAvg ? currentAvg.toFixed(1) : "—"}</td>
          <td className={delta > 0 ? "up" : delta < 0 ? "down" : ""}>
            {prevAvg && currentAvg ? `${delta > 0 ? "↑" : delta < 0 ? "↓" : "→"} ${Math.abs(delta).toFixed(1)}` : "—"}
          </td>
        </tr>
        <tr>
          <td>Onaylı test sayısı</td>
          <td>{prevSessions.length}</td>
          <td>{currentCount}</td>
          <td>—</td>
        </tr>
      </tbody>
    </table>
  );
}
