/**
 * Types for everything the API sends and receives. Keep these in step with the DTOs in
 * src/Application - they are the contract between the two halves of the application.
 */

export type CustomerTier = 'Standard' | 'Premium';

export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export type TicketStatus = 'New' | 'Open' | 'InProgress' | 'Resolved' | 'Closed';

export type SlaStatus = 'NotApplicable' | 'WithinSla' | 'AtRisk' | 'Breached' | 'Met';

export type TicketSortField = 'createdAtUtc' | 'updatedAtUtc' | 'dueAtUtc' | 'priority' | 'status';

export type SortDirection = 'asc' | 'desc';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CustomerSummary {
  id: number;
  name: string;
  tier: CustomerTier;
}

export interface CustomerContact extends CustomerSummary {
  email: string;
  phone: string | null;
}

export interface CategorySummary {
  id: number;
  name: string;
}

export interface CategoryOnTicket extends CategorySummary {
  requiresSpecialist: boolean;
}

export interface AgentSummary {
  id: number;
  fullName: string;
}

export interface TicketListItem {
  id: number;
  reference: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  customer: CustomerSummary;
  category: CategorySummary;
  assignedAgent: AgentSummary | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  dueAtUtc: string | null;
  resolvedAtUtc: string | null;
  slaStatus: SlaStatus;
}

export interface TicketDetail {
  id: number;
  reference: string;
  title: string;
  description: string;
  status: TicketStatus;
  priority: TicketPriority;
  customer: CustomerContact;
  category: CategoryOnTicket;
  assignedAgent: AgentSummary | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  dueAtUtc: string | null;
  resolvedAtUtc: string | null;
  slaStatus: SlaStatus;
  /** How priority, due date and owner were decided. Only the response to creating a ticket has it. */
  triage?: TriageResult;
}

/** What the system decided when a ticket was raised, and in words why. */
export interface TriageResult {
  appliedPriority: TicketPriority;
  priorityReason: string;
  slaWindowHours: number;
  slaReason: string;
  assignmentReason: string;
}

/** One entry in a ticket's escalation history. */
export interface EscalationEntry {
  id: number;
  ticketId: number;
  fromPriority: TicketPriority;
  toPriority: TicketPriority;
  fromAgent: AgentSummary | null;
  toAgent: AgentSummary | null;
  fromDueAtUtc: string | null;
  toDueAtUtc: string | null;
  reason: string;
  escalatedBy: string;
  escalatedAtUtc: string;
}

/** The response to escalating a ticket: the ticket as it is now, and the entry just written. */
export interface EscalationResult {
  ticket: TicketDetail;
  escalation: EscalationEntry;
}

/** Body of an escalation. Who escalated is not sent: the API takes it from the token. */
export interface EscalatePayload {
  reason: string;
}

/** The agent who is signed in: just enough to greet them. */
export interface SignedInAgent {
  id: number;
  fullName: string;
  email: string;
}

/** What a successful sign-in returns, and what the browser keeps for the length of the session. */
export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  agent: SignedInAgent;
}

/** Everything the ticket list screen can narrow the results by. */
export interface TicketFilters {
  search: string;
  status?: TicketStatus;
  priority?: TicketPriority;
  categoryId?: number;
  customerId?: number;
  assignedAgentId?: number;
  unassignedOnly?: boolean;
  slaStatus?: SlaStatus;
  page: number;
  pageSize: number;
  sortBy: TicketSortField;
  sortDirection: SortDirection;
}

export interface CreateTicketPayload {
  title: string;
  description: string;
  customerId: number;
  categoryId: number;
  requestedPriority?: TicketPriority;
}

export interface Agent {
  id: number;
  fullName: string;
  email: string;
  isActive: boolean;
  maxOpenTickets: number;
  openTicketCount: number;
  specializations: CategorySummary[];
}

export interface Category {
  id: number;
  name: string;
  isActive: boolean;
  requiresSpecialist: boolean;
  forcesCriticalPriority: boolean;
}

export interface CustomerListItem {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  tier: CustomerTier;
  openTicketCount: number;
}

export interface CustomerDetail {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  tier: CustomerTier;
  createdAtUtc: string;
  tickets: TicketListItem[];
}

/** RFC 7807 problem document, which is how the API reports every failure. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
