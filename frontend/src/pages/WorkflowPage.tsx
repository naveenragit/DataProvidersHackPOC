import { useState } from 'react'
import { GitBranch } from 'lucide-react'
import WorkflowDiagram from '@/components/workflow/WorkflowDiagram'
import { workflowTabs } from '@/components/workflow/workflowData'
import type { NodeType } from '@/components/workflow/workflowTypes'

const LEGEND: { type: NodeType; label: string; dotClass: string }[] = [
  { type: 'service',   label: 'Service / API',     dotClass: 'bg-blue-500' },
  { type: 'agent',     label: 'Foundry AI Agent',  dotClass: 'bg-indigo-500' },
  { type: 'gate',      label: 'Human Gate',        dotClass: 'bg-amber-500' },
  { type: 'datastore', label: 'Data Store',        dotClass: 'bg-teal-500' },
  { type: 'outcome',   label: 'Outcome',           dotClass: 'bg-green-500' },
]

/**
 * WorkflowPage
 *
 * Interactive system workflow visualization for the Prism rating-reconciliation pipeline.
 * Data + components are co-located under src/components/workflow/.
 */
export default function WorkflowPage() {
  const [activeTabId, setActiveTabId] = useState<string>(workflowTabs[0].id)

  const activeTab = workflowTabs.find((t) => t.id === activeTabId) ?? workflowTabs[0]

  return (
    // Stretch to fill the main content area; negate parent padding with -m-6
    <div className="flex flex-col h-full -m-6 overflow-hidden">

      {/* ── Page Header ── */}
      <div className="flex-shrink-0 px-6 py-4 bg-slate-800/80 border-b border-slate-700">
        <div className="flex items-center justify-between">
          <div>
            <div className="flex items-center gap-2">
              <GitBranch className="w-5 h-5 text-indigo-400" />
              <h1 className="text-base font-bold text-white">System Workflow</h1>
            </div>
            <p className="text-slate-500 text-xs mt-0.5">
              End-to-end agent orchestration — click any component to see rich details
            </p>
          </div>

          {/* Legend */}
          <div className="hidden md:flex items-center gap-4 flex-wrap">
            {LEGEND.map((item) => (
              <div key={item.type} className="flex items-center gap-1.5">
                <div className={`w-2.5 h-2.5 rounded-sm flex-shrink-0 ${item.dotClass}`} />
                <span className="text-slate-400 text-xs">{item.label}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Active tab description */}
        <p className="text-slate-400 text-xs mt-2 max-w-3xl">{activeTab.description}</p>
      </div>

      {/* ── Tab Navigation ── */}
      <div className="flex-shrink-0 px-6 bg-slate-800/60 border-b border-slate-700">
        <nav className="flex gap-0.5 -mb-px" aria-label="Workflow tabs">
          {workflowTabs.map((tab) => {
            const isActive = tab.id === activeTabId
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTabId(tab.id)}
                className={[
                  'px-4 py-2.5 text-sm font-medium rounded-t-md border-b-2 transition-colors whitespace-nowrap',
                  isActive
                    ? 'border-indigo-500 text-indigo-300 bg-indigo-500/10'
                    : 'border-transparent text-slate-400 hover:text-slate-200 hover:bg-slate-700/40',
                ].join(' ')}
                aria-current={isActive ? 'page' : undefined}
              >
                {tab.label}
              </button>
            )
          })}
        </nav>
      </div>

      {/* ── Diagram ── */}
      <div className="flex-1 overflow-hidden flex">
        <WorkflowDiagram tab={activeTab} />
      </div>
    </div>
  )
}
