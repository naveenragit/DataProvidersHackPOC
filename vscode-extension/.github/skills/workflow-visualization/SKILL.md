# Workflow Visualization Skill

Creates interactive system workflow diagrams for financial services applications —
dark-themed, clickable node graphs with detail panels, matching the architecture
shown in the reference screenshots.

## When to Use This Skill

Load this skill when:
- Implementing the `WorkflowPage.tsx` for a new application
- Adding new nodes/connections to an existing workflow diagram
- Understanding the component structure for workflow visualization
- Defining workflow data (`workflowData.ts`) for a new feature

---

## Architecture Overview

The workflow visualization consists of five files:

```
frontend/src/
├── data/
│   └── workflowData.ts           # All workflow definitions (nodes, edges, tabs)
├── types/
│   └── workflowTypes.ts          # TypeScript types for workflow data
├── components/workflow/
│   ├── WorkflowDiagram.tsx       # Main graph rendering component
│   ├── WorkflowNode.tsx          # Individual node box
│   └── WorkflowDetailPanel.tsx   # Right-side detail panel (opens on node click)
└── pages/
    └── WorkflowPage.tsx          # Full page with tab navigation
```

---

## TypeScript Types (`workflowTypes.ts`)

```typescript
export type NodeType = 'service' | 'agent' | 'gate' | 'datastore' | 'outcome'

export interface WorkflowNodeDetail {
  title: string
  subtitle: string
  description: string
  sourceFiles: string[]
  responsibilities: string[]
  dataFlow: string[]
  technologies: string[]
  keyFacts?: string[]
}

export interface WorkflowNode {
  id: string
  type: NodeType
  label: string
  subtitle: string
  position: { x: number; y: number }
  detail: WorkflowNodeDetail
}

export interface WorkflowEdge {
  id: string
  source: string
  target: string
  label?: string
  dashed?: boolean  // true for escalation/error paths
}

export interface WorkflowTab {
  id: string
  label: string
  description: string
  nodes: WorkflowNode[]
  edges: WorkflowEdge[]
}
```

---

## Node Type Color Map

| `NodeType` | Background | Border | Label Color | Use For |
|---|---|---|---|---|
| `service` | `bg-blue-900/50` | `border-blue-600` | `text-blue-200` | FastAPI services, orchestration layers |
| `agent` | `bg-purple-900/50` | `border-purple-600` | `text-purple-200` | Azure AI Foundry agents |
| `gate` | `bg-amber-900/50` | `border-amber-600` | `text-amber-200` | Human-in-the-loop approval checkpoints |
| `datastore` | `bg-teal-900/50` | `border-teal-600` | `text-teal-200` | Cosmos DB, Azure AI Search |
| `outcome` | `bg-green-900/50` | `border-green-600` | `text-green-200` | Final outputs, completed results |

Start trigger node: `bg-slate-700/70 border-slate-500` (neutral entry point).

---

## Component Implementation

### `WorkflowNode.tsx`
```tsx
import type { WorkflowNode, NodeType } from '@/types/workflowTypes'

const NODE_STYLES: Record<NodeType, string> = {
  service: 'bg-blue-900/50 border-blue-600 text-blue-200',
  agent: 'bg-indigo-900/50 border-indigo-500 text-indigo-200',
  gate: 'bg-amber-900/50 border-amber-600 text-amber-200',
  datastore: 'bg-teal-900/50 border-teal-600 text-teal-200',
  outcome: 'bg-green-900/50 border-green-600 text-green-200',
}

const TYPE_BADGE_STYLES: Record<NodeType, string> = {
  service: 'bg-blue-500/20 text-blue-400',
  agent: 'bg-indigo-500/20 text-indigo-400',
  gate: 'bg-amber-500/20 text-amber-400',
  datastore: 'bg-teal-500/20 text-teal-400',
  outcome: 'bg-green-500/20 text-green-400',
}

interface WorkflowNodeProps {
  node: WorkflowNode
  isSelected: boolean
  onClick: (node: WorkflowNode) => void
}

export default function WorkflowNodeComponent({ node, isSelected, onClick }: WorkflowNodeProps) {
  return (
    <div
      className={`
        absolute cursor-pointer rounded-lg border-2 px-4 py-3 min-w-[160px] max-w-[200px]
        transition-all duration-200 select-none
        ${NODE_STYLES[node.type]}
        ${isSelected ? 'ring-2 ring-white/40 scale-105' : 'hover:scale-102 hover:brightness-110'}
      `}
      style={{ left: node.position.x, top: node.position.y }}
      onClick={() => onClick(node)}
    >
      <div className="text-sm font-semibold leading-tight">{node.label}</div>
      <div className="text-xs mt-1 opacity-60 truncate">{node.subtitle}</div>
    </div>
  )
}
```

