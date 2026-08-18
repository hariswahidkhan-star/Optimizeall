# OptimizeAll — Azure Deployment and Operations

- **Document ID:** OA-DEP-001
- **Version:** 1.0

---

## 1. Topology

```
                    ┌──────────────┐
   Users ─────────► │ Front Door   │  TLS, WAF, global routing
                    └──────┬───────┘
                           │
              ┌────────────▼─────────────┐
              │ Container Apps Environment│  VNet-integrated
              │  ┌────────┐ ┌──────────┐  │
              │  │  web   │ │   api    │  │  external ingress
              │  └────────┘ └────┬─────┘  │
              │            ┌─────▼─────┐  │
              │            │  worker   │  │  no ingress, KEDA on queue depth
              │            └─────┬─────┘  │
              └──────────────────┼────────┘
                                 │  private endpoints only
        ┌────────────┬───────────┼────────────┬──────────────┐
        ▼            ▼           ▼            ▼              ▼
  PostgreSQL 16   Redis 7    Key Vault    Blob Storage   Log Analytics
  + pgvector      Premium    RBAC only    GZRS           + App Insights
  Zone-redundant
```

Nothing in the data tier has a public endpoint. A database protected only by a password on the open
internet is one credential leak away from a breach, and this one holds every tenant's data.

## 2. Sizing by environment

| | Development | Staging | Production |
|---|---|---|---|
| API replicas | 0–3 (scale to zero) | 1–5 | 2–20 |
| Worker replicas | 0–3 | 1–5 | 2–30 |
| PostgreSQL | B_Standard_B2s, 32 GB | GP_Standard_D2ds_v5, 128 GB | GP_Standard_D4ds_v5, 256 GB, zone-redundant HA |
| Redis | Basic C0 | Standard C1 | Premium P1 |
| Backup retention | 7 days | 14 days | 35 days, geo-redundant |
| Log retention | 30 days | 30 days | 90 days |

Development scales to zero deliberately: an idle environment should cost nothing.

## 3. Deploying

### Prerequisites

Azure subscription with Contributor and User Access Administrator; a container registry;
Entra ID app registration for OIDC; a storage account for Terraform state.

### Steps

```bash
# 1. Provision
cd infra/terraform
terraform init -backend-config=backends/prod.hcl
terraform plan  -var-file=environments/prod.tfvars -out=prod.plan
terraform apply prod.plan

# 2. Prepare the database
#    Migrations run as the server administrator; the application connects as optimizeall_app,
#    which is neither the owner nor a superuser — both bypass row-level security unconditionally.
psql "$ADMIN_CONNECTION" -f infra/sql/create-application-role.sql

cd ../../backend
dotnet dotnet-ef database update \
  --project src/OptimizeAll.Infrastructure \
  --startup-project src/OptimizeAll.Infrastructure

# 3. Verify isolation is actually in force before any tenant data exists
psql "$APP_CONNECTION" -f infra/sql/verify-rls.sql

# 4. Publish images and roll out
az acr build -r "$REGISTRY" -t optimizeall/api:$VERSION    -f backend/src/OptimizeAll.Api/Dockerfile    .
az acr build -r "$REGISTRY" -t optimizeall/worker:$VERSION -f backend/src/OptimizeAll.Worker/Dockerfile .
az acr build -r "$REGISTRY" -t optimizeall/web:$VERSION    -f frontend/Dockerfile                       .

terraform apply -var-file=environments/prod.tfvars \
  -var="api_image=$REGISTRY/optimizeall/api:$VERSION" \
  -var="worker_image=$REGISTRY/optimizeall/worker:$VERSION" \
  -var="web_image=$REGISTRY/optimizeall/web:$VERSION"
```

### Order matters

Migrations run **before** the new images roll out, and every migration must be backward compatible
with the currently deployed version. Container Apps replaces revisions gradually, so for a period
both versions are live against one schema. A migration that drops a column the old version still
reads will take production down during what looks like a routine deploy.

The rule: **expand, deploy, contract.** Add the new column, deploy code that writes both and reads
the new, then in a later release remove the old column.

## 4. Zero-downtime rollout

Container Apps single-revision mode shifts traffic once the new revision passes its readiness probe.
Readiness checks dependencies; liveness deliberately does not, because a restart cannot fix a
database outage and conflating the two turns a dependency incident into a restart storm.

Rollback is a traffic weight change, not a redeploy:

```bash
az containerapp revision list --name ca-optimizeall-prod-api -g rg-optimizeall-prod -o table
az containerapp ingress traffic set --name ca-optimizeall-prod-api -g rg-optimizeall-prod \
  --revision-weight <previous-revision>=100
```

Seconds, not minutes. Which is why a schema change that makes rollback impossible is treated as a
release-blocking design flaw rather than an inconvenience.

## 5. Backup and recovery

| | Target | Mechanism |
|---|---|---|
| RPO | ≤ 5 minutes | Continuous PostgreSQL transaction log backup |
| RTO | ≤ 60 minutes | Point-in-time restore + revision rollout |
| Retention | 35 days (prod) | Geo-redundant |

**Monthly restore drill.** Restore production to a scratch server at a timestamp, apply migrations,
run the verification suite, record the wall-clock time, destroy it. An untested backup is not a
backup, and the first time anyone discovers that is always during an incident.

## 6. Monitoring

| Signal | Threshold | Severity |
|---|---|---|
| Audit chain verification failed | any | **SEV0** |
| Execution refused on payload mismatch | any | **SEV0** |
| API 5xx | > 10 in 5 min | SEV1 |
| Run queue depth | > 1000 for 10 min | SEV1 |
| All AI providers unavailable | > 5 min | SEV1 |
| PostgreSQL storage | > 80% | SEV2 |
| Approval queue age | p95 > 4 h | SEV3 |

Severity 0 is reserved for the two conditions that mean the platform's governance guarantees have
stopped holding. Everything else is an availability or capacity problem, which is a different kind
of bad.

Every alert links to a runbook entry. An alert without a runbook is an interruption, not a signal.

## 7. Operational runbooks

### The kill switch

```bash
curl -X POST "$API/api/v1/workspaces/$WORKSPACE_ID/kill-switch" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"environment":"Production","reason":"Investigating anomalous outreach volume"}'
```

Halts every external action by every agent in that workspace within seconds. Engaging needs one
permission and a reason; releasing additionally requires step-up authentication. Under pressure, the
safe action should be the fast one.

### An AI provider is down

No action required. The router fails over to configured fallbacks, opens the provider's circuit
after five consecutive failures, and probes recovery with a single request. If every provider is
down, the platform stays up and queues work — verify by checking that queue depth is growing while
error rate is not.

### Runs are stuck in `Running`

The lease reclaimer sweeps every 30 seconds and re-queues runs whose lease has expired. If runs
persist beyond a few minutes, check whether workers are running at all, and whether Redis is
reachable — a worker that cannot dequeue looks identical to one that has no work.

### The audit chain verification failed

Treat as a potential integrity incident. Do not remediate first: preserve state, capture the failing
sequence number, engage the kill switch for the affected tenant, and escalate. The chain is designed
so that tampering is detectable; a detection is meaningful.

## 8. Cost

See `docs/09-cost-model.md`. Summary: infrastructure is a minor line item next to AI provider spend
at any meaningful usage, which is why budget enforcement lives in the domain rather than in a
dashboard.
