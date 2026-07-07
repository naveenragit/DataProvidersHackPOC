# Plan 12 — Deployment, Observability & Demo Readiness

**Objective:** Get a green deploy to Azure Container Apps (or the documented localhost-against-live-
Foundry fallback), turn on end-to-end observability, wire the all-green control decision, severity-rank
the flags, and rehearse the 3-minute demo three times. Days 4–5.

**Depends on:** all prior plans. **Primary day:** 4–5.

> Infra is provisioned manually (per planning decision) — this plan deploys **to existing resources**
> and documents the localhost fallback rather than authoring IaC from scratch.

---

## 1. Container Apps deployment (assume resources exist)

- [ ] Deploy the **ASP.NET Core API** and the **Node CopilotKit sidecar** to the **same Container Apps
      environment** (⚠ avoid App Service — Windows plans buffer SSE via IIS/ARR and break streaming;
      ACA/Envoy is clean)
- [ ] `minReplicas: 1` on **both** server apps (scale-to-zero cold start would hit during judging)
- [ ] Heartbeat every ~20s (ingress/LB idles out at 230–240s) so SSE streams survive
- [ ] Frontend: Azure Static Web Apps Free (auto HTTPS) or serve `dist/` from a container
- [ ] `DefaultAzureCredential` promotes local → managed identity with no code change; set
      `AZURE_CLIENT_ID` for a user-assigned identity if used
- [ ] Confirm the RBAC from Plan 00 is assigned and **propagated** ("works locally, 403 in cloud" =
      wait ~10 min, don't debug)
- [ ] If `azd` is used against existing resources, `azd deploy <service>` per app (1–3 min each)

## 2. Observability (turn on Day 1, verify here)

- [ ] App Insights end-to-end tracing across sidecar + API (⚠ ARC-09 — one correlated trace per action)
- [ ] Agent spans (`Rewind.Agents`) + analysis spans (`Rewind.Analysis`) visible in the trace
- [ ] Verify `APPLICATIONINSIGHTS_CONNECTION_STRING` is required in the deployed (non-dev) environment
      (⚠ ARC-14 — no silent telemetry disable)
- [ ] Reasoning traces + human-in-the-loop + observability are judging assets — confirm they're visible

## 3. Demo hardening

- [ ] Wire the **all-green control decision** end-to-end (exoneration path, Plan 05/09) — or narrate it
      if cutting (⚠ global cut-line #3)
- [ ] Severity-rank the red flags (timestamp gotcha first); ensure the red-arrow animation is smooth
- [ ] Confirm the **pre-rendered dossier PDF** fallback works offline (Plan 10 — mandatory)
- [ ] Stopwatch vs 3-week ghost baseline verified start/end (Plan 09)
- [ ] Scrub all surfaces for banned language (buy/sell/hold/recommend/allocate/trade/alpha/signal) and
      "point-in-time vintage" overclaims — narration says "observation-date filtering"

## 4. Resilience & load

- [ ] Confirm retry-with-jitter + circuit breakers on Azure calls (⚠ ARC-11); chatty agents on
      `gpt-4.1-mini`; request the TPM bump landed (avoid 429s during the live fan-out)
- [ ] Smoke-test the full flow against live Foundry + AI Search + Cosmos

## 5. Rehearsal (Day 5)

- [ ] Run the 3-minute script three times: inquiry → scope gate → sweep (streaming cards) → two-lane
      timeline → red flag (rule + docs) → control run → export → stopwatch (~90s vs 3 weeks)
- [ ] Time each beat; trim narration to hit 3:00
- [ ] Prepare the localhost fallback (Plan-wide cut-line #4) in case cloud is degraded during judging

## Acceptance criteria

- Both servers run in one Container Apps environment with `minReplicas: 1` and SSE intact — **or** the
  documented localhost-against-live-Foundry demo path is rehearsed and green
- One correlated App Insights trace covers a full reconstruction; agent/analysis spans present
- Control decision runs all-green; flags are severity-ranked; pre-rendered PDF opens offline
- No banned language anywhere; as-of framing is honest
- The 3-minute demo has been rehearsed 3× and lands within time

## Cut-lines (global, in order)

1. Live PDF export → pre-rendered (Plan 10)
2. MNPI/restricted-list flag → keep timestamp gotcha + hindsight note (Plan 05)
3. All-green control → narrate instead of run (this plan)
4. Azure deploy → demo from localhost against live Foundry + AI Search (this plan)

**Guaranteed-demoable core:** Vault Forensics + deterministic timeline + the one timestamp red flag,
streamed as agent cards. That alone lands the story and every hard constraint.
