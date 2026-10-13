import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';

/** Loads a ticket's escalation history, and again whenever the ticket changes or `reload` is called. */
export function useEscalations(ticketId: number) {
  const load = useCallback(() => ticketsApi.getEscalations(ticketId), [ticketId]);

  return useAsyncData(load, 'Unable to load the escalation history.');
}
