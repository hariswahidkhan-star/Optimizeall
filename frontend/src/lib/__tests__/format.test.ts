import { describe, expect, it } from 'vitest';
import { formatDuration, formatMoney, formatRelative, prettyJson, shortFingerprint } from '@/lib/format';

describe('formatMoney', () => {
  it('uses the currency the server supplied rather than assuming one', () => {
    // Rendering a EUR-budgeted workspace with a dollar sign would misreport spend to an executive.
    expect(formatMoney(1234.5, 'EUR')).toContain('€');
    expect(formatMoney(1234.5, 'USD')).toContain('$');
  });

  it('shows more precision for sub-unit amounts, because agent runs often cost fractions of a cent', () => {
    expect(formatMoney(0.0042, 'USD')).toBe('$0.0042');
  });
});

describe('formatRelative', () => {
  const now = new Date('2026-03-01T12:00:00Z');

  it('describes an upcoming expiry, so urgency is legible at a glance', () => {
    expect(formatRelative('2026-03-01T12:30:00Z', now)).toContain('30');
  });

  it('describes a past instant', () => {
    expect(formatRelative('2026-03-01T09:00:00Z', now)).toContain('3');
  });
});

describe('prettyJson', () => {
  it('formats valid JSON for review', () => {
    expect(prettyJson('{"a":1}')).toBe('{\n  "a": 1\n}');
  });

  it('returns malformed input unchanged rather than hiding it', () => {
    // An approver must see exactly what the payload is. Replacing a malformed payload with an error
    // message would conceal the very thing that makes it suspicious.
    expect(prettyJson('not json')).toBe('not json');
  });
});

describe('shortFingerprint', () => {
  it('keeps both ends so two fingerprints can be compared by eye', () => {
    const fingerprint = 'a'.repeat(28) + 'b'.repeat(28) + 'c'.repeat(8);
    const shortened = shortFingerprint(fingerprint);

    expect(shortened.startsWith('aaaaaaaa')).toBe(true);
    expect(shortened.endsWith('cccccccc')).toBe(true);
  });
});

describe('formatDuration', () => {
  it('uses milliseconds below a second', () => {
    expect(formatDuration(0.25)).toBe('250ms');
  });

  it('uses minutes and seconds above a minute', () => {
    expect(formatDuration(125)).toBe('2m 5s');
  });
});
