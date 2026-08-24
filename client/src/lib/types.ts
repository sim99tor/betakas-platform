/**
 * Sunucudan gelen state'in şekli. Alanlar .NET tarafındaki `StateDto` ile birebir
 * eşleşir (server/Betakas.Api/Dto/StateDto.cs) — JSON camelCase olarak serileşir.
 */

export type Role = "founder" | "tester" | "admin";
export type UserStatus = "active" | "pending";

export interface Subscription {
  planId: string | null;
  renewsAt: number | null;
  active: boolean;
}

export interface User {
  id: string;
  name: string;
  initials: string;
  email: string;
  startup?: string | null;
  title?: string | null;
  tagline?: string | null;
  sector?: string | null;
  skills: string[];
  expertiseCategories: string[];
  expertiseOther?: string | null;
  role: Role;
  status: UserStatus;
  subscription?: Subscription | null;
  /** Yalnızca oturumdaki kullanıcının kendi kaydında dolu gelir. */
  iban?: string | null;
}

/** Bug numarası → tik durumu. */
export type FixMap = Record<string, FixState>;
export type FixState = "fixed" | "wip" | "later" | "norepro";

export interface ProductVersion {
  id: string;
  ownerId: string;
  label: string;
  url?: string | null;
  createdAt: number;
  notes?: string | null;
  fixes?: FixMap | null;
}

export type RequestStatus = "open" | "closed";
export type Visibility = "public" | "exclude-sector";

export interface TestRequest {
  id: string;
  ownerId: string;
  versionId?: string | null;
  title: string;
  url?: string | null;
  productCategory?: string | null;
  stage?: string | null;
  feedbackType?: string | null;
  scenario?: string | null;
  credits: number;
  slots: number;
  visibility: Visibility;
  status: RequestStatus;
  createdAt: number;
  boosted: boolean;
  boostedAt?: number | null;
}

/** Aşamaya göre alanları değişen serbest obje (firstImpression / bugs / ux / dropOff …). */
export type Feedback = Record<string, string>;

export type SessionStatus =
  | "applied" | "approved" | "submitted" | "accepted" | "disputed" | "rejected";

export interface TestSession {
  id: string;
  requestId: string;
  testerId: string;
  status: SessionStatus;
  appliedAt?: number | null;
  submittedAt?: number | null;
  rating?: number | null;
  ownerRating?: number | null;
  durationMin?: number | null;
  proofUrl?: string | null;
  feedback?: Feedback | null;
  disputeNote?: string | null;
  disputeOutcome?: "release" | "refund" | null;
  cashPaid?: number | null;
}

export interface LedgerEntry {
  id: string;
  ts: number;
  /** Kullanıcı id'si ya da sanal hesap: system | escrow */
  from: string;
  to: string;
  amount: number;
  type: string;
  ref?: string | null;
  note?: string | null;
}

export interface CashLedgerEntry {
  id: string;
  ts: number;
  /** Kullanıcı id'si ya da sanal hesap: revenue | pool | bank */
  from: string;
  to: string;
  amount: number;
  type: string;
  ref?: string | null;
  note?: string | null;
}

export interface Purchase {
  id: string;
  ts: number;
  userId: string;
  kind?: string | null;
  packageId: string;
  packageName: string;
  tokens: number;
  testers: number;
  gross: number;
  fee: number;
  pool: number;
  invoiceNo: string;
}

export interface Withdrawal {
  id: string;
  userId: string;
  amount: number;
  status: "pending" | "paid" | "rejected";
  requestedAt: number;
  resolvedAt?: number | null;
  iban?: string | null;
  note?: string | null;
}

export interface Settings {
  tokenPrice: number;
  feePct: number;
}

/** Giriş ekranındaki hesap kartları — yalnızca public state'te gelir. */
export interface DemoAccount {
  id: string;
  name: string;
  initials: string;
  email: string;
  role: Role;
  subtitle: string | null;
  completed: number;
  avg: number;
  balance: number;
}

export interface AppState {
  rev: number;
  version: number;
  authUserId: string | null;
  settings: Settings;
  sprintNo: number;
  seq: number;
  users: User[];
  versions: ProductVersion[];
  requests: TestRequest[];
  sessions: TestSession[];
  ledger: LedgerEntry[];
  cashLedger: CashLedgerEntry[];
  purchases: Purchase[];
  withdrawals: Withdrawal[];
  /** Yalnızca /api/public/state yanıtında bulunur. */
  demoAccounts?: DemoAccount[];
}

/** Domain uçlarının ortak yanıtı. */
export interface ActionResponse {
  message: string | null;
  state: AppState;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: User;
}

/** API hatası — sunucunun döndürdüğü mesaj ve HTTP durumu taşınır. */
export class ApiError extends Error {
  status: number;
  data: Record<string, unknown>;

  constructor(message: string, status: number, data: Record<string, unknown> = {}) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.data = data;
  }
}
