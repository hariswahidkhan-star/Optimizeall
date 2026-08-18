# 03 — Platform & Integration Matrix

The workbook's estate is **133 platforms across 13 areas**, each value-ranked 1–133 (`Platform Setup` col P; top 10 = gold rows). This document converts that estate into an **integration architecture** by asking, for every platform, the only question that matters for automation: *does an official API exist for the action the workbook wants, and does the platform's policy permit it?*

**Governing policy (`INT-001`, from Playbook technique 15 + Golden Rule 5 + QA check 5):**
1. Official APIs only.
2. Respect authentication scopes.
3. Respect published rate limits.
4. Respect platform automation policies.
5. **No scraping, no bots, no bulk automation** — ever, on any platform.
6. Where automation is unavailable, build a **human-assisted workflow** (prepare → approve → human executes → confirm → record).

## 3.1 Connector tiers

| Tier | Meaning | Build order |
|---|---|---|
| **T1 — Read+Write API** | Official API supports the write the workbook wants | MVP candidates |
| **T2 — Read-only API** | Official API for measurement only | MVP (cheap, high value, zero risk) |
| **T3 — Human-assisted** | No permitted API for the action; platform provides a native scheduler or manual route. Platform prepares, human executes, human confirms | MVP for outreach lane |
| **T4 — Manual / registry only** | One-off setup, submission forms, or account admin; tracked as tasks with evidence URLs, no connector | Backlog |
| **T5 — Prohibited** | Automation forbidden by policy or ToS | **Never build** |

## 3.2 The critical integrations (top-20 value ranks)

| Rank | Platform | Area | What the workbook wants | Official API | Tier | Auth | Notes / restrictions |
|---|---|---|---|---|---|---|---|
| 1 | LinkedIn Company Page | LinkedIn | 3 posts/week; reply to comments within 60 min | LinkedIn Marketing / Community Management APIs — **partner-gated, scope-approved** | T1 *(subject to app approval)* | OAuth 2.0, org scopes | Access must be verified against current LinkedIn developer terms before build (`INT-002`). Page reshares are not schedulable natively |
| 2 | LinkedIn Personal Profile | LinkedIn | All outreach: connect, message, follow-up, 15 comments/day | **None for these actions** | **T3 / T5** | — | Automating connects, DMs or comments is prohibited by ToS **and** by Golden Rule 5. Platform prepares copy-ready items; the human acts |
| 3 | LinkedIn Sales Navigator | LinkedIn | 30 qualified leads/day; saved searches; lead lists | No general-purpose export/search API for this use | **T3** | — | Human runs the saved search; platform ingests a manually exported/entered candidate set and does the qualification, enrichment, dedup and scoring |
| 4 | LinkedIn Articles | LinkedIn | 1 article/month per leader | Limited | T3 | — | **No canonical support** — original or rewritten content only |
| 5 | YouTube | Social | 1 long-form + 3 Shorts/week; chapters; UTM descriptions | YouTube Data API v3 | T1 | OAuth 2.0 | Quota-managed; Shorts scheduled individually |
| 6 | Website / Blog | Publishing | Originals first; author, sources, FAQ, internal links, schema | CMS API (WordPress REST or equivalent) | **T1 — the anchor integration** | App password / OAuth | The one place the platform can safely publish end-to-end |
| 7 | Zoom Webinars | Events | Monthly webinar; registrants → nurture | Zoom API | T1 | OAuth (S2S) | Registrant PII — consent + retention rules apply |
| 8 | Partnership / PR Outreach | Partnership/PR | 25 approaches/week | n/a (email) | T1 via ESP/mail | OAuth | Send via approved mail integration, per-item approval |
| 9 | Email Marketing (ESP) | Email & CRM | Welcome sequence; fortnightly newsletter; nurture | ESP API (**vendor not chosen — gap G-010**) | T1 | API key in vault | SPF/DKIM/DMARC required before first send |
| 10 | Bing Webmaster Tools + IndexNow | Analytics | Verify domain; IndexNow submissions | Bing Webmaster API + IndexNow | T1 | API key | Cheap, high-leverage for the ChatGPT retrieval pipeline (Playbook 3) |
| 11 | AI Answer Engines | SEO & Syndication | Monthly 20-prompt brand-citation audit | Provider APIs used as *measurement* | T2 | API key | Audit only — never to fabricate citations |
| 12 | PM World Journal | Publishing | Named-author monthly series | None | T4 | — | Editor relationship; tracked as tasks |
| 13 | Project Controls Expo (UK/USA/AUS) | Events | Speaker applications + awards entries | None | T4 | — | Dated deadlines → business calendar entries |
| 14 | Credential Engine Registry | Authority | Publish PCL-AI/PFL-AI/PML-AI in CTDL | Registry API / assisted upload | T4→T1 | — | Free; syndicated to US state systems |
| 15 | Google Knowledge Panel & Entity Graph | Authority | Entity parity; claim the panel | No API (claim flow) | T4 | — | Driven by schema + sameAs + Wikidata work |
| 16 | Education Schema Markup | SEO | Course List + EducationalOccupationalCredential | Site-side | T1 (via CMS) | — | Course Info rich result retired — do not implement |
| 17 | Google Search Console | Analytics | Weekly queries, positions, index coverage, links report | Search Console API | **T2 — MVP** | OAuth service account | Primary SEO truth source |
| 18 | Google Analytics | Analytics | Acquisition by channel; conversions; UTM campaigns | GA4 Data API | **T2 — MVP** | OAuth service account | Freshness disclosure required (`KPI-023`) |
| 19 | LinkedIn Newsletter | LinkedIn | Fortnightly issue | Limited | T3 | — | Beats LinkedIn Articles per Lists logging rule |
| 20 | LinkedIn Groups | LinkedIn | 3 useful answers/week | None | T5 (automation) / T3 (prep) | — | Answers are human-posted |

