# Architecture Decision Records

Each record captures a decision that was not obvious, the alternatives considered, and what it costs.
The cost section is the point: a decision recorded without its downside reads as advocacy, and the
person who has to revisit it in two years needs to know what was traded away.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-postgres-with-rls.md) | PostgreSQL with row-level security for tenant isolation | Accepted |
| [0002](0002-hand-rolled-mediator.md) | Hand-rolled dispatcher instead of a mediator library | Accepted |
| [0003](0003-payload-fingerprinting.md) | Cryptographic payload binding for approvals | Accepted |
| [0004](0004-pgvector-over-dedicated-store.md) | pgvector rather than a dedicated vector database | Accepted |
| [0005](0005-provider-abstraction.md) | Own the chat abstraction rather than adopt a framework | Accepted |
