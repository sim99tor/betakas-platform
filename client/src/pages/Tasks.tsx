import { Link } from "react-router-dom";
import { Card, Empty, PageHead, SessionStatusBadge, Tile, Tiles } from "../components/ui";
import {
  cashBalance, fmtDate, fmtTRY, getRequest, getUser, pendingEarnings,
  repMultiplier, testerLevel, totalEarned,
} from "../lib/derive";
import type { SessionStatus } from "../lib/types";
import { useAppState, useBetakas } from "../state/BetakasProvider";

const GROUPS: { status: SessionStatus; title: string; hint: string }[] = [
  { status: "approved", title: "Test Edilecek", hint: "Onaylandı — testi yapıp teslim et." },
  { status: "applied", title: "Onay Bekleyen Başvurular", hint: "Talep sahibi henüz onaylamadı." },
  { status: "submitted", title: "Onay Bekleyen Teslimler", hint: "Teslim ettin, kurucu puanlayacak." },
  { status: "disputed", title: "İtiraz Edilenler", hint: "Yönetim karara bağlayacak." },
  { status: "accepted", title: "Tamamlananlar", hint: "Kabul edildi, tokenin serbest kaldı." },
  { status: "rejected", title: "Reddedilenler", hint: "" },
];

export function Tasks() {
  const s = useAppState();
  const { me } = useBetakas();
  if (!me) return null;

  const mine = s.sessions.filter((x) => x.testerId === me.id);
  const level = testerLevel(s, me.id);

  return (
    <>
      <PageHead title="Görevlerim" sub={`${level.name} · ${level.done} tamamlanmış test`} />

      <Tiles>
        <Tile cap="Bekleyen Kazanç" value={pendingEarnings(s, me.id)} tone="warn" foot="escrow'da" />
        <Tile cap="Toplam Kazanılan Token" value={totalEarned(s, me.id)} tone="credit" />
        <Tile cap="Nakit Cüzdan" value={fmtTRY(cashBalance(s, me.id))} tone="ok" />
        <Tile
          cap="Seviye"
          value={level.name.replace(" Testçi", "")}
          foot={level.next ? `${level.next - level.done} test sonra yükseliyorsun` : "en üst seviye"}
        />
      </Tiles>

      {GROUPS.map((group) => {
        const rows = mine.filter((x) => x.status === group.status);
        if (rows.length === 0) return null;

        return (
          <Card key={group.status} title={`${group.title} (${rows.length})`}>
            {group.hint && <p className="card-hint">{group.hint}</p>}
            <div className="rows">
              {rows.map((x) => {
                const r = getRequest(s, x.requestId);
                const owner = getUser(s, r?.ownerId);
                return (
                  <Link key={x.id} to={`/talep/${x.requestId}`} className="row">
                    <div className="row-main">
                      <b>{r?.title}</b>
                      <span>
                        {owner?.startup ?? owner?.name} · {r?.credits} token
                        {x.status === "approved" && r &&
                          ` · kazancın ≈ ${Math.round(r.credits * repMultiplier(s, me.id))}`}
                        {x.submittedAt ? ` · teslim: ${fmtDate(x.submittedAt)}` : ""}
                      </span>
                    </div>
                    {x.rating ? <span className="badge ok">{x.rating}★</span> : null}
                    <SessionStatusBadge status={x.status} />
                  </Link>
                );
              })}
            </div>
          </Card>
        );
      })}

      {mine.length === 0 && (
        <Card>
          <Empty>Henüz bir göreve başvurmadın. <Link to="/kesfet">Açık Testler</Link>'e göz at.</Empty>
        </Card>
      )}
    </>
  );
}
