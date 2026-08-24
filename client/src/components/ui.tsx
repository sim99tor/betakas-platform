import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { STATUS_LABEL } from "../lib/constants";
import type { SessionStatus } from "../lib/types";

export function PageHead({ title, sub, actions }: {
  title: string; sub?: ReactNode; actions?: ReactNode;
}) {
  return (
    <div className="page-head">
      <div>
        <h1>{title}</h1>
        {sub && <p className="page-sub">{sub}</p>}
      </div>
      {actions && <div className="page-actions">{actions}</div>}
    </div>
  );
}

export function Tile({ cap, value, foot, tone }: {
  cap: string; value: ReactNode; foot?: ReactNode; tone?: "credit" | "warn" | "ok" | "violet";
}) {
  return (
    <div className="tile">
      <div className="cap">{cap}</div>
      <div className={`val ${tone ?? ""}`}>{value}</div>
      {foot && <div className="foot">{foot}</div>}
    </div>
  );
}

export function Tiles({ children }: { children: ReactNode }) {
  return <div className="tiles">{children}</div>;
}

export function Card({ title, actions, children, className }: {
  title?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string;
}) {
  return (
    <div className={`card ${className ?? ""}`}>
      {(title || actions) && (
        <div className="card-head">
          {title && <h2>{title}</h2>}
          {actions && <div className="card-actions">{actions}</div>}
        </div>
      )}
      {children}
    </div>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <div className="empty">{children}</div>;
}

export function Badge({ tone, children }: { tone?: string; children: ReactNode }) {
  return <span className={`badge ${tone ?? ""}`}>{children}</span>;
}

export function SessionStatusBadge({ status }: { status: SessionStatus }) {
  const tone: Record<SessionStatus, string> = {
    applied: "",
    approved: "credit",
    submitted: "warn",
    accepted: "ok",
    disputed: "critical",
    rejected: "muted",
  };
  return <Badge tone={tone[status]}>{STATUS_LABEL[status] ?? status}</Badge>;
}

export function RequestLink({ id, children }: { id: string; children: ReactNode }) {
  return <Link to={`/talep/${id}`}>{children}</Link>;
}
