import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Avatar } from "../components/Avatar";
import { Card, Empty, Tile, Tiles } from "../components/ui";
import { api } from "../lib/api";
import {
  expertiseLabels, getRequest, getUser, rankOf, reputation,
  reputationBadge, starStr, subtitleOf, testerLevel,
} from "../lib/derive";
import type { AppState } from "../lib/types";

/**
 * Giriş gerektirmeyen, paylaşılabilir testçi profili (#/t/<id> yerine /t/<id>).
 * Redakte public state'ten beslenir — feedback metinleri ve e-postalar bu yükte yoktur.
 */
export function PublicProfile() {
  const { userId } = useParams();
  const [state, setState] = useState<AppState | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    api.getPublicState()
      .then(setState)
      .catch((err) => setError(err instanceof Error ? err.message : "Yüklenemedi."));
  }, []);

  if (error) return <div className="public-wrap"><Card><Empty>{error}</Empty></Card></div>;
  if (!state) return <div className="public-wrap"><Card><Empty>Yükleniyor…</Empty></Card></div>;

  const user = getUser(state, userId);
  const rep = user ? reputation(state, user.id) : null;

  if (!user || !rep || rep.completed === 0) {
    return (
      <div className="public-wrap">
        <Card>
          <Empty>
            Bu profil herkese açık değil. Testçi en az bir onaylı test tamamlayınca profili açılır.
          </Empty>
        </Card>
      </div>
    );
  }

  const rank = rankOf(state, user.id);
  const badge = reputationBadge(rank);
  const level = testerLevel(state, user.id);

  const history = state.sessions
    .filter((x) => x.testerId === user.id && x.status === "accepted")
    .slice(-8).reverse();

  const shareText =
    `Betakas'ta ${rep.completed} test tamamladım, itibar puanım ${rep.avg.toFixed(1)}/5` +
    (rank ? ` — Top %${rank.pct} testçi` : "");

  return (
    <div className="public-wrap">
      <div className="public-head">
        <div className="brand"><span className="logo">B</span> Betakas</div>
        <Link className="btn ghost sm" to="/giris">Giriş yap</Link>
      </div>

      <Card>
        <div className="public-profile">
          <Avatar user={user} size="lg" />
          <div>
            <h1>{user.name}</h1>
            <p>{subtitleOf(user)}{user.tagline && ` · ${user.tagline}`}</p>
            {badge && <span className={`badge ${badge.cls} lg`}>{badge.label}</span>}
          </div>
        </div>
      </Card>

      <Tiles>
        <Tile cap="İtibar" value={rep.avg.toFixed(1)} foot={starStr(rep.avg)} />
        <Tile cap="Tamamlanan Test" value={rep.completed} tone="credit" />
        {rank && <Tile cap="Sıralama" value={`${rank.position}/${rank.total}`} foot={`ilk %${rank.pct}`} />}
        <Tile cap="Seviye" value={level.name.replace(" Testçi", "")} />
      </Tiles>

      {expertiseLabels(user).length > 0 && (
        <Card title="Uzmanlık Alanları">
          <div className="cat-picker">
            {expertiseLabels(user).map((c) => <span key={c} className="cat-chip on">{c}</span>)}
          </div>
        </Card>
      )}

      <Card title="Son Testler">
        <table className="table">
          <thead><tr><th>Ürün</th><th>Kategori</th><th>Tür</th><th>Puan</th></tr></thead>
          <tbody>
            {history.map((x) => {
              const r = getRequest(state, x.requestId);
              return (
                <tr key={x.id}>
                  <td>{r?.title}</td>
                  <td>{r?.productCategory}</td>
                  <td>{r?.feedbackType}</td>
                  <td>{x.rating}★</td>
                </tr>
              );
            })}
          </tbody>
        </table>
        <p className="help">Feedback metinleri gizlidir; yalnızca puan ve kategori gösterilir.</p>
      </Card>

      <Card title="Paylaş">
        <div className="share-row">
          <button
            className="btn ghost sm"
            onClick={() => navigator.clipboard?.writeText(window.location.href)}
          >
            Linki kopyala
          </button>
          <button
            className="btn ghost sm"
            onClick={() => navigator.clipboard?.writeText(`${shareText}\n${window.location.href}`)}
          >
            Panoya kopyala (metin)
          </button>
          <a
            className="btn ghost sm"
            href={`https://www.linkedin.com/sharing/share-offsite/?url=${encodeURIComponent(window.location.href)}`}
            target="_blank"
            rel="noreferrer"
          >
            LinkedIn'de paylaş
          </a>
        </div>
      </Card>

      <div className="landing-footer">
        Betakas · Bu profil herkese açıktır, görüntülemek için giriş gerekmez.
        İtibar puanı ve sıralama tamamlanan testlerden otomatik hesaplanır.
      </div>
    </div>
  );
}
