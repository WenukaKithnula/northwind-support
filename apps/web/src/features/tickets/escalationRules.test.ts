import { describe, expect, it } from 'vitest';
import {
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  escalationBlockedReason,
  validateEscalation,
} from './escalationRules';

describe('validateEscalation', () => {
  const valid = { reason: 'Customer called twice and needs an answer.' };

  it('accepts a good reason', () => {
    expect(validateEscalation(valid)).toEqual({});
  });

  it.each([REASON_MIN_LENGTH, REASON_MAX_LENGTH])('accepts a reason of exactly %i characters', (length) => {
    expect(validateEscalation({ reason: 'a'.repeat(length) }).reason).toBeUndefined();
  });

  it.each([REASON_MIN_LENGTH - 1, REASON_MAX_LENGTH + 1])(
    'rejects a reason of %i characters, just outside the limits',
    (length) => {
      expect(validateEscalation({ reason: 'a'.repeat(length) }).reason).toMatch(/5 to 500/);
    },
  );

  it('counts the reason after trimming, as the server does', () => {
    expect(validateEscalation({ reason: '   abc   ' }).reason).toBeDefined();
    expect(validateEscalation({ reason: '  abcde  ' }).reason).toBeUndefined();
  });

  it('rejects an empty reason', () => {
    expect(validateEscalation({ reason: '' }).reason).toBeDefined();
  });

  it('does not ask who is escalating, because the API takes that from the token', () => {
    expect(Object.keys(validateEscalation({ reason: '' }))).toEqual(['reason']);
  });
});

describe('escalationBlockedReason', () => {
  const open = { reference: 'TCK-0001', status: 'Open', priority: 'Medium' } as const;

  it('lets an open ticket below Critical be escalated', () => {
    expect(escalationBlockedReason(open)).toBeUndefined();
    expect(escalationBlockedReason({ ...open, status: 'New', priority: 'Low' })).toBeUndefined();
    expect(escalationBlockedReason({ ...open, status: 'InProgress', priority: 'High' })).toBeUndefined();
  });

  it('blocks a Critical ticket and says so', () => {
    expect(escalationBlockedReason({ ...open, priority: 'Critical' })).toMatch(/already Critical/);
  });

  it.each(['Resolved', 'Closed'] as const)('blocks a %s ticket and says which', (status) => {
    expect(escalationBlockedReason({ ...open, status })).toContain(status);
  });
});
