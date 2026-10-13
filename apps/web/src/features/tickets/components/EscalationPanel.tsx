import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
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
 * Escalates a ticket one priority level. The form checks the same limits as the server, so an
 * invalid reason never reaches the API; when the ticket cannot be escalated at all, the form is
 * replaced by the reason. What the screen knows can be out of date, so a refusal from the server
 * is shown too.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('');
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

    const problems = validateEscalation({ reason, escalatedBy });
    setErrors(problems);
    setSubmitError(undefined);

    if (Object.keys(problems).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      const result = await ticketsApi.escalate(ticket.id, {
        reason: reason.trim(),
        escalatedBy: escalatedBy.trim(),
      });

      setReason('');
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError && Object.keys(caught.fieldErrors).length > 0) {
        setErrors(fromApiErrors(caught.fieldErrors));
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

      <Field id="escalation-by" label="Escalated by" error={errors.escalatedBy}>
        {(fieldProps) => (
          <input
            {...fieldProps}
            type="text"
            value={escalatedBy}
            disabled={isSaving}
            onChange={(event) => setEscalatedBy(event.target.value)}
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

/** Picks out the messages the API sent for the two fields this form has. */
function fromApiErrors(fieldErrors: Record<string, string[]>): EscalationErrors {
  const errors: EscalationErrors = {};

  for (const [field, messages] of Object.entries(fieldErrors)) {
    const key = field.charAt(0).toLowerCase() + field.slice(1);

    if (key === 'reason' || key === 'escalatedBy') {
      errors[key] = messages[0];
    }
  }

  return errors;
}
