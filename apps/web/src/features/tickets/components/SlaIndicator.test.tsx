import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { slaStatusLabels } from '../slaLabels';
import { SlaIndicator } from './SlaIndicator';

const allStatuses: SlaStatus[] = ['WithinSla', 'AtRisk', 'Breached', 'Met', 'NotApplicable'];

describe('SlaIndicator', () => {
  it.each([
    ['WithinSla', 'Within SLA'],
    ['AtRisk', 'At risk'],
    ['Breached', 'Breached'],
    ['Met', 'Met'],
    ['NotApplicable', 'No SLA'],
  ] as const)('shows %s as "%s"', (status, label) => {
    render(<SlaIndicator status={status} />);

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('makes every state distinguishable by its words, not by its colour', () => {
    const texts = allStatuses.map((status) => {
      const { container, unmount } = render(<SlaIndicator status={status} />);
      const text = container.textContent;
      unmount();

      return text;
    });

    // Five different readings, so no state depends on telling two colours apart.
    expect(new Set(texts).size).toBe(allStatuses.length);
  });

  it('gives every state its own symbol as well as its own words', () => {
    const symbols = allStatuses.map((status) => {
      const { container, unmount } = render(<SlaIndicator status={status} />);
      const symbol = container.querySelector('[aria-hidden="true"]')?.textContent;
      unmount();

      return symbol;
    });

    expect(symbols.every((symbol) => symbol && symbol.length > 0)).toBe(true);
    expect(new Set(symbols).size).toBe(allStatuses.length);
  });

  it('leaves the symbol out of what a screen reader reads, so the label is read once', () => {
    render(<SlaIndicator status="AtRisk" />);

    expect(screen.getByText('!')).toHaveAttribute('aria-hidden', 'true');
  });

  it('shows the due date when it is given one', () => {
    render(<SlaIndicator status="AtRisk" dueAtUtc="2026-10-13T19:12:00Z" />);

    expect(screen.getByText(/2026/)).toBeInTheDocument();
  });

  it('shows a dash when the ticket has no due date', () => {
    render(<SlaIndicator status="NotApplicable" dueAtUtc={null} />);

    expect(screen.getByText('-')).toBeInTheDocument();
  });

  it('shows no due date line when none is asked for', () => {
    const { container } = render(<SlaIndicator status="Met" />);

    expect(container.querySelector('.sla-indicator__due')).toBeNull();
  });

  it('exports a label for every status, for the filter to share', () => {
    expect(Object.keys(slaStatusLabels).sort()).toEqual([...allStatuses].sort());
  });
});
