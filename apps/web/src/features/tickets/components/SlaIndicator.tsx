import { Badge, type BadgeTone } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { slaStatusLabels } from '../slaLabels';

/**
 * Each state has a symbol and a colour as well as its words, so the state is never carried by
 * colour alone.
 */
const presentation: Record<SlaStatus, { symbol: string; tone: BadgeTone }> = {
  WithinSla: { symbol: '•', tone: 'info' },
  AtRisk: { symbol: '!', tone: 'warning' },
  Breached: { symbol: '✕', tone: 'danger' },
  Met: { symbol: '✓', tone: 'success' },
  NotApplicable: { symbol: '–', tone: 'neutral' },
};

interface SlaIndicatorProps {
  status: SlaStatus;
  /** When given, the due date is shown beside the status (a null one shows a dash). */
  dueAtUtc?: string | null;
}

/** Where a ticket stands against its due date. The one place that is drawn, in the list and on the detail page. */
export function SlaIndicator({ status, dueAtUtc }: SlaIndicatorProps) {
  const { symbol, tone } = presentation[status];

  return (
    <div className="sla-indicator">
      <Badge tone={tone}>
        <span aria-hidden="true">{symbol}</span> {slaStatusLabels[status]}
      </Badge>

      {dueAtUtc !== undefined && <span className="sla-indicator__due">{formatDateTime(dueAtUtc)}</span>}
    </div>
  );
}
