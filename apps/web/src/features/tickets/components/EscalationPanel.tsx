import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { useSession } from '../../../auth/useSession';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import {
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  escalationBlockedReason,
  validateEscalation,
  type EscalationErrors,
} from '../escalationRules';

interface EscalationPanelProps {
  ticket: TicketDetail;
  /** Told only about an escalation that succeeded, so a refused one leaves the screen as it was. */
  onEscalated: (result: EscalationResult) => void;
}

/**
 * Escalates a ticket one priority level. The form checks the same limit as the server, so an
 * invalid reason never reaches the API; when the ticket cannot be escalated at all, the form is
 * replaced by the reason. What the screen knows can be out of date, so a refusal from the server
 * is shown too. The escalation is recorded against whoever is signed in; the form does not ask.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const session = useSession();
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<EscalationErrors>({});
  const [submitError, setSubmitError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);

  const blockedReason = escalationBlockedReason(ticket);

  if (blockedReason) {
    return (
      <div className="card">
        <h2>Escalate</h2>
        <p className="field__hint">{blockedReason}</p>
      </div>
    );
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const problems = validateEscalation({ reason });
    setErrors(problems);
    setSubmitError(undefined);

    if (Object.keys(problems).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      const result = await ticketsApi.escalate(ticket.id, { reason: reason.trim() });

      setReason('');
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError && caught.fieldErrors.Reason) {
        setErrors({ reason: caught.fieldErrors.Reason[0] });
      }

      setSubmitError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit} noValidate aria-busy={isSaving}>
      <h2>Escalate</h2>
      <p className="field__hint">
        Raises the priority one level, restarts the due date and records who escalated it and why.
        {session && <> It will be recorded as {session.agent.fullName}.</>}
      </p>

      <Field
        id="escalation-reason"
        label="Reason"
        error={errors.reason}
        hint={`${REASON_MIN_LENGTH} to ${REASON_MAX_LENGTH} characters.`}
      >
        {(fieldProps) => (
          <textarea
            {...fieldProps}
            rows={3}
            value={reason}
            disabled={isSaving}
            onChange={(event) => setReason(event.target.value)}
          />
        )}
      </Field>

      {submitError && (
        <p className="field__error" role="alert">
          {submitError}
        </p>
      )}

      <div className="button-row">
        <button type="submit" className="button button--primary" disabled={isSaving}>
          {isSaving ? 'Escalating...' : 'Escalate'}
        </button>
      </div>
    </form>
  );
}
