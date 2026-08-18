/** Presentation helpers. Kept pure so they are trivially testable. */

const numberFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 });

/**
 * Formats money using the currency the server sent.
 *
 * The currency is never assumed: a workspace budgeted in EUR must not be rendered with a dollar
 * sign, and silently defaulting would misreport spend to an executive reading the dashboard.
 */
export function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: amount < 1 ? 4 : 2,
  }).format(amount);
}

export function formatNumber(value: number): string {
  return numberFormatter.format(value);
}

export function formatPercent(fraction: number): string {
  return new Intl.NumberFormat(undefined, {
    style: 'percent',
    maximumFractionDigits: 1,
  }).format(fraction);
}

export function formatDateTime(iso: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(iso));
}

/**
 * A relative description of when something happens or happened.
 *
 * Approvals expire, and "in 12 minutes" communicates urgency in a way an absolute timestamp does
 * not. The absolute time is still shown alongside, because a relative time alone is useless in an
 * audit conversation.
 */
export function formatRelative(iso: string, now: Date = new Date()): string {
  const target = new Date(iso).getTime();
  const deltaSeconds = Math.round((target - now.getTime()) / 1000);
  const absolute = Math.abs(deltaSeconds);

  const formatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });

  if (absolute < 60) {
    return formatter.format(deltaSeconds, 'second');
  }

  if (absolute < 3600) {
    return formatter.format(Math.round(deltaSeconds / 60), 'minute');
  }

  if (absolute < 86_400) {
    return formatter.format(Math.round(deltaSeconds / 3600), 'hour');
  }

  return formatter.format(Math.round(deltaSeconds / 86_400), 'day');
}

export function formatDuration(seconds: number): string {
  if (seconds < 1) {
    return `${Math.round(seconds * 1000)}ms`;
  }

  if (seconds < 60) {
    return `${seconds.toFixed(1)}s`;
  }

  const minutes = Math.floor(seconds / 60);
  const remainder = Math.round(seconds % 60);
  return `${minutes}m ${remainder}s`;
}

/**
 * Pretty-prints a JSON string for display, returning the original text when it will not parse.
 *
 * Returning the raw text on failure is deliberate: an approver must see exactly what the payload
 * is, and hiding a malformed payload behind an error message would conceal the very thing that
 * makes it suspicious.
 */
export function prettyJson(json: string): string {
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
}

/** Truncates the payload fingerprint for display while keeping enough to compare by eye. */
export function shortFingerprint(fingerprint: string): string {
  return `${fingerprint.slice(0, 8)}…${fingerprint.slice(-8)}`;
}
