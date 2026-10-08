export interface Account {
  id: string;
  name: string;
  role: string;
  unit: string;
  providerId: number | null;
  active: boolean;
  readAccess: string;
  writeAccess: string;
  version: number;
}
export interface Session {
  demo: boolean;
  user: Account | null;
  accounts: Account[];
}
export interface CreationOptions {
  processes: Process[];
  providers: Provider[];
  eligibility: { processVersionId: number; providerIds: number[] }[];
}
export interface Provider {
  id: number;
  name: string;
  registration: string;
  unit: string;
  branches: string;
  contactName: string;
  email: string;
  phone: string;
  agreement: string;
  version: number;
}
export interface Field {
  key: string;
  label: string;
  type: string;
  required: boolean;
  viewRoles: string[];
  editRoles: string[];
  editStates: string[];
}
export interface State {
  key: string;
  label: string;
  terminal: boolean;
}
export interface Transition {
  key: string;
  label: string;
  from: string;
  to: string;
  roles: string[];
  guard: string;
  effects: string[];
  routes?: PayloadRoute[] | null;
  businessAction?: { key: string; inputs: Record<string, string>; success: string; failure: string } | null;
  trigger?: 'user' | 'businessSuccess' | 'businessFailure';
}
export interface BusinessOperation {
  id: number;
  status: 'pending' | 'sent' | 'blocked';
  result: { success: boolean; message: string; data: Record<string, string> | null } | null;
}
export interface Definition {
  name: string;
  initialState: string;
  fields: Field[];
  states: State[];
  transitions: Transition[];
  documents: string[];
}
export interface Process {
  id: number;
  key: string;
  number: number;
  publishedAt: string;
  definition: Definition;
}
export interface CaseSummary {
  id: number;
  title: string;
  providerId: number;
  providerName: string;
  state: string;
  stateLabel: string;
  processName: string;
  round: number;
  unit: string;
  assigneeId: string | null;
  updatedAt: string;
}
export interface ReviewTask {
  id: number;
  caseId: number;
  documentId: number;
  round: number;
  status: string;
  result: string | null;
  reviewerId: string | null;
  note: string;
  dueAt: string;
}
export interface Document {
  id: number;
  kind: string;
  number: number;
  fileName: string;
  size: number;
  validUntil: string | null;
  uploadedAt: string;
  current: boolean;
  valid: boolean;
}
export interface Action {
  key: string;
  label: string;
  needsReason: boolean;
  blockedReason: string | null;
  to?: string;
  toLabel?: string;
  routeKey?: string | null;
  routeLabel?: string | null;
  decisionReason?: string | null;
}
export interface CaseDetail extends CaseSummary {
  routing: RouteResult | null;
  version: number;
  createdAt: string;
  processVersionId: number;
  processNumber: number;
  data: Record<string, string>;
  fields: { key: string; label: string; type: string; required: boolean; editable: boolean }[];
  requiredDocuments: string[];
  canUpload: boolean;
  canWrite: boolean;
  actions: Action[];
  documents: Document[];
  tasks: ReviewTask[];
  response: { note: string; actor: string; at: string; final: boolean } | null;
  history: { id: number; actor: string; action: string; note: string; round: number; at: string; isPublic: boolean }[];
}
export interface Report {
  total: number;
  states: { state: string; count: number }[];
  openTasks: number;
  overdueTasks: number;
}
export interface Notification {
  id: number;
  caseId: number;
  message: string;
  at: string;
}
export interface OrgUnit {
  key: string;
  name: string;
  parentKey: string | null;
  version: number;
}
export interface RoutingCondition {
  field: string;
  operator: string;
  value: string;
}
export interface RoutingSpec {
  match: 'all' | 'any';
  conditions: RoutingCondition[];
}
export interface RoutingRule {
  processKey: string | null;
  id: number;
  name: string;
  priority: number;
  enabled: boolean;
  targetUnit: string;
  version: number;
  updatedAt: string;
  spec: RoutingSpec;
}
export interface RouteResult {
  unit: string;
  unitName: string;
  ruleId: number | null;
  ruleVersion: number | null;
  ruleName: string;
  reason: string;
}

export interface PayloadCondition {
  field: string;
  operator: string;
  value: string;
}
export interface PayloadRoute {
  key: string;
  label: string;
  to: string;
  priority: number;
  when: { match: 'all' | 'any'; conditions: PayloadCondition[] };
}
export interface PayloadDecision {
  to: string;
  toLabel: string;
  routeKey: string | null;
  routeLabel: string;
  priority: number | null;
  reason: string;
}