### `WorkflowDetailPanel.tsx`
```tsx
import { X, FileCode, ChevronRight } from 'lucide-react'
import type { WorkflowNode, NodeType } from '@/types/workflowTypes'

const TYPE_BADGE: Record<NodeType, { label: string; className: string }> = {
  service:   { label: 'SERVICE',   className: 'bg-blue-500/20 text-blue-400' },
  agent:     { label: 'FOUNDRY AI AGENT', className: 'bg-indigo-500/20 text-indigo-400' },
  gate:      { label: 'HUMAN GATE', className: 'bg-amber-500/20 text-amber-400' },
  datastore: { label: 'DATA STORE', className: 'bg-teal-500/20 text-teal-400' },
  outcome:   { label: 'OUTCOME',   className: 'bg-green-500/20 text-green-400' },
}

export default function WorkflowDetailPanel({
  node,
  onClose,
}: {
  node: WorkflowNode
  onClose: () => void
}) {
  const badge = TYPE_BADGE[node.type]
  const d = node.detail

  return (
    <div className="w-96 bg-slate-900 border-l border-slate-700 overflow-y-auto flex-shrink-0">
      <div className="p-5 space-y-5">
        {/* Header */}
        <div className="flex items-start justify-between">
          <div>
            <span className={`text-xs font-semibold uppercase tracking-wider px-2 py-0.5 rounded ${badge.className}`}>
              {badge.label}
            </span>
            <h2 className="text-white font-bold text-lg mt-2">{d.title}</h2>
            <p className="text-slate-400 text-xs mt-0.5">{d.subtitle}</p>
          </div>
          <button onClick={onClose} className="text-slate-500 hover:text-slate-300 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Description */}
        <p className="text-slate-300 text-sm leading-relaxed">{d.description}</p>

        {/* Source Files */}
        {d.sourceFiles.length > 0 && (
          <div>
            <div className="flex items-center gap-1.5 text-slate-400 text-xs font-semibold uppercase tracking-wider mb-2">
              <FileCode className="w-3.5 h-3.5" />
              SOURCE FILES
            </div>
            {d.sourceFiles.map((f) => (
              <div key={f} className="text-indigo-400 text-xs font-mono bg-slate-800 rounded px-2 py-1 mb-1">
                • {f}
              </div>
            ))}
          </div>
        )}

        {/* Responsibilities */}
        {d.responsibilities.length > 0 && (
          <div>
            <div className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-2">RESPONSIBILITIES</div>
            {d.responsibilities.map((r, i) => (
              <div key={i} className="text-slate-300 text-xs flex items-start gap-1.5 mb-1.5">
                <ChevronRight className="w-3 h-3 text-slate-500 flex-shrink-0 mt-0.5" />
                {r}
              </div>
            ))}
          </div>
        )}

        {/* Data Flow */}
        {d.dataFlow.length > 0 && (
          <div>
            <div className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-2">DATA FLOW</div>
            <ol className="space-y-1">
              {d.dataFlow.map((step, i) => (
                <li key={i} className="text-slate-300 text-xs flex items-start gap-2">
                  <span className="text-slate-500 font-mono">{i + 1}.</span>
                  {step}
                </li>
              ))}
            </ol>
          </div>
        )}

        {/* Key Facts */}
        {d.keyFacts && d.keyFacts.length > 0 && (
          <div>
            <div className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-2">KEY FACTS</div>
            {d.keyFacts.map((f, i) => (
              <div key={i} className="text-slate-300 text-xs flex items-start gap-1.5 mb-1.5">
                <span className="text-slate-500">•</span>
                {f}
              </div>
            ))}
          </div>
        )}

        {/* Technologies */}
        {d.technologies.length > 0 && (
          <div>
            <div className="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-2">TECHNOLOGY</div>
            <div className="flex flex-wrap gap-1.5">
              {d.technologies.map((t) => (
                <span key={t} className="text-xs bg-slate-700 text-slate-300 px-2 py-0.5 rounded">
                  {t}
                </span>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
```

