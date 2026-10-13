import { formatDateTime } from '../../../lib/format';
import type { TicketDetail } from '../../../types/api';

interface TriageResultProps {
  /** A ticket as returned by creating it, which is the only response that carries a triage decision. */
  ticket: TicketDetail;
}

/** What the system decided for a new ticket, and why: its priority, its due date and its owner. */
export function TriageResult({ ticket }: TriageResultProps) {
  const { triage } = ticket;

  if (!triage) {
    return null;
  }

  return (
    <dl className="summary triage">
      <div>
        <dt>Priority</dt>
        <dd>
          {triage.appliedPriority}
          <span className="summary__note">{triage.priorityReason}</span>
        </dd>
      </div>

      <div>
        <dt>Due</dt>
        <dd>
          {formatDateTime(ticket.dueAtUtc)}
          <span className="summary__note">
            {triage.slaWindowHours} h window. {triage.slaReason}
          </span>
        </dd>
      </div>

      <div>
        <dt>Assigned to</dt>
        <dd>
          {ticket.assignedAgent ? ticket.assignedAgent.fullName : 'Unassigned'}
          <span className="summary__note">
            {ticket.assignedAgent ? triage.assignmentReason : `Because: ${triage.assignmentReason}`}
          </span>
        </dd>
      </div>
    </dl>
  );
}
