import { X, FileCode, ChevronRight, Cpu } from 'lucide-react'
import type { WorkflowNode, NodeType } from './workflowTypes'

const TYPE_BADGE: Record<NodeType, { label: string; className: string }> = {
  service:   { label: 'SERVICE',          className: 'bg-blue-500/20 text-blue-300 border-blue-500/30' },
  agent:     { label: 'FOUNDRY AI AGENT', className: 'bg-indigo-500/20 text-indigo-300 border-indigo-500/30' },
  gate:      { label: 'HUMAN GATE',       className: 'bg-amber-500/20 text-amber-300 border-amber-500/30' },
  datastore: { label: 'DATA STORE',       className: 'bg-teal-500/20 text-teal-300 border-teal-500/30' },
  outcome:   { label: 'OUTCOME',          className: 'bg-green-500/20 text-green-300 border-green-500/30' },
}

interface WorkflowDetailPanelProps {
  node: WorkflowNode
  onClose: () => void
}

export default function WorkflowDetailPanel({ node, onClose }: WorkflowDetailPanelProps) {
  const badge = TYPE_BADGE[node.type]
  const d = node.detail

  return (
    <div className="w-[380px] flex-shrink-0 bg-slate-900 border-l border-slate-700 overflow-y-auto">
      <div className="p-5 space-y-5">

        {/* ── Header ── */}
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <span
              className={`inline-block text-xs font-semibold uppercase tracking-wider px-2 py-0.5 rounded border ${badge.className}`}
            >
              {badge.label}
            </span>
            <h2 className="text-white font-bold text-base mt-2 leading-tight">{d.title}</h2>
            <p className="text-slate-500 text-xs mt-0.5 font-mono">{d.subtitle}</p>
          </div>
          <button
            aria-label="Close detail panel"
            onClick={onClose}
            className="text-slate-500 hover:text-slate-300 transition-colors flex-shrink-0 mt-1"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* ── Description ── */}
        <p className="text-slate-300 text-sm leading-relaxed">{d.description}</p>

        {/* ── Source Files ── */}
        {d.sourceFiles.length > 0 && (
          <section>
            <SectionHeader icon={<FileCode className="w-3.5 h-3.5" />} label="SOURCE FILES" />
            <div className="space-y-1">
              {d.sourceFiles.map((f) => (
                <div key={f} className="text-indigo-400 text-xs font-mono bg-slate-800/60 rounded px-2.5 py-1.5">
                  • {f}
                </div>
              ))}
            </div>
          </section>
        )}

        {/* ── Responsibilities ── */}
        {d.responsibilities.length > 0 && (
          <section>
            <SectionHeader label="RESPONSIBILITIES" />
            <ul className="space-y-1.5">
              {d.responsibilities.map((r, i) => (
                <li key={i} className="text-slate-300 text-xs flex items-start gap-1.5">
                  <ChevronRight className="w-3 h-3 text-slate-500 flex-shrink-0 mt-0.5" />
                  {r}
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* ── Data Flow ── */}
        {d.dataFlow.length > 0 && (
          <section>
            <SectionHeader label="DATA FLOW" />
            <ol className="space-y-1.5">
              {d.dataFlow.map((step, i) => (
                <li key={i} className="text-slate-300 text-xs flex items-start gap-2">
                  <span className="text-slate-500 font-mono tabular-nums w-3 flex-shrink-0">{i + 1}.</span>
                  {step}
                </li>
              ))}
            </ol>
          </section>
        )}

        {/* ── Key Facts ── */}
        {d.keyFacts && d.keyFacts.length > 0 && (
          <section>
            <SectionHeader label="KEY FACTS" />
            <ul className="space-y-1.5">
              {d.keyFacts.map((f, i) => (
                <li key={i} className="text-slate-300 text-xs flex items-start gap-1.5">
                  <span className="text-slate-500 flex-shrink-0">•</span>
                  {f}
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* ── Technologies ── */}
        {d.technologies.length > 0 && (
          <section>
            <SectionHeader icon={<Cpu className="w-3.5 h-3.5" />} label="TECHNOLOGY" />
            <div className="flex flex-wrap gap-1.5">
              {d.technologies.map((t) => (
                <span key={t} className="text-xs bg-slate-700 text-slate-300 px-2 py-0.5 rounded border border-slate-600">
                  {t}
                </span>
              ))}
            </div>
          </section>
        )}
      </div>
    </div>
  )
}

function SectionHeader({ label, icon }: { label: string; icon?: React.ReactNode }) {
  return (
    <div className="flex items-center gap-1.5 text-slate-400 text-xs font-semibold uppercase tracking-wider mb-2">
      {icon}
      {label}
    </div>
  )
}
