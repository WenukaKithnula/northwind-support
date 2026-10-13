import { Link } from 'react-router-dom';
import { formatDateTime } from '../../../lib/format';
import type { TicketListItem } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';
import { PriorityBadge, StatusBadge } from './TicketBadges';

interface TicketTableProps {
  tickets: TicketListItem[];
}

export function TicketTable({ tickets }: TicketTableProps) {
  return (
    <table className="table">
      <caption className="visually-hidden">Support tickets</caption>
      <thead>
        <tr>
          <th scope="col">Reference</th>
          <th scope="col">Title</th>
          <th scope="col">Customer</th>
          <th scope="col">Category</th>
          <th scope="col">Priority</th>
          <th scope="col">Status</th>
          <th scope="col">Agent</th>
          <th scope="col">SLA</th>
          <th scope="col">Created</th>
        </tr>
      </thead>
      <tbody>
        {tickets.map((ticket) => (
          <tr key={ticket.id}>
            <td>
              <Link to={`/tickets/${ticket.id}`}>{ticket.reference}</Link>
            </td>
            <td>{ticket.title}</td>
            <td>
              {ticket.customer.name}
              {ticket.customer.tier === 'Premium' && <span className="tier-tag">Premium</span>}
            </td>
            <td>{ticket.category.name}</td>
            <td>
              <PriorityBadge priority={ticket.priority} />
            </td>
            <td>
              <StatusBadge status={ticket.status} />
            </td>
            <td>{ticket.assignedAgent?.fullName ?? 'Unassigned'}</td>
            <td>
              <SlaIndicator status={ticket.slaStatus} dueAtUtc={ticket.dueAtUtc} />
            </td>
            <td>{formatDateTime(ticket.createdAtUtc)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