### `WorkflowDiagram.tsx` (SVG edge rendering)
```tsx
import { useState, useRef } from 'react'
import WorkflowNodeComponent from './WorkflowNode'
import WorkflowDetailPanel from './WorkflowDetailPanel'
import type { WorkflowNode, WorkflowEdge, WorkflowTab } from '@/types/workflowTypes'

const NODE_WIDTH = 180
const NODE_HEIGHT = 64

function getEdgePath(
  source: WorkflowNode,
  target: WorkflowNode,
): string {
  const sx = source.position.x + NODE_WIDTH / 2
  const sy = source.position.y + NODE_HEIGHT
  const tx = target.position.x + NODE_WIDTH / 2
  const ty = target.position.y
  const midY = (sy + ty) / 2
  return `M ${sx} ${sy} C ${sx} ${midY}, ${tx} ${midY}, ${tx} ${ty}`
}

export default function WorkflowDiagram({ tab }: { tab: WorkflowTab }) {
  const [selectedNode, setSelectedNode] = useState<WorkflowNode | null>(null)
  const nodeMap = Object.fromEntries(tab.nodes.map((n) => [n.id, n]))

  // Calculate canvas dimensions from node positions
  const maxX = Math.max(...tab.nodes.map((n) => n.position.x + NODE_WIDTH + 40))
  const maxY = Math.max(...tab.nodes.map((n) => n.position.y + NODE_HEIGHT + 40))

  return (
    <div className="flex flex-1 overflow-hidden">
      <div className="flex-1 overflow-auto relative bg-slate-900">
        {/* Canvas */}
        <div className="relative" style={{ width: maxX, height: maxY, minWidth: '100%', minHeight: '100%' }}>
          {/* SVG Edges */}
          <svg className="absolute inset-0 pointer-events-none" width={maxX} height={maxY}>
            <defs>
              <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
                <polygon points="0 0, 10 3.5, 0 7" fill="#64748b" />
              </marker>
              <marker id="arrowhead-dashed" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
                <polygon points="0 0, 10 3.5, 0 7" fill="#475569" />
              </marker>
            </defs>
            {tab.edges.map((edge) => {
              const source = nodeMap[edge.source]
              const target = nodeMap[edge.target]
              if (!source || !target) return null
              return (
                <path
                  key={edge.id}
                  d={getEdgePath(source, target)}
                  fill="none"
                  stroke={edge.dashed ? '#475569' : '#64748b'}
                  strokeWidth={1.5}
                  strokeDasharray={edge.dashed ? '6,4' : undefined}
                  markerEnd={edge.dashed ? 'url(#arrowhead-dashed)' : 'url(#arrowhead)'}
                />
              )
            })}
          </svg>

          {/* Nodes */}
          {tab.nodes.map((node) => (
            <WorkflowNodeComponent
              key={node.id}
              node={node}
              isSelected={selectedNode?.id === node.id}
              onClick={setSelectedNode}
            />
          ))}
        </div>
      </div>

      {/* Detail Panel */}
      {selectedNode && (
        <WorkflowDetailPanel
          node={selectedNode}
          onClose={() => setSelectedNode(null)}
        />
      )}
    </div>
  )
}
```

