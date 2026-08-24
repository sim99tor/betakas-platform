import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Avatar } from "../components/Avatar";
import { Card, Empty, PageHead, Tile, Tiles } from "../components/ui";
import { api } from "../lib/api";
import { OTHER_CATEGORY, PRODUCT_CATEGORIES } from "../lib/constants";
import {
  balance, expertiseLabels, fmtDate, founderRating, getRequest, getUser,
  rankOf, reputation, reputationBadge, starStr, subtitleOf, testerLevel, versionsOf,
} from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function Profile() {
  const { userId } = useParams();
  const s = useAppState();
  const { me, run, busy, toast } = useBetakas();

  const user = getUser(s, userId ?? me?.id);
  if (!user || !me) return <Card><Empty>Kullanıcı bulunamadı.</Empty></Card>;

  const isSelf = user.id === me.id;
  const rep = reputation(s, user.id);
  const rank = rankOf(s, user.id);
  const badge = reputationBadge(rank);
  const level = testerLevel(s, user.id);
  const asFounder = founderRating(s, user.id);

  const history = s.sessions
    .filter((x) => x.testerId === user.id && x.status === "accepted")
    .slice(-8).reverse();

  const publicUrl = `${window.location.origin}/t/${user.id}`;

  return (
    <>
      <PageHead
        title={user.name}
        sub={
          <>
            {subtitleOf(user)}
            {user.tagline && <> · {user.tagline}</>}
          </>
        }
        actions={<Avatar user={user} size="lg" />}
      />

      <Tiles>
        <Tile
          cap="İtibar"
          value={rep.avg ? rep.avg.toFixed(1) : "—"}
          foot={rep.avg ? starStr(rep.avg) : "henüz puan yok"}
        />
        <Tile cap="Tamamlanan Test" value={rep.completed} tone="credit" />
        {rank && <Tile cap="Sıralama" value={`${rank.position}/${rank.total}`} foot={`ilk %${rank.pct}`} />}
        {isSelf && <Tile cap="Token Bakiyesi" value={balance(s, user.id)} tone="ok" />}
      </Tiles>

      {badge && (
        <Card>
          <div className="badge-row">
            <span className={`badge ${badge.cls} lg`}>{badge.label}</span>
            <span>{level.name}</span>
            {user.role === "tester" && (
              <>
                <div className="spacer" />
                <button
                  className="btn ghost sm"
                  onClick={() => {
                    navigator.clipboard?.writeText(publicUrl);
                    toast("Public profil linki kopyalandı.", "good");
                  }}
                >
                  Public profil linkini kopyala
                </button>
                <Link className="btn ghost sm" to={`/t/${user.id}`}>Public profili gör</Link>
              </>
            )}
          </div>
        </Card>
      )}

      {asFounder.count > 0 && (
        <Card title="Kurucu Olarak Aldığı Puan">
          <p>
            {asFounder.avg.toFixed(1)} {starStr(asFounder.avg)} · {asFounder.count} testçi değerlendirmesi
          </p>
          <p className="help">Testçiler senaryonun ve sürecin kalitesini böyle değerlendirdi.</p>
        </Card>
      )}

      {isSelf && user.role !== "admin" && (
        <ExpertiseEditor
          initial={user.expertiseCategories}
          initialOther={user.expertiseOther ?? ""}
          busy={busy}
          onSave={(categories, other) => run(() => api.saveExpertise(categories, other))}
        />
      )}

      {!isSelf && (
        <Card title="Uzmanlık Alanları">
          <div className="cat-picker">
            {expertiseLabels(user).map((c) => <span key={c} className="cat-chip on">{c}</span>)}
            {expertiseLabels(user).length === 0 && <Empty>Belirtilmemiş.</Empty>}
          </div>
        </Card>
      )}

      {user.role === "founder" && (
        <Card title="Ürün Sürümleri">
          {versionsOf(s, user.id).length === 0 ? (
            <Empty>Henüz sürüm yok.</Empty>
          ) : (
            <div className="rows">
              {versionsOf(s, user.id).slice().reverse().map((v) => (
                <Link key={v.id} to={`/surum/${v.id}`} className="row">
                  <span className="ver-badge">{v.label}</span>
                  <div className="row-main">
                    <b>{v.notes?.slice(0, 90)}</b>
                    <span>{fmtDate(v.createdAt)}</span>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </Card>
      )}

      <Card title="Test Geçmişi">
        {history.length === 0 ? (
          <Empty>Henüz tamamlanmış test yok.</Empty>
        ) : (
          <table className="table">
            <thead><tr><th>Ürün</th><th>Kategori</th><th>Tür</th><th>Puan</th><th>Tarih</th></tr></thead>
            <tbody>
              {history.map((x) => {
                const r = getRequest(s, x.requestId);
                return (
                  <tr key={x.id}>
                    <td>{r?.title}</td>
                    <td>{r?.productCategory}</td>
                    <td>{r?.feedbackType}</td>
                    <td>{x.rating}★</td>
                    <td>{fmtDate(x.submittedAt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </Card>
    </>
  );
}

function ExpertiseEditor({ initial, initialOther, busy, onSave }: {
  initial: string[]; initialOther: string; busy: boolean;
  onSave: (categories: string[], other: string | null) => void;
}) {
  const [picked, setPicked] = useState<string[]>(initial);
  const [other, setOther] = useState(initialOther);

  const toggle = (c: string) =>
    setPicked((list) => (list.includes(c) ? list.filter((x) => x !== c) : [...list, c]));

  return (
    <Card title="Uzmanlık Alanların">
      <p className="card-hint">
        Açık Testler sayfasında bu kategorilerdeki talepler listenin başında gösterilir.
      </p>
      <div className="cat-picker">
        {PRODUCT_CATEGORIES.map((c) => (
          <button key={c} className={`cat-chip ${picked.includes(c) ? "on" : ""}`} onClick={() => toggle(c)}>
            {c}
          </button>
        ))}
      </div>

      {picked.includes(OTHER_CATEGORY) && (
        <input
          style={{ marginTop: 10 }}
          maxLength={60}
          value={other}
          onChange={(e) => setOther(e.target.value)}
          placeholder="Örn: Oyun, IoT, Masaüstü yazılım"
        />
      )}

      <button
        className="btn primary"
        style={{ marginTop: 12 }}
        disabled={busy || picked.length === 0}
        onClick={() => onSave(picked, other.trim() || null)}
      >
        Kaydet
      </button>
    </Card>
  );
}
