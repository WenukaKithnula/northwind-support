import type { TicketDetail } from '../../types/api';

/** The limits the API enforces on an escalation's reason. Keep them in step with TicketEscalation.cs. */
export const REASON_MIN_LENGTH = 5;
export const REASON_MAX_LENGTH = 500;

export interface EscalationInput {
  reason: string;
}

export type EscalationErrors = Partial<Record<keyof EscalationInput, string>>;

/**
 * Checks an escalation against the same rules as the server, so an invalid one never reaches the
 * API. The reason is counted after trimming, because that is how the server counts it. Who is
 * escalating is not asked for: the API takes that from the signed-in agent.
 */
export function validateEscalation({ reason }: EscalationInput): EscalationErrors {
  const errors: EscalationErrors = {};
  const length = reason.trim().length;

  if (length < REASON_MIN_LENGTH || length > REASON_MAX_LENGTH) {
    errors.reason = `The reason must be ${REASON_MIN_LENGTH} to ${REASON_MAX_LENGTH} characters.`;
  }

  return errors;
}

/**
 * Why a ticket cannot be escalated, or undefined when it can. The screen uses it to take the form
 * away; the server makes the same decision and is still asked, because what is on screen can be
 * out of date.
 */
export function escalationBlockedReason(
  ticket: Pick<TicketDetail, 'reference' | 'status' | 'priority'>,
): string | undefined {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return `${ticket.reference} is ${ticket.status} and cannot be escalated.`;
  }

  if (ticket.priority === 'Critical') {
    return `${ticket.reference} is already Critical and cannot be escalated further.`;
  }

  return undefined;
}
