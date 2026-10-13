import { useSyncExternalStore } from 'react';
import { sessionStore } from './sessionStore';

/** The signed-in session, or null. Re-renders whoever uses it when someone signs in or out. */
export function useSession() {
  return useSyncExternalStore(sessionStore.subscribe, sessionStore.get);
}
