import { useState } from "react";
import { useParams } from "react-router-dom";
import { Avatar } from "../components/Avatar";
import { Card, Empty, PageHead, SessionStatusBadge, Tile, Tiles } from "../components/ui";
import { api } from "../lib/api";
import { BOOST_COST, FEEDBACK_TEMPLATES, DEFAULT_STAGE, FIX_STATES } from "../lib/constants";
import { changelogOf } from "../lib/bugs";
import {
  balance, escrowRemaining, fmtDate, fmtTRY, getUser, getVersion, payoutRate,
  repMultiplier, slotsLeft, slotsTaken, stageOf, testedEarlierVersions,
} from "../lib/derive";
import type { TestSession } from "../lib/types";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function RequestDetail() {
  const { requestId } = useParams();
  const s = useAppState();
  const { me, run, busy } = useBetakas();

  const request = s.requests.find((r) => r.id === requestId);
  if (!request || !me) return <Card><Empty>Talep bulunamadı.</Empty></Card>;

  const owner = getUser(s, request.ownerId);
  const version = getVersion(s, request.versionId);
  const isOwner = request.ownerId === me.id;
  const sessions = s.sessions.filter((x) => x.requestId === request.id);
  const mySession = sessions.find((x) => x.testerId === me.id && x.status !== "rejected");
  const escrow = escrowRemaining(s, request.id);
  const changelog = version ? changelogOf(s, version) : null;
  const earlier = version ? testedEarlierVersions(s, me.id, version.id) : [];

  const canApply =
    !isOwner && !mySession && request.status === "open" && slotsLeft(s, request) > 0 && me.status === "active";

  return (
    <>
      <PageHead
        title={request.title}
        sub={
          <>
            {owner?.startup ?? owner?.name} · {request.productCategory} · {request.feedbackType}
            {version && <> · <span className="ver-badge">{version.label}</span></>}
          </>
        }
        actions={
          isOwner && request.status === "open" && (
            <>
              {!request.boosted && (
                <button
                  className="btn ghost"
                  disabled={busy || balance(s, me.id) < BOOST_COST}
                  onClick={() => {
                    if (window.confirm(`Bu talebi ${BOOST_COST} token karşılığında Keşfet'te öne çıkarmak istiyor musun? Bu token harcanır, iade edilmez.`)) {
                      run(() => api.boostRequest(request.id));
                    }
                  }}
                >
                  Öne çıkar ({BOOST_COST} token)
                </button>
              )}
              <button
                className="btn ghost"
                disabled={busy}
                onClick={() => {
                  if (window.confirm(`Talep kapatılsın mı? Kalan ${escrow} token iade edilecek.`)) {
                    run(() => api.closeRequest(request.id));
                  }
                }}
              >
                Talebi Kapat
              </button>
            </>
          )
        }
      />

      <Tiles>
        <Tile cap="Slot" value={`${slotsTaken(s, request.id)}/${request.slots}`} foot="dolu" />
        <Tile cap="Slot Başına" value={`${request.credits} token`} tone="credit" />
        <Tile cap="Escrow'da Bloke" value={escrow} tone="warn" />
        <Tile
          cap="Durum"
          value={request.status === "open" ? "Açık" : "Kapalı"}
          tone={request.status === "open" ? "ok" : undefined}
        />
      </Tiles>

      <Card title="Senaryo">
        <p>{request.scenario}</p>
        {request.url && (
          <p>
            Test edilecek link:{" "}
            <a href={request.url} target="_blank" rel="noreferrer">{request.url}</a>
          </p>
        )}
        {request.visibility === "exclude-sector" && (
          <p className="help">Bu talep aynı sektördeki kurucular için kapalıdır (rakip gizliliği).</p>
        )}
      </Card>

      {changelog && !isOwner && (
        <Card title={`Bu sürümde ne değişti (${changelog.source.label} → ${version!.label})`}>
          <p className="card-hint">
            Kurucunun kapattığını söylediği maddeler — regresyon testi için başlangıç noktan.
          </p>
          <ul className="fix-list">
            {changelog.rows.filter((r) => r.state === "fixed").map(({ bug }) => (
              <li key={bug.id}>{FIX_STATES.fixed.icon} #{bug.no} {bug.text}</li>
            ))}
          </ul>
          {earlier.length > 0 && (
            <p className="help">
              Bu ürünün {earlier.map((v) => v.label).join(", ")} sürümünü test etmiştin — regresyon testçisisin.
            </p>
          )}
        </Card>
      )}

      {canApply && (
        <Card>
          <div className="apply-bar">
            <div>
              <b>Bu teste başvur</b>
              <span>
                Kazancın ≈ {Math.round(request.credits * repMultiplier(s, me.id))} token
                {me.role === "tester" && ` + ${fmtTRY(request.credits * payoutRate(s))} nakit`}
              </span>
            </div>
            <button className="btn primary" disabled={busy} onClick={() => run(() => api.apply(request.id))}>
              Başvur
            </button>
          </div>
        </Card>
      )}

      {mySession && !isOwner && (
        <MySessionCard session={mySession} stage={stageOf(request)} />
      )}

      {isOwner && (
        <Card title={`Başvurular ve Teslimler (${sessions.length})`}>
          {sessions.length === 0 ? (
            <Empty>Henüz başvuru yok.</Empty>
          ) : (
            <div className="session-list">
              {sessions.map((x) => (
                <OwnerSessionRow key={x.id} session={x} credits={request.credits} />
              ))}
            </div>
          )}
        </Card>
      )}
    </>
  );
}

