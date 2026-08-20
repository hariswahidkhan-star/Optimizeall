"""UUIDv7 generation.

Phase 4 chose time-ordered UUIDs so index locality survives without exposing row
counts the way a sequence does. Generated application-side so an identifier exists
before the row does — the audit chain needs it at creation time, not after insert.
"""

from __future__ import annotations

import os
import time
import uuid


def uuid7() -> uuid.UUID:
    """Return a UUIDv7 (RFC 9562): 48-bit millisecond timestamp, then randomness."""
    ms = int(time.time() * 1000)
    rand = os.urandom(10)
    value = ms.to_bytes(6, "big") + rand
    b = bytearray(value)
    b[6] = (b[6] & 0x0F) | 0x70  # version 7
    b[8] = (b[8] & 0x3F) | 0x80  # RFC 4122 variant
    return uuid.UUID(bytes=bytes(b))
