import { Link } from "react-router-dom";
import { Avatar } from "../components/Avatar";
import { Card, Empty, PageHead } from "../components/ui";
import { expertiseLabels, reputationBadge, starStr, subtitleOf, testerRanking } from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function Radar() {
  const s = useAppState();
  const { me } = useBetakas();
  const ranking = testerRanking(s);

  return (
    <>
      <PageHead
        title="Radar & Liderlik"
        sub="İtibar puanı ve sıralama tamamlanan testlerden otomatik hesaplanır."
      />

      <Card title="Liderlik Tablosu">
        {ranking.length === 0 ? (
          <Empty>Henüz tamamlanmış test yok.</Empty>
        ) : (
          <table className="table">
            <thead>
              <tr><th>#</th><th>Testçi</th><th>Uzmanlık</th><th>Test</th><th>Puan</th><th>Rozet</th></tr>
            </thead>
            <tbody>
              {ranking.map((entry) => {
                const badge = reputationBadge(entry);
                const isMe = entry.user.id === me?.id;
                return (
                  <tr key={entry.user.id} className={isMe ? "me" : ""}>
                    <td>{entry.position}</td>
                    <td>
                      <Link to={`/profil/${entry.user.id}`} className="user-cell">
                        <Avatar user={entry.user} size="sm" />
                        <div>
                          <b>{entry.user.name}</b>
                          <span>{subtitleOf(entry.user)}</span>
                        </div>
                      </Link>
                    </td>
                    <td>{expertiseLabels(entry.user).join(", ") || "—"}</td>
                    <td>{entry.rep.completed}</td>
                    <td>{entry.rep.avg.toFixed(1)} {starStr(entry.rep.avg)}</td>
                    <td>{badge && <span className={`badge ${badge.cls}`}>{badge.label}</span>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </Card>

      <Card title="Yatırımcı Radarı">
        <p className="card-hint">
          Yakında: aktif test alan ve sürüm çıkaran ürünler yatırımcılara sinyal olarak sunulacak.
          Şu an yalnızca ekosistem içi görünürlük var.
        </p>
        <div className="rows">
          {s.users
            .filter((u) => u.role === "founder" && u.status === "active")
            .map((u) => {
              const versions = s.versions.filter((v) => v.ownerId === u.id).length;
              const tests = s.sessions.filter(
                (x) => x.status === "accepted" &&
                  s.requests.some((r) => r.id === x.requestId && r.ownerId === u.id),
              ).length;
              return (
                <Link key={u.id} to={`/profil/${u.id}`} className="row">
                  <Avatar user={u} />
                  <div className="row-main">
                    <b>{u.startup}</b>
                    <span>{u.tagline}</span>
                  </div>
                  <span className="badge">{versions} sürüm</span>
                  <span className="badge credit">{tests} onaylı test</span>
                </Link>
              );
            })}
        </div>
      </Card>
    </>
  );
}
