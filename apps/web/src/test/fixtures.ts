import type {
  EscalationEntry,
  PagedResult,
  TicketDetail,
  TicketListItem,
  TriageResult,
} from '../types/api';

/** A list row with sensible defaults, so tests only state what they care about. */
export function ticketListItem(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard' },
    category: { id: 2, name: 'Billing' },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    ...overrides,
  };
}

/** A ticket as the detail page shows it. */
export function ticketDetail(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    description: 'Invoice 5512 is EUR 240 higher than the confirmed order.',
    status: 'InProgress',
    priority: 'Medium',
    customer: {
      id: 2,
      name: 'Fabrikam Inc',
      tier: 'Standard',
      email: 'helpdesk@fabrikam.example',
      phone: null,
    },
    category: { id: 2, name: 'Billing', requiresSpecialist: true },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-25T08:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'WithinSla',
    ...overrides,
  };
}

/** One row of an escalation history. */
export function escalationEntry(overrides: Partial<EscalationEntry> = {}): EscalationEntry {
  return {
    id: 1,
    ticketId: 12,
    fromPriority: 'Medium',
    toPriority: 'High',
    fromAgent: { id: 4, fullName: 'Sara Lindqvist' },
    toAgent: { id: 4, fullName: 'Sara Lindqvist' },
    fromDueAtUtc: '2026-09-25T08:12:00Z',
    toDueAtUtc: '2026-09-25T18:00:00Z',
    reason: 'Customer called twice and needs an answer.',
    escalatedBy: 'alex.lead',
    escalatedAtUtc: '2026-09-25T10:00:00Z',
    ...overrides,
  };
}

/** The triage decision for a Premium customer's Security ticket, as the brief words it. */
export function triageResult(overrides: Partial<TriageResult> = {}): TriageResult {
  return {
    appliedPriority: 'Critical',
    priorityReason: "Category 'Security' is always treated as Critical",
    slaWindowHours: 2,
    slaReason: 'Critical base window 4h, halved for a Premium customer',
    assignmentReason: 'Fewest open tickets among Security specialists',
    ...overrides,
  };
}

export function page(items: TicketListItem[]): PagedResult<TicketListItem> {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
  };
}