// ---------------- Testçi tarafı ----------------

function MySessionCard({ session, stage }: { session: TestSession; stage: string }) {
  const { run, busy } = useBetakas();
  const template = FEEDBACK_TEMPLATES[stage] ?? FEEDBACK_TEMPLATES[DEFAULT_STAGE];

  const [fields, setFields] = useState<Record<string, string>>({});
  const [wouldUse, setWouldUse] = useState("evet");
  const [why, setWhy] = useState("");
  const [proof, setProof] = useState("");
  const [duration, setDuration] = useState(20);

  if (session.status === "applied") {
    return (
      <Card title="Başvurun">
        <p>Talep sahibi onayını bekliyor. Onaylanınca testi yapıp teslim edebilirsin.</p>
      </Card>
    );
  }

  if (session.status !== "approved") {
    return (
      <Card title="Teslimin">
        <div className="session-head">
          <SessionStatusBadge status={session.status} />
          {session.rating && <span className="badge ok">{session.rating}★</span>}
          {session.cashPaid ? <span className="badge ok">{fmtTRY(session.cashPaid)} ödendi</span> : null}
        </div>
        {session.feedback && <FeedbackView feedback={session.feedback} stage={stage} />}

        {session.status === "accepted" && !session.ownerRating && (
          <div className="rate-bar">
            <span>Talep sahibini değerlendir:</span>
            {[1, 2, 3, 4, 5].map((n) => (
              <button
                key={n}
                className="btn ghost sm"
                disabled={busy}
                onClick={() => run(() => api.rateOwner(session.id, n))}
              >
                {n}★
              </button>
            ))}
          </div>
        )}
      </Card>
    );
  }

  return (
    <Card title="Testi Teslim Et" className="form-card">
      <p className="card-hint">{template.hint}</p>

      {template.fields.map((f) => {
        const value = fields[f.key] ?? "";
        const short = value.trim().length < f.min;
        return (
          <div className="field" key={f.key}>
            <label>{f.label} <span className="req-star">*</span></label>
            <textarea
              rows={4}
              value={value}
              placeholder={f.placeholder}
              onChange={(e) => setFields((prev) => ({ ...prev, [f.key]: e.target.value }))}
            />
            <div className={`counter ${short ? "bad" : "ok"}`}>
              {value.trim().length} / en az {f.min} karakter
            </div>
          </div>
        );
      })}

      <div className="field">
        <label>{template.choice.label}</label>
        <div className="choice-row">
          {template.choice.options.map((o) => (
            <button
              key={o.v}
              className={`cat-chip ${wouldUse === o.v ? "on" : ""}`}
              onClick={() => setWouldUse(o.v)}
            >
              {o.l}
            </button>
          ))}
        </div>
      </div>

      <div className="field">
        <label>{template.choice.whyLabel} <span className="req-star">*</span></label>
        <textarea rows={2} value={why} onChange={(e) => setWhy(e.target.value)} />
      </div>

      <div className="two-col">
        <div className="field">
          <label>Ekran kaydı linki <span className="req-star">*</span></label>
          <input
            value={proof}
            onChange={(e) => setProof(e.target.value)}
            placeholder="https://loom.com/share/..."
          />
          <div className="help">Test kanıtı zorunludur.</div>
        </div>
        <div className="field">
          <label>Test süresi (dk) <span className="req-star">*</span></label>
          <input
            type="number"
            min={5}
            value={duration}
            onChange={(e) => setDuration(parseInt(e.target.value || "0", 10))}
          />
        </div>
      </div>

      <button
        className="btn primary lg"
        disabled={busy}
        onClick={() => run(() => api.submitFeedback(session.id, {
          fields, wouldUse, wouldUseWhy: why.trim(), proofUrl: proof.trim(), durationMin: duration,
        }))}
      >
        {busy ? "Gönderiliyor…" : "Teslim Et"}
      </button>
    </Card>
  );
}

