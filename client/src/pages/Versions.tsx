import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Card, Empty, PageHead } from "../components/ui";
import { api } from "../lib/api";
import { FIX_ORDER, FIX_STATES } from "../lib/constants";
import { changelogOf, isClosed, pendingBugs } from "../lib/bugs";
import { fmtDate, nextVersionLabels, versionRequests, versionStatus, versionsOf } from "../lib/derive";
import type { FixMap, FixState } from "../lib/types";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function Versions() {
  const s = useAppState();
  const { me, run, busy } = useBetakas();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  if (!me) return null;
  const mine = versionsOf(s, me.id);
  const latest = mine[mine.length - 1];
  const suggestions = latest ? nextVersionLabels(latest.label) : { patch: "v1.0", major: "v1.0" };

  return (
    <>
      <PageHead
        title="Ürün Sürümleri"
        sub="Her test talebi belirli bir sürüme açılır. Sürümler arası karşılaştırma gelişimini gösterir."
        actions={
          <button className="btn primary" onClick={() => setOpen((v) => !v)}>
            {open ? "Vazgeç" : "Yeni Sürüm Çıkar"}
          </button>
        }
      />

      {open && (
        <NewVersionForm
          suggestions={suggestions}
          busy={busy}
          onSubmit={async (payload) => {
            const ok = await run(() => api.createVersion(payload));
            if (ok) setOpen(false);
          }}
        />
      )}

      {mine.length === 0 ? (
        <Card><Empty>Henüz sürüm çıkarmadın. İlk sürümünü ekleyip teste sokabilirsin.</Empty></Card>
      ) : (
        <div className="timeline">
          {[...mine].reverse().map((v) => {
            const st = versionStatus(s, v.id);
            const requests = versionRequests(s, v.id);
            const log = changelogOf(s, v);

            return (
              <div key={v.id} className="tl-item">
                <div className="tl-dot" />
                <Card className="tl-card">
                  <div className="tl-head">
                    <span className="ver-badge lg">{v.label}</span>
                    <span className={`badge ${st.cls}`}>{st.label}</span>
                    <span className="tl-date">{fmtDate(v.createdAt)}</span>
                    <div className="spacer" />
                    {log && (
                      <span className="badge ok">{log.fixed}/{log.total} düzeltildi</span>
                    )}
                    <Link className="btn ghost sm" to={`/surum/${v.id}`}>Karne</Link>
                    <button className="btn ghost sm" onClick={() => navigate(`/yeni-talep?version=${v.id}`)}>
                      Teste Sok
                    </button>
                  </div>

                  {v.url && <a className="tl-url" href={v.url} target="_blank" rel="noreferrer">{v.url}</a>}
                  <p className="tl-notes">{v.notes}</p>
                  <div className="tl-meta">{requests.length} test talebi</div>
                </Card>
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}

// ---------------- Yeni sürüm formu ----------------

interface NewVersionPayload {
  label: string;
  url: string;
  notes: string;
  fixes: FixMap;
}

function NewVersionForm({ suggestions, busy, onSubmit }: {
  suggestions: { patch: string; major: string };
  busy: boolean;
  onSubmit: (p: NewVersionPayload) => void;
}) {
  const s = useAppState();
  const { me } = useBetakas();

  const [label, setLabel] = useState(suggestions.patch);
  const [url, setUrl] = useState("");
  const [notes, setNotes] = useState("");
  const [fixes, setFixes] = useState<FixMap>({});

  const pending = me ? pendingBugs(s, me.id) : null;
  const openRows = pending?.rows.filter((r) => !isClosed(r.state)) ?? [];
  const closedCount = (pending?.rows.length ?? 0) - openRows.length;

  const setFix = (id: string, state: FixState) =>
    setFixes((f) => ({ ...f, [id]: state }));

  return (
    <Card title="Yeni Sürüm Çıkar" className="form-card">
      <div className="field">
        <label>Sürüm etiketi <span className="req-star">*</span></label>
        <input value={label} onChange={(e) => setLabel(e.target.value)} placeholder="v1.1" />
        <div className="help">
          Öneriler:{" "}
          <button className="link-btn" onClick={() => setLabel(suggestions.patch)}>{suggestions.patch}</button>
          {" · "}
          <button className="link-btn" onClick={() => setLabel(suggestions.major)}>{suggestions.major}</button>
        </div>
      </div>

      <div className="field">
        <label>Sürüm linki <span className="req-star">*</span></label>
        <input value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://urunun.app" />
      </div>

      <div className="field">
        <label>Değişiklik notu <span className="req-star">*</span></label>
        <textarea
          rows={3}
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          placeholder="Bu sürümde ne değişti? Önceki testlerde çıkan hangi sorunu hedefledin?"
        />
      </div>

      {openRows.length > 0 && (
        <div className="field">
          <label>{pending!.source.label} sürümünde açık kalan bug'lar</label>
          <p className="help">
            Bu sürümde neyi kapattığını işaretle — sürüm notuna gömülür ve testçiye gösterilir.
            {closedCount > 0 && ` (${closedCount} tanesi önceki sürümlerde kapatıldı.)`}
          </p>

          <div className="fix-actions">
            <button
              className="btn ghost sm"
              onClick={() => setFixes((f) => {
                const next = { ...f };
                openRows.forEach((r) => { next[r.bug.id] = "fixed"; });
                return next;
              })}
            >
              Tümünü ✅ işaretle
            </button>
          </div>

          <div className="bug-list">
            {openRows.map(({ bug, state }) => (
              <div key={bug.id} className="bug-row">
                <span className="no">#{bug.no}</span>
                <div className="bug-text">
                  {bug.text}
                  <span className="bug-meta">
                    {bug.reporter}{bug.critical && <span className="badge critical">kritik</span>}
                  </span>
                </div>
                <div className="fix-picker">
                  {FIX_ORDER.map((fs) => (
                    <button
                      key={fs}
                      className={`fix-opt ${(fixes[bug.id] ?? state) === fs ? "on" : ""}`}
                      title={FIX_STATES[fs].label}
                      onClick={() => setFix(bug.id, fs)}
                    >
                      {FIX_STATES[fs].icon}
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <button
        className="btn primary lg"
        disabled={busy}
        onClick={() => onSubmit({ label: label.trim(), url: url.trim(), notes: notes.trim(), fixes })}
      >
        {busy ? "Oluşturuluyor…" : "Sürümü Oluştur"}
      </button>
    </Card>
  );
}
