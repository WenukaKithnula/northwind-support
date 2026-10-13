import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

/** Lets timers and pending promises run for the given time. */
const settle = (ms: number) =>
  act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });

/** Renders the hook and waits for its first request, then forgets the calls made so far. */
async function renderSettledHook() {
  const hook = renderHook(() => useTicketList());

  await settle(300);
  getTickets.mockClear();

  return hook;
}

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  describe('DEFECT-117: a filter change must send a request', () => {
    const immediateChanges: [string, Partial<TicketFilters>][] = [
      ['status', { status: 'InProgress' }],
      ['priority', { priority: 'High' }],
      ['category', { categoryId: 3 }],
      ['customer', { customerId: 2 }],
      ['assigned agent', { assignedAgentId: 4 }],
      ['unassigned only', { unassignedOnly: true }],
      // Added in Task 2 with no change to the hook: any filter in the applied set is requested at once.
      ['SLA status', { slaStatus: 'AtRisk' }],
      ['sort order', { sortBy: 'dueAtUtc', sortDirection: 'asc' }],
    ];

    it.each(immediateChanges)('sends a request straight away when %s changes', async (_name, patch) => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters(patch);
      });
      // No debounce window to wait for: only let the promise from the request settle.
      await settle(0);

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining(patch));
    });

    it('sends a request straight away when the page changes', async () => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters({ page: 2 });
      });
      await settle(0);

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }));
    });

    it('goes back to the first page when a filter changes', async () => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters({ page: 3 });
      });
      await settle(0);

      act(() => {
        result.current.updateFilters({ status: 'Open' });
      });
      await settle(0);

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'Open', page: 1 }),
      );
    });

    it('sends the chosen status with the search that follows it', async () => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters({ status: 'InProgress' });
      });
      await settle(0);

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });
      await settle(300);

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'InProgress', search: 'invoice' }),
      );
    });

    it('keeps the chosen status when the search is cleared', async () => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters({ status: 'InProgress' });
      });
      await settle(0);

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });
      await settle(300);

      act(() => {
        result.current.updateFilters({ search: '' });
      });
      await settle(300);

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'InProgress', search: '' }),
      );
    });
  });

  describe('searching', () => {
    it('searches with the text the user typed, once the typing settles', async () => {
      const { result } = renderHook(() => useTicketList());

      await settle(300);

      expect(getTickets).toHaveBeenCalledTimes(1);

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });

      // Still inside the debounce window: no second request yet.
      expect(getTickets).toHaveBeenCalledTimes(1);

      await settle(300);

      expect(getTickets).toHaveBeenCalledTimes(2);
      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: 'invoice', page: 1 }),
      );

      expect(result.current.isLoading).toBe(false);
      expect(result.current.data?.items).toHaveLength(1);
    });

    it('sends one request for a burst of keystrokes, not one per key', async () => {
      const { result } = await renderSettledHook();

      for (const text of ['i', 'in', 'inv', 'invo']) {
        act(() => {
          result.current.updateFilters({ search: text });
        });
        await settle(100);
      }

      expect(getTickets).not.toHaveBeenCalled();

      await settle(300);

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'invo' }));
    });

    it('shows what was typed straight away, before the request is sent', async () => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters({ search: 'inv' });
      });

      expect(result.current.filters.search).toBe('inv');
      expect(getTickets).not.toHaveBeenCalled();
    });

    it('goes back to the first page when the search changes', async () => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters({ page: 2 });
      });
      await settle(0);

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });
      await settle(300);

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: 'invoice', page: 1 }),
      );
    });
  });

  describe('clearing the filters', () => {
    it('sends a request with every filter cleared, including the search box', async () => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters({ status: 'Open', priority: 'High', search: 'invoice' });
      });
      await settle(300);
      getTickets.mockClear();

      act(() => {
        result.current.resetFilters();
      });
      await settle(300);

      expect(result.current.filters.search).toBe('');
      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({
          search: '',
          status: undefined,
          priority: undefined,
          page: 1,
        }),
      );
    });

    it('drops a search that was still waiting to be sent', async () => {
      const { result } = await renderSettledHook();

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });
      act(() => {
        result.current.resetFilters();
      });
      await settle(300);

      expect(getTickets).not.toHaveBeenCalledWith(expect.objectContaining({ search: 'invoice' }));
    });
  });
});