## 3.3 Areas summarised (all 133)

| Area | Count | Dominant tier | MVP connectors | Notes |
|---|---|---|---|---|
| LinkedIn | 7 | T3 (except Company Page T1) | none in MVP | The highest-value lane is the least automatable — this shapes the whole architecture |
| Social Media | 10 | T1/T3 | YouTube (phase 2) | Native schedulers exist for most (see 3.4) |
| Publishing | 20 | T1 for own site + Medium; T4 for the rest | Website/Blog | Canonical rules are hard gates |
| Community | 12 | **T5 for automation**, T3 for prep | none | Reddit, Quora, DEV, Stack Exchange all prohibit promotional automation |
| Directory/Review | 20 | T4 | none | Setup + periodic verification tasks |
| Events | 8 | T1 (Zoom) / T4 | none in MVP | Expo deadlines → calendar |
| Podcast | 5 | T4 | none | Pitch tracking only |
| Partnership/PR | 7 | T1 via mail, T4 for routes | mail (phase 2) | PR & Target Directory drives the task list |
| Paid Media | 4 | T1 | none | **Deliberately out of MVP** — spend controls first |
| Email & CRM | 4 | T1 | ESP (phase 2) | Vendor undecided (G-010) |
| SEO & Syndication | 7 | T1/T2 | GSC, IndexNow | Highest ROI per unit of build |
| Authority & Listings | 17 | T4 | none | High business value, low technical surface |
| Analytics | 3 | T2 | GSC, GA4, Clarity | MVP |

## 3.4 Native scheduling capability (workbook-verified, Aug 2026)

From `Content Scheduler` rows 106–125. This table is **operational truth the platform must respect** — it determines whether "schedule a post" is a platform action or a human action.

