import { useBetakas } from "../state/BetakasProvider";

/**
 * Bildirimler. Metin sunucudan geldiği için düz metin olarak basılır —
 * vanilla sürümdeki innerHTML kullanımı bilinçli olarak bırakıldı.
 */
export function Toasts() {
  const { toasts, dismissToast } = useBetakas();

  return (
    <div id="toasts">
      {toasts.map((t) => (
        <div
          key={t.id}
          className={`toast ${t.kind}`}
          role="status"
          onClick={() => dismissToast(t.id)}
        >
          {t.message}
        </div>
      ))}
    </div>
  );
}
