import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { AgentSummary, EscalationEntry } from '../../../types/api';

interface EscalationHistoryProps {
  /** Newest first, as the API sends them. Undefined until the first load finishes. */
  entries?: EscalationEntry[];
  isLoading: boolean;
  error?: string;
  onRetry: () => void;
}

/** What a ticket's escalations changed: priority, owner and due date, with the reason, who and when. */
export function EscalationHistory({ entries, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <div className="card">
      <h2>Escalation history</h2>

      {error && <ErrorMessage message={error} onRetry={onRetry} />}

      {isLoading && !entries && <Spinner label="Loading history" />}

      {entries && entries.length === 0 && <EmptyState title="Not escalated yet" />}

      {entries && entries.length > 0 && (
        <div className="table-wrapper">
          <table className="table">
            <caption className="visually-hidden">Escalation history, newest first</caption>
            <thead>
              <tr>
                <th scope="col">When</th>
                <th scope="col">Who</th>
                <th scope="col">Priority</th>
                <th scope="col">Owner</th>
                <th scope="col">Due date</th>
                <th scope="col">Reason</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => (
                <tr key={entry.id}>
                  <td>{formatDateTime(entry.escalatedAtUtc)}</td>
                  <td>{entry.escalatedBy}</td>
                  <td>
                    {entry.fromPriority} → {entry.toPriority}
                  </td>
                  <td>{ownerChange(entry.fromAgent, entry.toAgent)}</td>
                  <td>
                    {formatDateTime(entry.fromDueAtUtc)} → {formatDateTime(entry.toDueAtUtc)}
                  </td>
                  <td>{entry.reason}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function ownerChange(from: AgentSummary | null, to: AgentSummary | null): string {
  const before = from?.fullName ?? 'Unassigned';
  const after = to?.fullName ?? 'Unassigned';

  return from?.id === to?.id ? `${after} (unchanged)` : `${before} → ${after}`;
}
