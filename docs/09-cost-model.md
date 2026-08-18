# OptimizeAll — Cost Model

- **Document ID:** OA-FIN-001
- **Version:** 1.0

---

## 1. How to read this

Every figure below is an **estimate produced from published list prices and stated assumptions**, not
a measured result. Nothing has been run at load yet. Two categories differ in how much to trust them:

- **Azure infrastructure** — reasonably predictable. Sizes are fixed by the Terraform configuration;
  the main uncertainty is egress and log volume.
- **AI provider spend** — dominated by usage, which is a property of how customers use the platform
  rather than of the platform. Treat the per-run figures as arithmetic on assumptions, and replace
  them with measurements as soon as real runs exist.

Prices change. The platform reflects this: model prices live in configuration, not in code, and the
pricing service **refuses to price an unknown model** rather than costing it at zero — an uncosted
run would silently bypass every budget ceiling and surface as a surprise invoice.

---

## 2. Azure infrastructure

Monthly, `westeurope` list prices, excluding tax and any commitment discount.

### Production

| Component | Configuration | Est. monthly (USD) |
|---|---|---|
| Container Apps — API | 2–20 replicas, 1 vCPU / 2 GiB, ~30% average utilisation | 180 |
| Container Apps — Worker | 2–30 replicas, 1 vCPU / 2 GiB, bursty | 260 |
| Container Apps — Web | 2 replicas, 0.25 vCPU / 0.5 GiB | 25 |
| PostgreSQL Flexible Server | GP_Standard_D4ds_v5, 256 GB, zone-redundant HA | 720 |
| PostgreSQL backup | 256 GB geo-redundant, 35 days | 60 |
| Redis Cache | Premium P1, 6 GB | 420 |
| Key Vault | Standard, ~1M operations | 5 |
| Blob Storage | 500 GB GZRS + operations | 45 |
| Log Analytics | ~50 GB/month ingest, 90-day retention | 190 |
| Application Insights | Included in the workspace above | — |
| Front Door | Standard + ~1 TB egress | 120 |
| Container Registry | Standard | 20 |
| **Total** | | **≈ 2,045** |

Zone-redundant HA roughly doubles the PostgreSQL line. It is bought to survive a zone outage; if a
tenant's contract does not require that, it is the first thing to reconsider.

### Staging

Same shape, smaller: no HA, Standard Redis, 14-day retention. **≈ 520/month.**

### Development

Scales to zero when idle. **≈ 120/month**, most of it the database, which cannot scale to zero.

---

## 3. AI provider spend

### Per-run arithmetic

Assumptions, stated so they can be argued with: a typical agent run makes 3 model calls, sends
roughly 8,000 prompt tokens per call (system prompt, memory, retrieved knowledge, conversation), and
produces 1,200 completion tokens.

That is 24,000 prompt and 3,600 completion tokens per run.

| Model | Input / Output per 1M | Est. per run |
|---|---|---|
| Claude Opus 5 | $5.00 / $25.00 | $0.21 |
| Claude Sonnet 5 | $3.00 / $15.00 | $0.13 |
| Claude Haiku 4.5 | $1.00 / $5.00 | $0.04 |

The seeded workforce defaults to Sonnet 5, which is the deliberate middle: capable enough for
drafting and analysis, roughly a third the cost of Opus. Agents whose output is reviewed by a human
before it matters — draft content, research — are the ones where a cheaper model is defensible.
Agents whose mistakes are expensive to catch are not.

### Monthly, by tenant size

| Tenant profile | Runs/month | Est. AI spend |
|---|---|---|
| Small — 5 agents, light use | 2,000 | $260 |
| Medium — 15 agents, daily schedules | 12,000 | $1,560 |
| Large — 29 agents, heavy use | 50,000 | $6,500 |

Embeddings are a rounding error by comparison: at $0.02 per 1M tokens, ingesting 10,000 documents of
2,000 tokens each costs about $0.40.

### The shape of the bill

At any meaningful usage, **AI spend exceeds infrastructure spend by an order of magnitude**. A single
medium tenant's provider bill is comparable to the entire production infrastructure. This is why
budget enforcement is a domain invariant checked before each provider call rather than a dashboard
someone reviews monthly — by the time a monthly review notices, the money is gone.

---

## 4. Unit economics

At 100 tenants of mixed size (60 small, 30 medium, 10 large):

| | Monthly |
|---|---|
| Infrastructure (production + staging + dev) | 2,700 |
| AI provider spend | 127,600 |
| **Total** | **130,300** |
| **Per tenant** | **1,303** |

Infrastructure is 2% of cost. The platform's margin is therefore almost entirely a function of AI
spend per tenant, which is what the per-workspace budget cap exists to make predictable — for the
customer and for the operator.

---

## 5. Cost controls, and what each one actually prevents

| Control | Prevents |
|---|---|
| Per-run token, cost, iteration, tool-call and wall-clock ceilings | One agent consuming a month's budget in an afternoon |
| Per-workspace monthly cap, checked at queue time | Many individually-affordable runs adding up to an overspend |
| Alert at 80% of budget | Discovering the cap only when work stops |
| Model policy per agent | Paying Opus prices for work Haiku does adequately |
| Provider failover with recorded cost | A vendor outage silently routing everything to a costlier model |
| Refusing to price an unknown model | An uncosted model bypassing every ceiling above |
| Scale-to-zero in non-production | Paying for idle environments |

---

## 6. What would change these numbers most

1. **Model choice.** Moving the drafting agents from Sonnet to Haiku cuts their line by ~70%. Whether
   quality holds is an empirical question this platform is well set up to answer, because every run
   records its model and its approval outcome.
2. **Prompt caching.** Agent system prompts and retrieved knowledge are stable within a run and often
   across runs. Provider-side caching could plausibly cut prompt-token cost substantially. Not yet
   implemented; the largest single unexploited saving.
3. **Retrieval discipline.** Prompt tokens dominate the per-run cost, and retrieved knowledge
   dominates prompt tokens. Tightening `semanticRecallLimit` is a direct lever.
4. **Zone-redundant HA.** The largest single infrastructure line. Justified for production, hard to
   justify anywhere else.
