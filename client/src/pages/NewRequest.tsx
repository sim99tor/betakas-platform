import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Card, Empty, PageHead } from "../components/ui";
import { api } from "../lib/api";
import {
  DEFAULT_STAGE, FEEDBACK_TEMPLATES, FEEDBACK_TYPES, PRODUCT_CATEGORIES,
  PRODUCT_STAGES, STAGE_SCENARIOS,
} from "../lib/constants";
import { balance, versionsOf } from "../lib/derive";
import { useAppState, useBetakas } from "../state/BetakasProvider";

export function NewRequest() {
  const s = useAppState();
  const { me, run, busy } = useBetakas();
  const navigate = useNavigate();
  const [params] = useSearchParams();

  const myVersions = versionsOf(s, me?.id ?? "");
  const [versionId, setVersionId] = useState(params.get("version") ?? myVersions[myVersions.length - 1]?.id ?? "");
  const [title, setTitle] = useState("");
  const [category, setCategory] = useState(PRODUCT_CATEGORIES[0]);
  const [stage, setStage] = useState(DEFAULT_STAGE);
  const [type, setType] = useState(FEEDBACK_TYPES[0]);
  const [scenario, setScenario] = useState("");
  const [credits, setCredits] = useState(15);
  const [slots, setSlots] = useState(2);
  const [excludeSector, setExcludeSector] = useState(false);

  if (!me) return null;

  const available = balance(s, me.id);
  const total = credits * slots;
  const enough = total <= available;
  const template = FEEDBACK_TEMPLATES[stage] ?? FEEDBACK_TEMPLATES[DEFAULT_STAGE];
  const selected = myVersions.find((v) => v.id === versionId);

  async function submit() {
    const ok = await run(() => api.createRequest({
      title: title.trim(),
      versionId,
      productCategory: category,
      stage,
      feedbackType: type,
      scenario: scenario.trim(),
      credits,
      slots,
      excludeSector,
    }));
    if (ok) navigate("/panel");
  }

  if (myVersions.length === 0) {
    return (
      <>
        <PageHead title="Yeni Test Talebi" />
        <Card>
          <Empty>
            Önce bir ürün sürümü çıkarmalısın — her test talebi belirli bir sürüme açılır.{" "}
            <Link to="/surumler">Ürün Sürümleri</Link>
          </Empty>
        </Card>
      </>
    );
  }

  return (
    <>
      <PageHead
        title="Yeni Test Talebi"
        sub="Tokenlerin escrow'a bloke edilir; testçi teslim edip sen onaylayınca serbest kalır."
      />

      <Card className="form-card">
        <div className="field">
          <label>Ürün sürümü <span className="req-star">*</span></label>
          <select value={versionId} onChange={(e) => setVersionId(e.target.value)}>
            {myVersions.map((v) => <option key={v.id} value={v.id}>{v.label}</option>)}
          </select>
          {selected?.url && <div className="help">Test edilecek link: {selected.url}</div>}
        </div>

        <div className="field">
          <label>Başlık <span className="req-star">*</span></label>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Örn: Yeni onboarding akışı"
          />
        </div>

        <div className="two-col">
          <div className="field">
            <label>Ürün kategorisi <span className="req-star">*</span></label>
            <select value={category} onChange={(e) => setCategory(e.target.value)}>
              {PRODUCT_CATEGORIES.map((c) => <option key={c}>{c}</option>)}
            </select>
            <div className="help">Testçi eşleştirmesi buna göre yapılır.</div>
          </div>

          <div className="field">
            <label>Ürün aşaması</label>
            <select value={stage} onChange={(e) => setStage(e.target.value)}>
              {PRODUCT_STAGES.map((x) => <option key={x}>{x}</option>)}
            </select>
            <div className="help">{template.hint}</div>
          </div>
        </div>

        <div className="field">
          <label>Feedback türü</label>
          <select value={type} onChange={(e) => setType(e.target.value)}>
            {FEEDBACK_TYPES.map((x) => <option key={x}>{x}</option>)}
          </select>
        </div>

        <div className="field">
          <label>Senaryo <span className="req-star">*</span></label>
          <textarea
            rows={4}
            value={scenario}
            onChange={(e) => setScenario(e.target.value)}
            placeholder="Testçiye ne yapmasını istiyorsun? Adım adım yaz."
          />
          {STAGE_SCENARIOS[stage] && (
            <button className="link-btn" onClick={() => setScenario(STAGE_SCENARIOS[stage])}>
              Şablon kullan
            </button>
          )}
        </div>

        <div className="two-col">
          <div className="field">
            <label>Slot başına token</label>
            <input
              type="number"
              min={5}
              value={credits}
              onChange={(e) => setCredits(parseInt(e.target.value || "0", 10))}
            />
          </div>
          <div className="field">
            <label>Slot (kaç testçi)</label>
            <input
              type="number"
              min={1}
              max={20}
              value={slots}
              onChange={(e) => setSlots(parseInt(e.target.value || "0", 10))}
            />
          </div>
        </div>

        <label className="check">
          <input
            type="checkbox"
            checked={excludeSector}
            onChange={(e) => setExcludeSector(e.target.checked)}
          />
          Aynı sektördeki kurucular bu talebi görmesin (rakip gizliliği)
        </label>

        <div className={`escrow-preview ${enough ? "" : "bad"}`}>
          <div>
            <b>{total} token</b> escrow'a bloke edilecek
            <span> ({slots} slot × {credits} token)</span>
          </div>
          <div className={enough ? "ok" : "bad"}>
            Bakiyen: {available} token
            {!enough && " — yetersiz"}
          </div>
        </div>

        {!enough && (
          <p className="help">
            Test yaparak token kazanabilir ya da <Link to="/token-satin-al">paket satın alabilirsin</Link>.
          </p>
        )}

        <button className="btn primary lg" disabled={busy || !enough} onClick={submit}>
          {busy ? "Açılıyor…" : "Talebi Aç & Tokenleri Bloke Et"}
        </button>
      </Card>
    </>
  );
}
