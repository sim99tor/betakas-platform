import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Avatar } from "../components/Avatar";
import { Card, Empty, PageHead } from "../components/ui";
import { PRODUCT_CATEGORIES } from "../lib/constants";
import {
  categoryMatch, expertiseLabels, getUser, getVersion, hasMatchPriority,
  repMultiplier, skillMatches, slotsLeft, timeAgo,
} from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

/**
 * Açık talepler. Sıralama: uzmanlık eşleşmesi → öne çıkarma → abonelik önceliği → tarih.
 * Rakip gizliliği (aynı sektöre kapalı talep) burada da uygulanır; sunucu başvuruda
 * aynı kuralı ayrıca doğrular.
 */
export function Discover() {
  const s = useAppState();
  const { me } = useBetakas();
  const [category, setCategory] = useState<string>("");

  const list = useMemo(() => {
    if (!me) return [];

    return s.requests
      .filter((r) => r.status === "open" && r.ownerId !== me.id)
      .filter((r) => slotsLeft(s, r) > 0)
      .filter((r) => !s.sessions.some(
        (x) => x.requestId === r.id && x.testerId === me.id && x.status !== "rejected",
      ))
      .filter((r) => {
        if (r.visibility !== "exclude-sector") return true;
        const owner = getUser(s, r.ownerId);
        return !(me.sector && owner?.sector && me.sector === owner.sector);
      })
      .filter((r) => !category || r.productCategory === category)
      .sort((a, b) => {
        const match = Number(categoryMatch(me, b)) - Number(categoryMatch(me, a));
        if (match) return match;
        const boost = Number(b.boosted) - Number(a.boosted);
        if (boost) return boost;
        const priority = Number(hasMatchPriority(s, b.ownerId)) - Number(hasMatchPriority(s, a.ownerId));
        if (priority) return priority;
        return b.createdAt - a.createdAt;
      });
  }, [s, me, category]);

  if (!me) return null;

  const mine = expertiseLabels(me);
  const matching = list.filter((r) => categoryMatch(me, r)).length;

  return (
    <>
      <PageHead
        title="Açık Testler"
        sub="Başka kurucuların ürünlerini test et, token kazan. İtibar çarpanın kazancını doğrudan artırır."
      />

      {mine.length > 0 && (
        <div className="info-bar">
          Uzmanlık alanların: <b>{mine.join(" · ")}</b> — {matching} talep eşleşiyor.
        </div>
      )}

      <div className="filter-row">
        <button className={`cat-chip ${category === "" ? "on" : ""}`} onClick={() => setCategory("")}>
          Tümü
        </button>
        {PRODUCT_CATEGORIES.map((c) => (
          <button
            key={c}
            className={`cat-chip ${category === c ? "on" : ""}`}
            onClick={() => setCategory(c)}
          >
            {c}
          </button>
        ))}
      </div>

      {list.length === 0 ? (
        <Card><Empty>Şu an uygun açık test yok.</Empty></Card>
      ) : (
        <div className="grid-cards">
          {list.map((r) => {
            const owner = getUser(s, r.ownerId);
            const version = getVersion(s, r.versionId);
            const earns = Math.round(r.credits * repMultiplier(s, me.id));
            const skills = skillMatches(me, r);

            return (
              <Link key={r.id} to={`/talep/${r.id}`} className="discover-card">
                <div className="dc-head">
                  <Avatar user={owner} />
                  <div className="who">
                    <b>{owner?.startup ?? owner?.name}</b>
                    <span>{owner?.name} · {timeAgo(r.createdAt)}</span>
                  </div>
                  <span className="badge credit">{r.credits} token</span>
                </div>

                <h3>{r.title}</h3>
                <p className="dc-scenario">{r.scenario}</p>

                <div className="dc-tags">
                  <span className="badge cat">{r.productCategory}</span>
                  <span className="badge">{r.feedbackType}</span>
                  {version && <span className="ver-badge">{version.label}</span>}
                  {categoryMatch(me, r) && <span className="badge violet">Uzmanlığınla eşleşiyor</span>}
                  {r.boosted && <span className="badge warn">Öne çıkarıldı</span>}
                  {hasMatchPriority(s, r.ownerId) && <span className="badge ok">Öncelikli</span>}
                  {skills.map((sk) => <span key={sk} className="badge muted">{sk}</span>)}
                </div>

                <div className="dc-foot">
                  <span>{slotsLeft(s, r)} slot boş</span>
                  <span className="earn">Kazancın ≈ <b>{earns} token</b></span>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </>
  );
}
