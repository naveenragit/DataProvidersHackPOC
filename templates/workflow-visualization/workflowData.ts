import type { WorkflowTab } from './workflowTypes'

// ---------------------------------------------------------------------------
// Replace the sample data below with your application's actual workflow.
// Each tab represents a major workflow (e.g., Meeting Intelligence, Portfolio
// Intelligence). Nodes map to agents, services, gates, and data stores.
// ---------------------------------------------------------------------------

export const workflowTabs: WorkflowTab[] = [
  {
    id: 'meeting-intelligence',
    label: 'Meeting Intelligence',
    description:
      'Full pipeline for advisor-client meetings: pre-meeting research, live transcription, PII redaction, real-time sentiment and recommendation analysis, and post-meeting summary — with two human-in-the-loop approval gates.',
    nodes: [
      // ── Entry ──────────────────────────────────────────────────────────────
      {
        id: 'start',
        type: 'service',
        label: 'Start Workflow',
        subtitle: 'meeting · session trigger',
        position: { x: 280, y: 20 },
        detail: {
          title: 'Start Workflow',
          subtitle: 'meeting · session trigger',
          description:
            'Entry point triggered when an advisor initiates a new client meeting session. Generates a session ID, persists the session record to Cosmos DB, and fires pre-meeting intelligence preparation.',
          sourceFiles: ['backend/app/routers/meetings.py'],
          responsibilities: [
            'Accept session start request from the React frontend',
            'Generate a unique session_id',
            'Create an initial session document in Cosmos DB (sessions container)',
            'Trigger the Pre-Meeting Prep pipeline asynchronously',
          ],
          dataFlow: [
            '1. POST /api/v1/meetings/start → advisor_id, client_id',
            '2. Session record written to Cosmos DB',
            '3. Pre-Meeting Prep pipeline invoked',
          ],
          technologies: ['FastAPI', 'Azure Cosmos DB', 'Pydantic v2'],
        },
      },

      // ── Pre-Meeting Prep ───────────────────────────────────────────────────
      {
        id: 'pre-meeting-prep',
        type: 'service',
        label: 'Pre-Meeting Prep',
        subtitle: 'service · advisory_workflow.py',
        position: { x: 240, y: 160 },
        detail: {
          title: 'Pre-Meeting Prep',
          subtitle: 'service · advisory_workflow.py',
          description:
            'Orchestrates parallel pre-meeting intelligence: news scan for client holdings, tax situation analysis, and relationship deepening ideas — aggregated into a single structured advisor briefing.',
          sourceFiles: ['backend/app/orchestration/advisory_workflow.py'],
          responsibilities: [
            'Run NewsAgent, TaxAgent, and AdvisoryAgent concurrently via asyncio.gather',
            'Merge results into a unified pre_meeting_briefing object',
            'Persist briefing to Cosmos DB for advisor retrieval before the meeting',
            'Log the advisory run to the audit trail',
          ],
          dataFlow: [
            '1. Receives client_id and session_id',
            '2. Dispatches NewsAgent and AdvisoryAgent in parallel',
            '3. Merges results into pre_meeting_briefing',
            '4. Saves briefing to Cosmos DB sessions container',
          ],
          technologies: ['asyncio', 'Azure AI Foundry', 'Azure Cosmos DB'],
        },
      },

      // ── Parallel: News Agent + Advisory Agent ──────────────────────────────
      {
        id: 'news-agent',
        type: 'agent',
        label: 'News Agent',
        subtitle: 'agent · news_agent.py',
        position: { x: 80, y: 300 },
        detail: {
          title: 'News Agent',
          subtitle: 'agent · news_agent.py',
          description:
            'Scans recent financial news relevant to the client\'s portfolio holdings using Bing Grounding. Identifies material events — earnings surprises, credit rating changes, regulatory actions — that may require advisor attention.',
          sourceFiles: ['backend/app/agents/news_agent.py'],
          responsibilities: [
            'Retrieve client holdings from Cosmos DB',
            'Query Bing for recent news on each holding',
            'Filter to material events (earnings, downgrades, regulatory)',
            'Return structured news summary with source citations',
          ],
          dataFlow: [
            '1. Receives client holdings list',
            '2. Calls Azure AI Foundry with Bing Grounding tool',
            '3. Returns news_summary with source URLs',
          ],
          technologies: ['Azure AI Foundry', 'Bing Grounding', 'o4-mini'],
          keyFacts: [
            'Uses Bing Grounding for real-time market data',
            'Source URLs preserved for compliance verification',
          ],
        },
      },
      {
        id: 'advisory-agent',
        type: 'agent',
        label: 'Advisory Agent',
        subtitle: 'agent · advisory_agent.py',
        position: { x: 460, y: 300 },
        detail: {
          title: 'Advisory Agent',
          subtitle: 'agent · advisory_agent.py',
          description:
            'Analyzes the client\'s portfolio composition, recent life events, and risk profile to generate relationship deepening ideas and potential discussion topics for the advisor.',
          sourceFiles: ['backend/app/agents/advisory_agent.py'],
          responsibilities: [
            'Read client profile and portfolio from Cosmos DB',
            'Analyze life events, goals, and risk tolerance',
            'Generate relationship deepening suggestions',
            'Identify potential suitability concerns',
          ],
          dataFlow: [
            '1. Receives client_id and portfolio data',
            '2. Analyzes via Azure AI Foundry (o4-mini)',
            '3. Returns advisory_insights with discussion topics',
          ],
          technologies: ['Azure AI Foundry', 'o4-mini', 'Azure Cosmos DB'],
        },
      },

      // ── Gate 0: Advisor Joins Call ─────────────────────────────────────────
      {
        id: 'meeting-in-progress',
        type: 'gate',
        label: 'Meeting In Progress',
        subtitle: 'gate · advisor joins call',
        position: { x: 180, y: 440 },
        detail: {
          title: 'Meeting In Progress',
          subtitle: 'gate · advisor joins call',
          description:
            'Human gate: the advisor reviews the pre-meeting briefing and joins the client call. No automated processing occurs until the advisor initiates the live session.',
          sourceFiles: ['frontend/src/components/SessionControls.tsx'],
          responsibilities: [
            'Display pre-meeting briefing to advisor',
            'Wait for advisor to click "Start Session"',
            'Activate WebSocket connections for real-time processing',
          ],
          dataFlow: [
            '1. Advisor reviews briefing in the UI',
            '2. Advisor clicks Start Session',
            '3. WebSocket connections established for Transcription, Sentiment, Recommendations',
          ],
          technologies: ['React', 'WebSocket', 'FastAPI'],
        },
      },

      // ── Transcription ──────────────────────────────────────────────────────
      {
        id: 'transcription-agent',
        type: 'agent',
        label: 'Transcription Agent',
        subtitle: 'agent · transcription_agent.py',
        position: { x: 280, y: 580 },
        detail: {
          title: 'Transcription Agent',
          subtitle: 'agent · transcription_agent.py',
          description:
            'Converts live meeting audio to structured transcript segments using Azure Speech Services. Identifies advisor and client speakers. Optimized with financial terminology phrase lists.',
          sourceFiles: ['backend/app/agents/transcription_agent.py'],
          responsibilities: [
            'Stream audio from browser via WebSocket',
            'Transcribe using Azure Speech with speaker diarization',
            'Return interim and final transcript segments with timestamps',
            'Apply financial terminology phrase list for accuracy',
          ],
          dataFlow: [
            '1. Receives PCM audio chunks via WebSocket /ws/transcribe/{session_id}',
            '2. Sends to Azure Speech Services (streaming)',
            '3. Returns transcript_chunk events: text, speaker, timestamp, is_final',
          ],
          technologies: ['Azure Speech Services', 'WebSocket', 'Speaker Diarization'],
        },
      },

      // ── PII Redaction ──────────────────────────────────────────────────────
      {
        id: 'pii-agent',
        type: 'agent',
        label: 'PII Redaction',
        subtitle: 'agent · pii_agent.py',
        position: { x: 280, y: 700 },
        detail: {
          title: 'PII Redaction',
          subtitle: 'agent · pii_agent.py',
          description:
            'Detects and redacts personally identifiable information from transcript segments before downstream processing. Ensures compliance with GDPR and financial data privacy regulations.',
          sourceFiles: ['backend/app/agents/pii_agent.py'],
          responsibilities: [
            'Detect PII entities: names, account numbers, SSN, DOB, addresses',
            'Redact or mask detected entities',
            'Pass sanitized transcript to parallel downstream agents',
            'Log PII detection events (without the PII values)',
          ],
          dataFlow: [
            '1. Receives raw transcript segment',
            '2. Runs Azure AI Language PII detection',
            '3. Returns redacted_transcript segment',
          ],
          technologies: ['Azure AI Language', 'PII Detection', 'GDPR Compliance'],
        },
      },

      // ── Parallel: Sentiment + Profile + Recommendation ────────────────────
      {
        id: 'sentiment-agent',
        type: 'agent',
        label: 'Sentiment Agent',
        subtitle: 'agent · sentiment_agent.py',
        position: { x: 60, y: 840 },
        detail: {
          title: 'Sentiment Agent',
          subtitle: 'agent · sentiment_agent.py',
          description:
            'Analyzes the emotional tone of the meeting conversation in real time. Tracks advisor and client sentiment across the meeting timeline to flag emotional inflection points.',
          sourceFiles: ['backend/app/agents/sentiment_agent.py'],
          responsibilities: [
            'Analyze sentiment per transcript segment',
            'Track overall and per-speaker sentiment trend',
            'Detect significant sentiment shifts',
            'Return structured sentiment scores via WebSocket',
          ],
          dataFlow: [
            '1. Receives redacted transcript segment',
            '2. Analyzes via Azure AI Language sentiment',
            '3. Emits sentiment_update event via WebSocket',
          ],
          technologies: ['Azure AI Language', 'Sentiment Analysis', 'WebSocket'],
        },
      },
      {
        id: 'profile-agent',
        type: 'agent',
        label: 'Profile Agent',
        subtitle: 'agent · profile_agent.py',
        position: { x: 280, y: 840 },
        detail: {
          title: 'Profile Agent',
          subtitle: 'agent · profile_agent.py',
          description:
            'Extracts structured client profile updates from the live meeting conversation: new goals, life events, risk preference changes, and asset mentions to enrich the Cosmos DB client record post-meeting.',
          sourceFiles: ['backend/app/agents/profile_agent.py'],
          responsibilities: [
            'Extract goals, life events, risk changes from conversation',
            'Build profile_extractions document with source quote attribution',
            'Update client record in Cosmos DB clients container post-meeting',
            'Delta-only updates — only changed fields written back',
          ],
          dataFlow: [
            '1. Receives PII-redacted transcript at end of meeting segment',
            '2. GPT-4.1 extracts structured delta: goals, life events, risk changes',
            '3. profile_extractions document built with source-quote attribution',
            '4. Client record updated in Cosmos DB clients container post-meeting',
          ],
          technologies: ['Azure AI Foundry', 'GPT-4.1', 'Azure Cosmos DB', 'Pydantic'],
          keyFacts: [
            'Delta-only updates — only changed fields written back to client document',
            'Source quote preserved for every extraction for compliance verification',
            'Extracts: goals, life_events, risk_preference, estate_notes, asset_mentions',
          ],
        },
      },
      {
        id: 'recommendation-agent',
        type: 'agent',
        label: 'Recommendation Agent',
        subtitle: 'agent · recommendation_agent.py',
        position: { x: 500, y: 840 },
        detail: {
          title: 'Recommendation Agent',
          subtitle: 'agent · recommendation_agent.py',
          description:
            'Generates real-time investment recommendations based on the meeting conversation, client portfolio, and market context. All recommendations include rationale and supporting data for suitability review.',
          sourceFiles: ['backend/app/agents/recommendation_agent.py'],
          responsibilities: [
            'Monitor conversation for actionable investment signals',
            'Cross-reference signals with client portfolio and risk profile',
            'Generate ranked recommendations with rationale and risk warnings',
            'Include source attribution for each recommendation',
          ],
          dataFlow: [
            '1. Receives redacted transcript + sentiment signals',
            '2. Queries portfolio context from Cosmos DB',
            '3. Generates recommendations via Azure AI Foundry (o4-mini)',
            '4. Emits recommendations_update event via WebSocket',
          ],
          technologies: ['Azure AI Foundry', 'o4-mini', 'Azure Cosmos DB'],
          keyFacts: [
            'Every recommendation includes rationale and risk warning',
            'Source attribution required for compliance',
            'Not executed — advisory only, requires Gate-1 approval',
          ],
        },
      },

      // ── Gate 1: Recommendation Review ──────────────────────────────────────
      {
        id: 'gate-1',
        type: 'gate',
        label: 'GATE-1 · Recommendation Review',
        subtitle: 'human in the loop',
        position: { x: 160, y: 980 },
        detail: {
          title: 'GATE-1 · Recommendation Review',
          subtitle: 'human in the loop',
          description:
            'Human-in-the-loop approval gate. The advisor reviews AI-generated recommendations before they are logged or shared with the client. Approved recommendations are persisted to the session record.',
          sourceFiles: ['frontend/src/components/RecommendationCards.tsx', 'backend/app/routers/meetings.py'],
          responsibilities: [
            'Present recommendations to advisor for review',
            'Allow advisor to approve, modify, or reject each recommendation',
            'Record approval decision with advisor_id and timestamp',
            'Persist approved recommendations to Cosmos DB session record',
          ],
          dataFlow: [
            '1. Advisor reviews recommendations in UI',
            '2. PATCH /api/v1/meetings/{session_id}/recommendations/approve',
            '3. gate_1_approved = true recorded in Cosmos DB',
            '4. Triggers Summary Agent pipeline',
          ],
          technologies: ['React', 'FastAPI', 'Azure Cosmos DB', 'Audit Logging'],
        },
      },
    ],
    edges: [
      { id: 'e-start-prep', source: 'start', target: 'pre-meeting-prep' },
      { id: 'e-prep-news', source: 'pre-meeting-prep', target: 'news-agent' },
      { id: 'e-prep-advisory', source: 'pre-meeting-prep', target: 'advisory-agent' },
      { id: 'e-news-gate0', source: 'news-agent', target: 'meeting-in-progress' },
      { id: 'e-advisory-gate0', source: 'advisory-agent', target: 'meeting-in-progress' },
      { id: 'e-gate0-transcription', source: 'meeting-in-progress', target: 'transcription-agent' },
      { id: 'e-transcription-pii', source: 'transcription-agent', target: 'pii-agent' },
      { id: 'e-pii-sentiment', source: 'pii-agent', target: 'sentiment-agent' },
      { id: 'e-pii-profile', source: 'pii-agent', target: 'profile-agent' },
      { id: 'e-pii-recommendation', source: 'pii-agent', target: 'recommendation-agent' },
      { id: 'e-sentiment-gate1', source: 'sentiment-agent', target: 'gate-1' },
      { id: 'e-profile-gate1', source: 'profile-agent', target: 'gate-1' },
      { id: 'e-recommendation-gate1', source: 'recommendation-agent', target: 'gate-1' },
    ],
  },

  // ---------------------------------------------------------------------------
  // Add additional workflow tabs here:
  // {
  //   id: 'portfolio-intelligence',
  //   label: 'Portfolio Intelligence',
  //   ...
  // }
  // ---------------------------------------------------------------------------
]