| Platform | Native scheduler | Window / limits | Schedulable | Not schedulable |
|---|---|---|---|---|
| LinkedIn (Page + personal) | Yes, free, in composer | 10 min – 3 months | Text, single image, single video, link | **Polls, documents/carousels, multi-image; Page reshares** |
| Facebook Page | Meta Business Suite Planner | 75 days; 25 posts/day Meta cap | Posts, Reels, Stories, bulk | Live video |
| Instagram | In-app + Business Suite | 25/day, 75 days | Feed, carousels, Reels; Stories via Business Suite | Stories from the IG app; Live |
| X (Twitter) | x.com desktop, free | Up to 18 months | Single posts | Threads, mobile, polls |
| Threads | Native since Jan 2025 | 75 days | Standard posts | Replies |
| TikTok | TikTok Studio (desktop, Business/Creator) | 15 min – **10 days only**; scheduled posts not editable | Videos | Mobile app; personal accounts |
| YouTube | Studio scheduled publish + Premieres | ~1 year | Long-form, Shorts (individually), Premieres | Batch-scheduling Shorts |
| Pinterest | Native Pin scheduler (Business) | 30 days; 10 queued | Standard image/video Pins | Bulk |
| Bluesky | **None** | — | — | Everything natively (third-party only) |
| Snapchat | Partial (Public Story) | Undocumented | Public Story snaps | Spotlight |
| Telegram Channel | Yes | 365 days; 100 queued | All message types | Recurring (needs bots) |
| WhatsApp Channel | **None** | — | — | Everything — manual only |
| Google Business Profile | Yes (newly rolled out — confirm in dashboard) | No published caps | Updates, Offers, Events | Mobile; recurring |
| Reddit | **Mod tools only**, in communities you moderate | Recurring | Text/link posts | Ordinary-user scheduling |
| Medium | Yes ("Schedule for later") | Publishes ~5 min after set time | Stories incl. into publications | Publications accepting drafts only |
| Newsletter / ESP | Yes, all modern ESPs | Effectively unlimited | Campaigns | — |

The workbook's recommended $0 stack (Metricool Free for 9 networks + native schedulers for LinkedIn/X/Telegram/Medium/ESP) is **a valid interim** and is recorded as decision D-11.

## 3.5 Required specification per connector (`INT-010`)

Every connector must declare, in configuration and in documentation:

| Field | Example |
|---|---|
| Official API availability | Yes / No / Partner-gated |
| Authentication | OAuth2 auth-code / client-credentials / API key / none |
| Scopes requested | least privilege, listed explicitly |
| Rate limits | requests/window, per credential |
| Read operations | endpoint → normalised entity |
| Write operations | endpoint → idempotency key strategy |
| **Restricted operations** | actions the connector must refuse to expose as tools |
| Webhooks | available? signature scheme? |
| Failure handling | retry class, backoff, circuit-breaker threshold |
| Health states | Connected / Degraded / RateLimited / AuthenticationExpired / Unavailable / Disabled |
| Data classification handled | Public / Internal / Confidential / Restricted |
| Verification date | with a **6-monthly re-verify obligation**, mirroring the workbook's own `Verified` column |

## 3.6 Systems of record (`DAT-020`)

The workbook is explicit that it is not the ledger. This must not regress.

| Domain | System of record | The Growth OS holds |
|---|---|---|
| Certification orders, applications, revenue | **PCI platform** (`Dashboard!A103`: "Revenue figures reconcile against PCI platform order references") | A reconciled snapshot + the PCI order reference; a lead may not be marked Converted without it |
| Customer / lead relationship records | **CRM** once selected (`UPGRADE NOTES` #18) — gap G-009 | Working pipeline until CRM exists; then sync with external IDs and conflict rules |
| Published website content | **CMS** | Content records, versions and performance |
| Web analytics | **GA4 / GSC / Clarity** | Normalised KPI snapshots with freshness stamps |
| Credentials | **Password vault** (`Accounts Register`: vault entry name only) | Credential *references* only — never secrets |
| Agent/workflow history, approvals, audit | **The Growth OS** | The authoritative record |
| Human HR scoring | Manager + HR process | Employee Score data under restricted RBAC (`CMP-014`) |