### `WorkflowPage.tsx` (Full page)
```tsx
import { useState } from 'react'
import { GitBranch } from 'lucide-react'
import WorkflowDiagram from '@/components/workflow/WorkflowDiagram'
import { workflowTabs } from '@/data/workflowData'
import type { NodeType } from '@/types/workflowTypes'

const LEGEND: { type: NodeType; label: string; color: string }[] = [
  { type: 'service',   label: 'Service / API',     color: 'bg-blue-500' },
  { type: 'agent',     label: 'Foundry AI Agent',   color: 'bg-indigo-500' },
  { type: 'gate',      label: 'Human Gate',         color: 'bg-amber-500' },
  { type: 'datastore', label: 'Data Store',         color: 'bg-teal-500' },
  { type: 'outcome',   label: 'Outcome',            color: 'bg-green-500' },
]

export default function WorkflowPage() {
  const [activeTabId, setActiveTabId] = useState(workflowTabs[0].id)
  const activeTab = workflowTabs.find((t) => t.id === activeTabId) ?? workflowTabs[0]

  return (
    <div className="flex flex-col h-full -m-6">
      {/* Page Header */}
      <div className="px-6 py-4 bg-slate-800 border-b border-slate-700 flex-shrink-0">
        <div className="flex items-center gap-2 mb-1">
          <GitBranch className="w-5 h-5 text-indigo-400" />
          <h1 className="text-lg font-bold text-white">System Workflow</h1>
        </div>
        <p className="text-slate-400 text-xs">{activeTab.description}</p>

        {/* Legend */}
        <div className="flex items-center gap-4 mt-3">
          {LEGEND.map((item) => (
            <div key={item.type} className="flex items-center gap-1.5">
              <div className={`w-2.5 h-2.5 rounded-sm ${item.color}`} />
              <span className="text-slate-400 text-xs">{item.label}</span>
            </div>
          ))}
          <div className="flex items-center gap-1.5 ml-2">
            <div className="w-6 border-t border-dashed border-slate-500" />
            <span className="text-slate-400 text-xs">Escalation path</span>
          </div>
        </div>
      </div>

      {/* Tab Navigation */}
      <div className="px-6 bg-slate-800 border-b border-slate-700 flex-shrink-0">
        <div className="flex gap-1">
          {workflowTabs.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTabId(tab.id)}
              className={`px-4 py-2.5 text-sm font-medium rounded-t-lg transition-colors ${
                tab.id === activeTabId
                  ? 'bg-indigo-600/20 text-indigo-300 border-b-2 border-indigo-500'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-700/50'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </div>

      {/* Diagram */}
      <div className="flex-1 overflow-hidden flex">
        <WorkflowDiagram tab={activeTab} />
      </div>
    </div>
  )
}
```

---

## Workflow Data Definition Template (`workflowData.ts`)

```typescript
import type { WorkflowTab } from '@/types/workflowTypes'

export const workflowTabs: WorkflowTab[] = [
  {
    id: 'meeting-intelligence',
    label: 'Meeting Intelligence',
    description: 'Full pipeline for advisor-client meetings: pre-meeting research, live transcription, PII redaction, real-time sentiment and recommendation analysis, and post-meeting summary — with two human-in-the-loop approval gates.',
    nodes: [
      {
        id: 'start',
        type: 'service',
        label: 'Start Workflow',
        subtitle: 'meeting · session trigger',
        position: { x: 280, y: 20 },
        detail: {
          title: 'Start Workflow',
          subtitle: 'meeting · session trigger',
          description: 'Entry point triggered when an advisor initiates a new client meeting session via the frontend.',
          sourceFiles: ['backend/app/routers/meetings.py'],
          responsibilities: ['Accept session start request', 'Generate session ID', 'Initialize Cosmos DB session record'],
          dataFlow: ['1. POST /api/v1/meetings/start', '2. Session created in Cosmos DB', '3. Triggers Pre-Meeting Prep pipeline'],
          technologies: ['FastAPI', 'Azure Cosmos DB'],
        },
      },
      // Add more nodes following this pattern...
    ],
    edges: [
      { id: 'e-start-prep', source: 'start', target: 'pre-meeting-prep' },
      // Add more edges...
    ],
  },
]
```

---

## Adding a New Workflow Tab

When a new major feature is added (e.g., "Loan Origination", "Portfolio Intelligence"):

1. Define the new tab object in `workflowData.ts`
2. Map out ALL components: every agent, service, gate, and data store
3. Position nodes top-to-bottom for linear flows, side-by-side for parallel flows
4. Horizontal spacing: 200px between parallel nodes; Vertical spacing: 120px between sequential nodes
5. Add the tab `id` to the `workflowTabs` array — it automatically appears in navigation

## Node Positioning Guidelines

- **Single flow column**: center at x=280, start at y=20, increment y by 120
- **Parallel branches**: space at x=100 and x=460 (280±180), same y level
- **Three parallel branches**: x=60, x=280, x=500
- **After parallel merge**: back to center x=280
- **Gate nodes**: always full-width (`min-w-full`), center at x=180, width=380
