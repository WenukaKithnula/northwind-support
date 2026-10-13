import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketFilters } from '../../../types/api';

/** How long typing has to pause before the search text is sent to the API. */
const SEARCH_DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

/**
 * Owns the ticket list's filters and the data they produce.
 *
 * The list is always loaded for `applied`, the filters in force. Changing anything in it sends
 * a request straight away, so a filter added later needs no extra wiring here. The one thing
 * that waits is the text being typed into the search box: it is kept apart in `searchText` and
 * only copied into `applied` once the typing settles, so there is no request per keystroke.
 */
export function useTicketList() {
  const [applied, setApplied] = useState<TicketFilters>(emptyFilters);
  const [searchText, setSearchText] = useState(emptyFilters.search);
  const searchTimer = useRef<number | undefined>(undefined);

  const load = useCallback(() => ticketsApi.getTickets(applied), [applied]);
  const list = useAsyncData(load, 'Unable to load tickets.');

  const cancelPendingSearch = useCallback(() => window.clearTimeout(searchTimer.current), []);

  // A search still waiting to be sent must not fire after the page has gone.
  useEffect(() => cancelPendingSearch, [cancelPendingSearch]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    const { search, ...immediate } = patch;

    if (search !== undefined) {
      setSearchText(search);
      cancelPendingSearch();
      searchTimer.current = window.setTimeout(() => {
        setApplied((current) =>
          current.search === search ? current : { ...current, search, page: 1 },
        );
      }, SEARCH_DEBOUNCE_MS);
    }

    if (Object.keys(immediate).length > 0) {
      // Any change to what is shown starts again from the first page, unless it is the page.
      setApplied((current) => ({ ...current, ...immediate, page: immediate.page ?? 1 }));
    }
  };

  const resetFilters = () => {
    cancelPendingSearch();
    setSearchText(emptyFilters.search);
    setApplied(emptyFilters);
  };

  // The search box shows what was typed, which can be ahead of what was last requested.
  const filters = useMemo(() => ({ ...applied, search: searchText }), [applied, searchText]);

  return {
    filters,
    updateFilters,
    resetFilters,
    reload: list.reload,
    isLoading: list.isLoading,
    data: list.data,
    error: list.error,
  };
}
