# ADR 0005 — Own the chat abstraction rather than adopt a framework

**Status:** Accepted · **Date:** 2026-08-18

## Context

The platform must treat OpenAI, Anthropic and Google as interchangeable. Frameworks exist for this —
Semantic Kernel, LangChain and its .NET ports — and they offer agent loops, memory and tool calling
out of the box.

## Decision

Define our own `ChatRequest` / `ChatResponse` / `ToolDefinition` types and write a thin adapter per
provider, using each vendor's official SDK where one exists.

The determining factor is the tool-calling loop. In this platform that loop is not a convenience; it
is where authorisation, budget enforcement, kill-switch checks and approval gating happen. A
framework that owns the loop owns the enforcement point, and adapting a framework's extension model
to enforce "this tool call requires two named human approvers and a payload hash match" is harder
than writing the loop.

## Consequences

**Good.** The enforcement point is ours, in one file, fully tested. Adding a provider means
implementing one interface. An architecture test asserts that no provider SDK type crosses the
Infrastructure boundary, so "interchangeable" is enforced rather than intended. No framework
upgrade can change how tool authorisation behaves.

**Costly.** We implement per-provider translation ourselves, including the differences that make
providers genuinely non-uniform — Anthropic takes the system prompt as a separate field, Gemini names
the assistant turn "model" and returns no tool-call id. We forgo framework features we would
otherwise get free. When a provider adds a capability, we add support for it rather than waiting for
a package.

**Revisit when.** A framework's extension model becomes expressive enough to host the governance
controls without weakening them. That is a high bar, and one worth re-testing rather than assuming.
