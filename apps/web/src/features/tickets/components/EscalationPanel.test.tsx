import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { escalationEntry, ticketDetail } from '../../../test/fixtures';
import type { EscalationResult } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

const goodReason = 'Customer called twice and needs an answer.';

function setUp(ticket = ticketDetail()) {
  const onEscalated = vi.fn();

  render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

  return { onEscalated, user: userEvent.setup() };
}

async function fillIn(user: ReturnType<typeof userEvent.setup>, reason: string, who = 'alex.lead') {
  await user.clear(screen.getByLabelText('Reason'));
  await user.type(screen.getByLabelText('Reason'), reason);
  await user.clear(screen.getByLabelText('Escalated by'));
  await user.type(screen.getByLabelText('Escalated by'), who);
}

describe('EscalationPanel', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('validation', () => {
    it('blocks a reason that is too short, shows the error and never calls the API', async () => {
      const { user, onEscalated } = setUp();

      await fillIn(user, 'abc');
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      expect(screen.getByText('The reason must be 5 to 500 characters.')).toBeInTheDocument();
      expect(screen.getByLabelText('Reason')).toHaveAttribute('aria-invalid', 'true');
      expect(escalate).not.toHaveBeenCalled();
      expect(onEscalated).not.toHaveBeenCalled();
    });

    it('blocks a reason that is too long without calling the API', async () => {
      const { user } = setUp();

      await user.type(screen.getByLabelText('Escalated by'), 'alex.lead');
      // Pasting is quicker than typing 501 characters, and is how a long reason would arrive.
      await user.click(screen.getByLabelText('Reason'));
      await user.paste('a'.repeat(501));
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      expect(screen.getByText('The reason must be 5 to 500 characters.')).toBeInTheDocument();
      expect(escalate).not.toHaveBeenCalled();
    });

    it('blocks an escalation that does not say who is escalating', async () => {
      const { user } = setUp();

      await user.type(screen.getByLabelText('Reason'), goodReason);
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      expect(screen.getByText('Say who is escalating this ticket.')).toBeInTheDocument();
      expect(escalate).not.toHaveBeenCalled();
    });

    it('counts the reason after trimming, like the server', async () => {
      const { user } = setUp();

      await fillIn(user, '   abc   ');
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      expect(escalate).not.toHaveBeenCalled();
    });
  });

  describe('escalating', () => {
    it('sends the trimmed reason and name, then hands the result to the page', async () => {
      const result: EscalationResult = {
        ticket: ticketDetail({ priority: 'High' }),
        escalation: escalationEntry(),
      };
      escalate.mockResolvedValue(result);
      const { user, onEscalated } = setUp(ticketDetail({ id: 12 }));

      await fillIn(user, `  ${goodReason}  `, '  alex.lead  ');
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      await waitFor(() => expect(onEscalated).toHaveBeenCalledWith(result));
      expect(escalate).toHaveBeenCalledWith(12, { reason: goodReason, escalatedBy: 'alex.lead' });
    });

    it('shows a pending state while the request is in flight, and does not send twice', async () => {
      let finish: (value: EscalationResult) => void = () => undefined;
      escalate.mockReturnValue(new Promise((resolve) => (finish = resolve)));
      const { user } = setUp();

      await fillIn(user, goodReason);
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      const pending = await screen.findByRole('button', { name: 'Escalating...' });
      expect(pending).toBeDisabled();

      await user.click(pending);
      expect(escalate).toHaveBeenCalledTimes(1);

      finish({ ticket: ticketDetail({ priority: 'High' }), escalation: escalationEntry() });
      await screen.findByRole('button', { name: 'Escalate' });
    });

    it('clears the reason after a successful escalation, ready for the next', async () => {
      escalate.mockResolvedValue({ ticket: ticketDetail(), escalation: escalationEntry() });
      const { user } = setUp();

      await fillIn(user, goodReason);
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      await waitFor(() => expect(screen.getByLabelText('Reason')).toHaveValue(''));
    });
  });

  describe('when the server refuses', () => {
    it('shows a 409 to the user and leaves the ticket unchanged', async () => {
      escalate.mockRejectedValue(
        new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated further.'),
      );
      const { user, onEscalated } = setUp();

      await fillIn(user, goodReason);
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Ticket TCK-0012 is already Critical and cannot be escalated further.',
      );
      // The page is only told about a success, so the ticket on screen is not touched.
      expect(onEscalated).not.toHaveBeenCalled();
      expect(screen.getByRole('button', { name: 'Escalate' })).toBeEnabled();
    });

    it('shows a server validation error against the field it belongs to', async () => {
      escalate.mockRejectedValue(
        new ApiError(400, 'One or more validation errors occurred.', {
          errors: { Reason: ['The reason must be 5 to 500 characters.'] },
        }),
      );
      const { user } = setUp();

      await fillIn(user, goodReason);
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      expect(await screen.findByText('The reason must be 5 to 500 characters.')).toBeInTheDocument();
      expect(screen.getByLabelText('Reason')).toHaveAttribute('aria-invalid', 'true');
    });

    it('shows a failure that is not an API error without losing what was typed', async () => {
      escalate.mockRejectedValue(new TypeError('Failed to fetch'));
      const { user } = setUp();

      await fillIn(user, goodReason);
      await user.click(screen.getByRole('button', { name: 'Escalate' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Could not escalate the ticket.');
      expect(screen.getByLabelText('Reason')).toHaveValue(goodReason);
    });
  });

  describe('when the ticket cannot be escalated', () => {
    it('takes the form away from a Critical ticket and says why', () => {
      setUp(ticketDetail({ priority: 'Critical' }));

      expect(screen.queryByLabelText('Reason')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Escalate' })).not.toBeInTheDocument();
      expect(screen.getByText(/already Critical/)).toBeInTheDocument();
    });

    it.each(['Resolved', 'Closed'] as const)('takes the form away from a %s ticket', (status) => {
      setUp(ticketDetail({ status }));

      expect(screen.queryByLabelText('Reason')).not.toBeInTheDocument();
      expect(screen.getByText(new RegExp(`is ${status}`))).toBeInTheDocument();
    });

    it('shows the form for an open ticket below Critical', () => {
      setUp(ticketDetail({ status: 'Open', priority: 'Low' }));

      expect(screen.getByLabelText('Reason')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Escalate' })).toBeInTheDocument();
    });
  });
});
