import type { WorkflowTab } from './workflowTypes'

// ---------------------------------------------------------------------------
// Prism — Corporate Bond Credit-Rating Divergence Explainer.
// This is the MINIMAL seed pipeline for package 09 (the frontend shell). Package 10
// enriches node details, positions, and adds the full annotated pipeline.
//
// Prism RECONCILES provider ratings; it is NOT a trading agent. Vocabulary is limited to
// reconciliation, divergence, provenance, as-of, coverage, and notch gap. The heart is a
// deterministic C# core (Analysis/*) — the LLM agents only narrate and cite.
// ---------------------------------------------------------------------------

export const workflowTabs: WorkflowTab[] = [
  {
    id: 'rating-reconciliation',
    label: 'Rating Reconciliation Pipeline',
    description:
      'End-to-end reconciliation of corporate-bond credit ratings across providers: gather each provider verdict and the issuer fundamentals, decompose the notch gaps, raise deterministic red flags (stale input, missing coverage, outliers, methodology conflict), then assemble a cited dossier behind a scope-confirmation gate. Backed by Azure AI Search (rating corpus) and Azure Cosmos DB (dossier persistence).',
    nodes: [
      // ── Entry ────────────────────────────────────────────────────────────
      {
        id: 'issuer-selected',
        type: 'service',
        label: 'Issuer Selected',
        subtitle: 'service · reconciliations API',
        position: { x: 310, y: 20 },
        detail: {
          title: 'Issuer Selected',
          subtitle: 'service · reconciliations API',
          description:
            'Entry point when an analyst selects an issuer and an as-of date to reconcile. Validates the request and starts the reconciliation sweep over all covered providers.',
          sourceFiles: [
            'frontend/src/pages/IssuersPage.tsx',
            'backend/FinancialServices.Api/Controllers/ReconciliationsController.cs',
          ],
          responsibilities: [
            'Accept an issuerId + as-of date from the Issuers grid',
            'Validate the request and reject unknown fields',
            'Start the reconciliation sweep across covered providers',
          ],
          dataFlow: [
            'POST /api/v1/reconciliations → { issuerId, asOf, providers? }',
            'Request handed to the reconciliation orchestrator',
          ],
          technologies: ['ASP.NET Core', 'React', 'TanStack Query'],
        },
      },

      // ── Orchestrator ─────────────────────────────────────────────────────
      {
        id: 'reconciliation-orchestrator',
        type: 'agent',
        label: 'Reconciliation Orchestrator',
        subtitle: 'agent · orchestration',
        position: { x: 310, y: 150 },
        detail: {
          title: 'Reconciliation Orchestrator',
          subtitle: 'agent · orchestration',
          description:
            'Coordinates the sweep: fans out to the provider explainer and fundamentals agents, then hands their outputs to the deterministic decomposition and red-flag engines. Narrates; it never overrides the deterministic core.',
          sourceFiles: [
            'backend/FinancialServices.Api/Orchestration/PrismStreamingOrchestrator.cs',
            'backend/FinancialServices.Api/Orchestration/PrismSweepSteps.cs',
          ],
          responsibilities: [
            'Fan out to the provider explainer and fundamentals agents',
            'Collect provider verdicts and the issuer fundamentals snapshot',
            'Pass results to the deterministic decomposition + red-flag engines',
          ],
          dataFlow: [
            'Receives { issuerId, asOf }',
            'Invokes provider + fundamentals agents concurrently',
            'Forwards collected inputs to Analysis/*',
          ],
          technologies: ['Microsoft Agent Framework', 'Azure AI Foundry'],
        },
      },

      // ── Fan-out: two agents ──────────────────────────────────────────────
      {
        id: 'provider-explainer-agent',
        type: 'agent',
        label: 'Provider Explainer Agent',
        subtitle: 'agent · provider ratings',
        position: { x: 140, y: 280 },
        detail: {
          title: 'Provider Explainer Agent',
          subtitle: 'agent · provider ratings',
          description:
            'Retrieves each provider verdict (Moody\u2019s, Morningstar DBRS, MSCI) with its native letter, notch, as-of and input-as-of dates, and methodology citations. Grounds every statement in the indexed rating card — no fabrication.',
          sourceFiles: [
            'backend/FinancialServices.Api/Agents/ProviderExplainerAgent.cs',
            'backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs',
          ],
          responsibilities: [
            'Fetch each provider\u2019s letter, notch, and as-of dates',
            'Attach methodology-document citations for provenance',
            'Return MISSING_COVERAGE where a provider does not cover the issuer',
          ],
          dataFlow: [
            'Reads provider rating cards from Azure AI Search',
            'Normalizes letters to notches via NotchLadder',
            'Emits ProviderVerdict records with citations',
          ],
          technologies: ['Azure AI Foundry', 'Azure AI Search', 'Microsoft Agent Framework'],
        },
      },
      {
        id: 'fundamentals-agent',
        type: 'agent',
        label: 'Fundamentals Agent',
        subtitle: 'agent · EDGAR / FRED',
        position: { x: 480, y: 280 },
        detail: {
          title: 'Fundamentals Agent',
          subtitle: 'agent · EDGAR / FRED',
          description:
            'Assembles the issuer fundamentals snapshot as of the requested date from SEC EDGAR (XBRL facts) and FRED, including leverage and the latest filing date that anchors the stale-input check.',
          sourceFiles: [
            'backend/FinancialServices.Api/Agents/FundamentalsAgent.cs',
            'backend/FinancialServices.Api/Connectors/EdgarClient.cs',
            'backend/FinancialServices.Api/Analysis/FundamentalsCalculator.cs',
          ],
          responsibilities: [
            'Pull duration-coherent XBRL facts as of the requested date',
            'Compute leverage inputs used by the decomposition',
            'Surface the latest filing date for the freshness check',
          ],
          dataFlow: [
            'Calls SEC EDGAR + FRED via typed connectors',
            'Derives fundamentals in the deterministic FundamentalsCalculator',
            'Returns a point-in-time FundamentalSnapshot',
          ],
          technologies: ['SEC EDGAR', 'FRED', 'C# (deterministic)'],
        },
      },

      // ── Datastore: rating corpus ────────────────────────────────────
      {
        id: 'azure-ai-search',
        type: 'datastore',
        label: 'Azure AI Search',
        subtitle: 'datastore · rating corpus',
        position: { x: 110, y: 410 },
        detail: {
          title: 'Azure AI Search',
          subtitle: 'datastore · rating corpus',
          description:
            'Holds the indexed provider rating cards and methodology documents (the prism-ratings index). The provider explainer reads each verdict and its citations from here, grounding every provider claim in a retrievable source — no fabrication.',
          sourceFiles: [
            'backend/FinancialServices.Api/Services/SearchCorpus.cs',
            'tools/SeedData/Search/PrismSearchIndex.cs',
          ],
          responsibilities: [
            'Store labeled rating cards + methodology docs per issuer',
            'Serve provider verdicts and citations to the explainer',
            'Back hybrid (vector + keyword) retrieval for grounding',
          ],
          dataFlow: [
            'Seeded from tools/SeedData (labeled-synthetic corpus)',
            'Queried by SearchCorpus for an issuer\u2019s provider cards',
            'Returns letters, notches, and source doc ids',
          ],
          technologies: ['Azure AI Search', 'DefaultAzureCredential'],
        },
      },

      // ── Deterministic core: decomposition ────────────────────────────────
      {
        id: 'divergence-decomposer',
        type: 'service',
        label: 'Divergence Decomposer',
        subtitle: 'Analysis · deterministic',
        position: { x: 310, y: 410 },
        detail: {
          title: 'Divergence Decomposer',
          subtitle: 'Analysis · deterministic',
          description:
            'Pure C# core. For each provider pair it computes the notch gap and decomposes it into Weighting, Input, and a residual Methodology Adjustment bucket — with an exact reconciliation invariant. No LLM in this step.',
          sourceFiles: ['backend/FinancialServices.Api/Analysis/DivergenceDecomposer.cs'],
          responsibilities: [
            'Compute the signed notch gap for each provider pair',
            'Attribute the gap to Weighting / Input / MethodologyAdjustment buckets',
            'Guarantee buckets reconcile exactly to the gap (invariant guard)',
          ],
          dataFlow: [
            'Receives provider verdicts + fundamentals snapshot',
            'Produces PairDivergence records with signed bucket contributions',
            'Flags residual-dominated gaps for honest framing',
          ],
          technologies: ['C# (deterministic)', 'records'],
        },
      },

      // ── Deterministic core: red flags ────────────────────────────────────
      {
        id: 'red-flag-engine',
        type: 'service',
        label: 'Red-Flag Engine',
        subtitle: 'Analysis · deterministic',
        position: { x: 310, y: 540 },
        detail: {
          title: 'Red-Flag Engine',
          subtitle: 'Analysis · deterministic',
          description:
            'Pure C# rules that raise deterministic, date-only-UTC flags: STALE_INPUT (rating action predates the latest filing), MISSING_COVERAGE, OUTLIER_PROVIDER, and METHODOLOGY_CONFLICT. Each flag carries verbatim rule text and evidence references.',
          sourceFiles: [
            'backend/FinancialServices.Api/Analysis/RedFlagEngine.cs',
            'backend/FinancialServices.Api/Analysis/ReconciliationScoring.cs',
          ],
          responsibilities: [
            'Evaluate stale-input, coverage, outlier, and methodology-conflict rules',
            'Attach verbatim rule text + evidence references to each flag',
            'Compute the deterministic consensus summary and confidence score',
          ],
          dataFlow: [
            'Receives ratings, fundamentals, and pair divergences',
            'Emits RedFlag records with severity + evidence',
            'Derives consensusSummary and a 0..1 confidence score',
          ],
          technologies: ['C# (deterministic)', 'InvariantCulture'],
        },
      },

      // ── Human gate ───────────────────────────────────────────────────────
      {
        id: 'confirm-scope-gate',
        type: 'gate',
        label: 'Confirm Scope',
        subtitle: 'human gate · reconciliation only',
        position: { x: 210, y: 670 },
        detail: {
          title: 'Confirm Scope',
          subtitle: 'human gate · reconciliation only',
          description:
            'A human confirmation gate that keeps Prism in scope: the dossier explains rating divergence and provenance only. It reconciles provider views — it makes no investment decisions.',
          sourceFiles: [
            'backend/FinancialServices.Api/Orchestration/PrismStreamingOrchestrator.cs',
            'frontend/src/pages/ReconciliationPage.tsx',
          ],
          responsibilities: [
            'Present the assembled divergences and flags for analyst review',
            'Confirm the output stays within reconciliation scope',
            'Release the dossier once the analyst acknowledges it',
          ],
          dataFlow: [
            'Analyst reviews divergences + red flags',
            'Confirms reconciliation scope',
            'Gate opens → dossier is finalized',
          ],
          technologies: ['React', 'human-in-the-loop'],
        },
      },

      // ── Outcome ──────────────────────────────────────────────────────────
      {
        id: 'dossier-ready',
        type: 'outcome',
        label: 'Dossier Ready',
        subtitle: 'outcome · cited reconciliation',
        position: { x: 310, y: 800 },
        detail: {
          title: 'Dossier Ready',
          subtitle: 'outcome · cited reconciliation',
          description:
            'The finalized reconciliation dossier: provider verdicts, decomposed divergences, red flags, consensus summary, and confidence — every claim cited to a provider card or filing. Persisted for retrieval and export.',
          sourceFiles: ['backend/FinancialServices.Api/Controllers/ReconciliationsController.cs'],
          responsibilities: [
            'Assemble verdicts, divergences, flags, consensus, and confidence',
            'Persist the dossier to Azure Cosmos DB',
            'Expose it for point-read retrieval and HTML export',
          ],
          dataFlow: [
            'Deterministic outputs + narration merged into DossierResponse',
            'Written to Cosmos DB (prism)',
            'GET /api/v1/reconciliations/{id} returns the cited dossier',
          ],
          technologies: ['Azure Cosmos DB', 'ASP.NET Core'],
        },
      },

      // ── Datastore: dossier persistence ───────────────────────────
      {
        id: 'cosmos-db',
        type: 'datastore',
        label: 'Cosmos DB',
        subtitle: 'datastore · dossier store',
        position: { x: 310, y: 930 },
        detail: {
          title: 'Cosmos DB',
          subtitle: 'datastore · dossier store',
          description:
            'Persists each finalized reconciliation dossier (partitioned by issuerId) and the audit events for the sweep. Point-reads return a dossier by id for retrieval and export.',
          sourceFiles: [
            'backend/FinancialServices.Api/Services/CosmosDossierStore.cs',
            'backend/FinancialServices.Api/Services/AuditService.cs',
          ],
          responsibilities: [
            'Persist dossiers keyed by issuerId + dossier id',
            'Record audit events for each reconciliation (ids + counts only)',
            'Serve point-reads for GET /reconciliations/{id}',
          ],
          dataFlow: [
            'DossierResponse written on completion of the sweep',
            'Audit event recorded (no PII)',
            'Point-read by id returns the cited dossier',
          ],
          technologies: ['Azure Cosmos DB', 'DefaultAzureCredential'],
        },
      },
    ],
    edges: [
      { id: 'e-issuer-orch', source: 'issuer-selected', target: 'reconciliation-orchestrator' },
      { id: 'e-orch-provider', source: 'reconciliation-orchestrator', target: 'provider-explainer-agent' },
      { id: 'e-orch-fundamentals', source: 'reconciliation-orchestrator', target: 'fundamentals-agent' },
      { id: 'e-provider-search', source: 'provider-explainer-agent', target: 'azure-ai-search', label: 'reads' },
      { id: 'e-provider-decomp', source: 'provider-explainer-agent', target: 'divergence-decomposer' },
      { id: 'e-fundamentals-decomp', source: 'fundamentals-agent', target: 'divergence-decomposer' },
      { id: 'e-decomp-redflag', source: 'divergence-decomposer', target: 'red-flag-engine' },
      { id: 'e-redflag-gate', source: 'red-flag-engine', target: 'confirm-scope-gate', label: 'escalation', dashed: true },
      { id: 'e-gate-dossier', source: 'confirm-scope-gate', target: 'dossier-ready' },
      { id: 'e-dossier-cosmos', source: 'dossier-ready', target: 'cosmos-db', label: 'persists' },
    ],
  },
  {
    id: 'market-context',
    label: 'Live Market Context (Morningstar)',
    description:
      'A CONTEXT-ONLY companion to the reconciliation: live Morningstar analyst research for a real listed security, fetched over the provider\u2019s hosted MCP server (Streamable HTTP + OAuth 2.1). Deliberately separate from the rating engine \u2014 it never feeds a notch, gap, or flag, and never expresses a buy/sell/hold view (P4). The runtime reuses the OAuth token captured by the one-time discovery-CLI login (headless refresh, no browser); fictional Prism issuers return a clean \u201Cnot covered\u201D state because Morningstar only knows listed securities.',
    nodes: [
      {
        id: 'mc-request',
        type: 'service',
        label: 'Context Lookup',
        subtitle: 'service · reconciliation page',
        position: { x: 310, y: 20 },
        detail: {
          title: 'Context Lookup',
          subtitle: 'service · reconciliation page',
          description:
            'The analyst enters a real ticker, ISIN, or company name in the live Market Context panel; TanStack Query issues a read-only GET. The panel is decoupled from the (illustrative) issuer on purpose.',
          sourceFiles: [
            'frontend/src/components/prism/MorningstarContextPanel.tsx',
            'frontend/src/hooks/useMorningstarContext.ts',
          ],
          responsibilities: [
            'Accept a real ticker / ISIN / company name',
            'Issue GET /api/v1/market-context/morningstar?identifier=…',
            'Render provider states (ok / not covered / re-login / unavailable) honestly',
          ],
          dataFlow: [
            'User submits an identifier',
            'GET /api/v1/market-context/morningstar?identifier=…',
            'Response cached 5 min per identifier',
          ],
          technologies: ['React', 'TanStack Query', 'shadcn/ui'],
        },
      },
      {
        id: 'mc-api',
        type: 'service',
        label: 'Market Context API',
        subtitle: 'service · MorningstarContextService',
        position: { x: 310, y: 160 },
        detail: {
          title: 'Market Context API',
          subtitle: 'service · MorningstarContextService',
          description:
            'Validates the identifier and orchestrates two MCP tool calls. Every failure mode (provider off, not covered, re-login, upstream error) is caught and mapped to a status \u2014 never a 5xx (P1 fail-soft). Logs ids + counts only (P6).',
          sourceFiles: [
            'backend/FinancialServices.Api/Controllers/MarketContextController.cs',
            'backend/FinancialServices.Api/Services/MarketContext/MorningstarContextService.cs',
          ],
          responsibilities: [
            'Validate the identifier (charset + length)',
            'Call id-lookup, then analyst-research',
            'Map every outcome to a renderable status (fail-soft)',
          ],
          dataFlow: [
            'identifier → morningstar-id-lookup-tool → Morningstar id',
            'id → morningstar-analyst-research-tool → research sections',
            'MorningstarContextResponse (200 for every state)',
          ],
          technologies: ['ASP.NET Core', 'ModelContextProtocol.Core'],
        },
      },
      {
        id: 'mc-oauth',
        type: 'gate',
        label: 'Headless OAuth',
        subtitle: 'gate · token reuse',
        position: { x: 310, y: 300 },
        detail: {
          title: 'Headless OAuth',
          subtitle: 'gate · token reuse',
          description:
            'Reuses the refresh token the one-time discovery-CLI login cached (git-ignored FileTokenCache + the dynamically-registered client). Refreshes silently at runtime \u2014 it never opens a browser; a missing/expired token surfaces as a ReloginRequired status rather than blocking.',
          sourceFiles: [
            'backend/FinancialServices.Api/Connectors/Mcp/ProviderOAuth.cs',
            'backend/FinancialServices.Api/Connectors/Mcp/FileTokenCache.cs',
            'backend/FinancialServices.Api/Connectors/Mcp/DcrClientStore.cs',
          ],
          responsibilities: [
            'Load the cached token + dynamically-registered client',
            'Refresh the access token headlessly (no browser at runtime)',
            'Raise ReloginRequired when re-authentication is needed',
          ],
          dataFlow: [
            'Load .prism/tokens/morningstar.json (+ .client.json)',
            'SDK refreshes the bearer token as needed',
            'Bearer token attached to the MCP session',
          ],
          technologies: ['OAuth 2.1 (PKCE + RFC 7591 DCR)', 'DefaultAzureCredential (prod seam)'],
        },
      },
      {
        id: 'mc-mcp',
        type: 'service',
        label: 'Morningstar MCP',
        subtitle: 'service · hosted MCP server',
        position: { x: 310, y: 440 },
        detail: {
          title: 'Morningstar MCP',
          subtitle: 'service · hosted MCP server',
          description:
            'Morningstar\u2019s hosted MCP gateway (Streamable HTTP), reached only over an SSRF host allow-list. Two tools are used: id-lookup (ticker/ISIN/name → Morningstar id) and analyst-research (id → latest report). Covers listed securities only.',
          sourceFiles: [
            'backend/FinancialServices.Api/Connectors/Mcp/McpToolSessionFactory.cs',
            'backend/FinancialServices.Api/Connectors/Mcp/McpToolSession.cs',
            'backend/FinancialServices.Api/Connectors/Mcp/ProviderMcpEndpointGuard.cs',
          ],
          responsibilities: [
            'Connect to https://mcp.morningstar.com/mcp (host-allowlisted, SEC-03)',
            'tools/call morningstar-id-lookup-tool',
            'tools/call morningstar-analyst-research-tool',
          ],
          dataFlow: [
            'initialize handshake (protocol version negotiated by the SDK)',
            'id-lookup → { morningstar_id, name, type, exchange }',
            'analyst-research → { results: [{ title, published_at, content, url }] }',
          ],
          technologies: ['Model Context Protocol', 'Streamable HTTP'],
        },
      },
      {
        id: 'mc-render',
        type: 'outcome',
        label: 'Attributed Research',
        subtitle: 'outcome · context panel',
        position: { x: 310, y: 580 },
        detail: {
          title: 'Attributed Research',
          subtitle: 'outcome · context panel',
          description:
            'The parser surfaces Morningstar\u2019s own research sections (title, date, excerpt, source link) \u2014 attributed third-party content, trimmed server-side, shown with a standing \u201Cnot investment advice\u201D label. It is never merged into Prism\u2019s reconciliation output (P4).',
          sourceFiles: [
            'backend/FinancialServices.Api/Services/MarketContext/MorningstarResponseParser.cs',
            'backend/FinancialServices.Api/Models/MarketContextDtos.cs',
            'frontend/src/components/prism/MorningstarContextPanel.tsx',
          ],
          responsibilities: [
            'Parse the research sections defensively (no invented fields, P1)',
            'Trim excerpts + cap section count (light panel, limited licensed-data surface)',
            'Render with source links + a P4-safe disclaimer',
          ],
          dataFlow: [
            'results[] → MarketContextSection[] (title, publishedAt, excerpt, url)',
            'Rendered as attributed cards with "Read on Morningstar" links',
          ],
          technologies: ['System.Text.Json', 'React', 'shadcn/ui'],
          keyFacts: [
            'Context only — never a buy/sell/hold view (P4)',
            'Separate from the rating reconciliation — never feeds a notch/gap/flag',
            'Fictional Prism issuers return "not covered" (Morningstar knows listed securities only)',
          ],
        },
      },
    ],
    edges: [
      { id: 'e-mc-request-api', source: 'mc-request', target: 'mc-api' },
      { id: 'e-mc-api-oauth', source: 'mc-api', target: 'mc-oauth' },
      { id: 'e-mc-oauth-mcp', source: 'mc-oauth', target: 'mc-mcp', label: 'bearer token' },
      { id: 'e-mc-mcp-render', source: 'mc-mcp', target: 'mc-render', label: 'sections' },
    ],
  },
]