// ---------------- Kurucu tarafı ----------------

function OwnerSessionRow({ session, credits }: { session: TestSession; credits: number }) {
  const s = useAppState();
  const { run, busy } = useBetakas();
  const tester = getUser(s, session.testerId);
  const request = s.requests.find((r) => r.id === session.requestId);

  return (
    <div className="session-item">
      <div className="session-head">
        <Avatar user={tester} />
        <div className="who">
          <b>{tester?.name}</b>
          <span>
            {session.submittedAt ? `Teslim: ${fmtDate(session.submittedAt)}` : `Başvuru: ${fmtDate(session.appliedAt)}`}
            {session.durationMin ? ` · ${session.durationMin} dk` : ""}
          </span>
        </div>
        <SessionStatusBadge status={session.status} />
      </div>

      {session.status === "applied" && (
        <div className="session-actions">
          <button className="btn primary sm" disabled={busy} onClick={() => run(() => api.approveApp(session.id))}>
            Onayla
          </button>
          <button className="btn ghost sm" disabled={busy} onClick={() => run(() => api.rejectApp(session.id))}>
            Reddet
          </button>
        </div>
      )}

      {session.proofUrl && (
        <p className="proof">
          Kanıt: <a href={session.proofUrl} target="_blank" rel="noreferrer">{session.proofUrl}</a>
        </p>
      )}

      {session.feedback && <FeedbackView feedback={session.feedback} stage={stageOf(request)} />}

      {session.status === "submitted" && (
        <div className="session-actions">
          <span>Puanla ve escrow'u serbest bırak:</span>
          {[1, 2, 3, 4, 5].map((n) => (
            <button
              key={n}
              className="btn primary sm"
              disabled={busy}
              onClick={() => run(() => api.acceptFeedback(session.id, n))}
              title={`${n} yıldız ver — ${credits} token serbest kalır`}
            >
              {n}★
            </button>
          ))}
          <button
            className="btn ghost sm"
            disabled={busy}
            onClick={() => {
              const note = window.prompt(
                "İtiraz nedeni (yönetime iletilecek):",
                "Feedback yüzeysel, test kanıtı yetersiz.",
              );
              if (note) run(() => api.disputeFeedback(session.id, note));
            }}
          >
            İtiraz Et
          </button>
        </div>
      )}

      {session.disputeNote && <p className="dispute-note">İtiraz: {session.disputeNote}</p>}
    </div>
  );
}

function FeedbackView({ feedback, stage }: { feedback: Record<string, string>; stage: string }) {
  const template = FEEDBACK_TEMPLATES[stage] ?? FEEDBACK_TEMPLATES[DEFAULT_STAGE];

  return (
    <div className="feedback">
      {template.fields.map((f) =>
        feedback[f.key] ? (
          <div key={f.key} className="fb-block">
            <b>{f.label}</b>
            <p>{feedback[f.key]}</p>
          </div>
        ) : null,
      )}
      {feedback.wouldUse && (
        <div className="fb-block">
          <b>{template.choice.label}</b>
          <p>
            <span className={`badge ${feedback.wouldUse === "evet" ? "ok" : feedback.wouldUse === "belki" ? "warn" : "critical"}`}>
              {template.choice.options.find((o) => o.v === feedback.wouldUse)?.l ?? feedback.wouldUse}
            </span>{" "}
            {feedback.wouldUseWhy}
          </p>
        </div>
      )}
    </div>
  );
}
