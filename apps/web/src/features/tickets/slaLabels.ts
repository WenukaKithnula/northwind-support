import type { SlaStatus } from '../../types/api';

/** The words for every SLA status. The indicator and the filter bar both use these, so they never differ. */
export const slaStatusLabels: Record<SlaStatus, string> = {
  WithinSla: 'Within SLA',
  AtRisk: 'At risk',
  Breached: 'Breached',
  Met: 'Met',
  NotApplicable: 'No SLA',
};
