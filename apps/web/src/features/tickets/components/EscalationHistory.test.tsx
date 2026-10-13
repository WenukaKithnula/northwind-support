import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { escalationEntry } from '../../../test/fixtures';
import { EscalationHistory } from './EscalationHistory';

describe('EscalationHistory', () => {
  it('shows the priority change, the reason, who did it and when', () => {
    render(<EscalationHistory entries={[escalationEntry()]} isLoading={false} onRetry={vi.fn()} />);

    const row = screen.getAllByRole('row')[1];

    expect(within(row).getByText('Medium → High')).toBeInTheDocument();
    expect(within(row).getByText('Customer called twice and needs an answer.')).toBeInTheDocument();
    expect(within(row).getByText('alex.lead')).toBeInTheDocument();
    // The first cell is when it happened.
    expect(within(row).getAllByRole('cell')[0]).toHaveTextContent(/2026/);
  });

  it('shows the due date before and after', () => {
    render(<EscalationHistory entries={[escalationEntry()]} isLoading={false} onRetry={vi.fn()} />);

    // Two dates joined by an arrow: the one it had, and the one it has now.
    expect(screen.getByText(/2026.*→.*2026/)).toBeInTheDocument();
  });

  it('says the owner stayed the same when it did', () => {
    render(<EscalationHistory entries={[escalationEntry()]} isLoading={false} onRetry={vi.fn()} />);

    expect(screen.getByText('Sara Lindqvist (unchanged)')).toBeInTheDocument();
  });

  it('shows who the ticket moved from and to when the owner changed', () => {
    const entry = escalationEntry({
      fromAgent: { id: 6, fullName: 'Yuki Tanaka' },
      toAgent: { id: 4, fullName: 'Sara Lindqvist' },
    });

    render(<EscalationHistory entries={[entry]} isLoading={false} onRetry={vi.fn()} />);

    expect(screen.getByText('Yuki Tanaka → Sara Lindqvist')).toBeInTheDocument();
  });

  it('shows "Unassigned" where the ticket had no owner before or after', () => {
    const entry = escalationEntry({ fromAgent: null, toAgent: null });

    render(<EscalationHistory entries={[entry]} isLoading={false} onRetry={vi.fn()} />);

    expect(screen.getByText('Unassigned (unchanged)')).toBeInTheDocument();
  });

  it('shows a dash for a due date the ticket did not have', () => {
    const entry = escalationEntry({ fromDueAtUtc: null });

    render(<EscalationHistory entries={[entry]} isLoading={false} onRetry={vi.fn()} />);

    expect(screen.getByText(/^-.*→/)).toBeInTheDocument();
  });

  it('lists the entries in the order it is given them, newest first', () => {
    const entries = [
      escalationEntry({ id: 2, fromPriority: 'High', toPriority: 'Critical', reason: 'Second reason here' }),
      escalationEntry({ id: 1, reason: 'First reason here' }),
    ];

    render(<EscalationHistory entries={entries} isLoading={false} onRetry={vi.fn()} />);

    const rows = screen.getAllByRole('row').slice(1);

    expect(within(rows[0]).getByText('Second reason here')).toBeInTheDocument();
    expect(within(rows[1]).getByText('First reason here')).toBeInTheDocument();
  });

  it('says so when the ticket was never escalated', () => {
    render(<EscalationHistory entries={[]} isLoading={false} onRetry={vi.fn()} />);

    expect(screen.getByText('Not escalated yet')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows a loading state before there is anything to show', () => {
    render(<EscalationHistory isLoading onRetry={vi.fn()} />);

    expect(screen.getByRole('status')).toHaveTextContent('Loading');
  });

  it('shows a failure with a way to try again', async () => {
    const onRetry = vi.fn();
    render(<EscalationHistory isLoading={false} error="Unable to load the escalation history." onRetry={onRetry} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Unable to load the escalation history.');

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
