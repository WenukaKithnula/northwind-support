import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ticketDetail, triageResult } from '../../../test/fixtures';
import { TriageResult } from './TriageResult';

describe('TriageResult', () => {
  const assigned = ticketDetail({
    priority: 'Critical',
    assignedAgent: { id: 3, fullName: 'Priya Nair' },
    dueAtUtc: '2026-10-13T12:00:00Z',
    triage: triageResult(),
  });

  it('shows the applied priority, the due date and the owner', () => {
    render(<TriageResult ticket={assigned} />);

    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText(/2026/)).toBeInTheDocument();
    expect(screen.getByText('Priya Nair')).toBeInTheDocument();
  });

  it('explains each part of the decision in the words the server gave', () => {
    render(<TriageResult ticket={assigned} />);

    expect(screen.getByText("Category 'Security' is always treated as Critical")).toBeInTheDocument();
    expect(screen.getByText(/Critical base window 4h, halved for a Premium customer/)).toBeInTheDocument();
    expect(screen.getByText('Fewest open tickets among Security specialists')).toBeInTheDocument();
  });

  it('shows the window in hours beside the due date', () => {
    render(<TriageResult ticket={assigned} />);

    expect(screen.getByText(/2 h window/)).toBeInTheDocument();
  });

  it('says why when nobody could be assigned', () => {
    const unassigned = ticketDetail({
      assignedAgent: null,
      triage: triageResult({ assignmentReason: 'No available agent specialised in Security' }),
    });

    render(<TriageResult ticket={unassigned} />);

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText(/Because: No available agent specialised in Security/)).toBeInTheDocument();
  });

  it('shows nothing when the ticket carries no triage decision', () => {
    const { container } = render(<TriageResult ticket={ticketDetail({ triage: undefined })} />);

    expect(container).toBeEmptyDOMElement();
  });
});
