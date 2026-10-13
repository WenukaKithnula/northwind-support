import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { TicketFilters } from '../types/api';
import { ticketsApi } from './tickets';

const fetchMock = vi.fn();

/** No filter chosen: the list as it first loads. */
const emptyFilters: TicketFilters = {
  search: '',
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

function lastRequest(): { url: string; init?: RequestInit } {
  const [url, init] = fetchMock.mock.calls.at(-1) as [string, RequestInit | undefined];

  return { url, init };
}

describe('ticketsApi', () => {
  beforeEach(() => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({}), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  it('puts the SLA status in the query string, like every other filter', async () => {
    await ticketsApi.getTickets({ ...emptyFilters, slaStatus: 'AtRisk' });

    expect(new URL(lastRequest().url, 'http://x').searchParams.get('slaStatus')).toBe('AtRisk');
  });

  it('leaves the SLA status out when none is chosen', async () => {
    await ticketsApi.getTickets(emptyFilters);

    expect(new URL(lastRequest().url, 'http://x').searchParams.has('slaStatus')).toBe(false);
  });

  it('escalates by posting the reason and who to the ticket', async () => {
    await ticketsApi.escalate(7, { reason: 'Customer called twice.', escalatedBy: 'alex.lead' });

    const { url, init } = lastRequest();

    expect(url).toBe('/api/tickets/7/escalate');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(String(init?.body))).toEqual({
      reason: 'Customer called twice.',
      escalatedBy: 'alex.lead',
    });
  });

  it('reads a ticket history from the escalations endpoint', async () => {
    await ticketsApi.getEscalations(7);

    expect(lastRequest().url).toBe('/api/tickets/7/escalations');
  });
});
