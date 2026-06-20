import type { WorkflowNode, NodeType } from './workflowTypes'

const NODE_WIDTH = 180
const NODE_HEIGHT = 64

/** Color classes per node type */
export const NODE_STYLES: Record<NodeType, string> = {
  service:   'bg-blue-900/50 border-blue-600 text-blue-200',
  agent:     'bg-indigo-900/50 border-indigo-500 text-indigo-200',
  gate:      'bg-amber-900/50 border-amber-600 text-amber-200',
  datastore: 'bg-teal-900/50 border-teal-600 text-teal-200',
  outcome:   'bg-green-900/50 border-green-600 text-green-200',
}

interface WorkflowNodeProps {
  node: WorkflowNode
  isSelected: boolean
  onClick: (node: WorkflowNode) => void
}

export default function WorkflowNodeComponent({ node, isSelected, onClick }: WorkflowNodeProps) {
  const isGate = node.type === 'gate'

  return (
    <div
      role="button"
      tabIndex={0}
      aria-label={`${node.label} — click to view details`}
      className={[
        'absolute cursor-pointer rounded-lg border-2 px-4 py-3 transition-all duration-150 select-none',
        NODE_STYLES[node.type],
        isGate ? 'text-center' : '',
        isSelected
          ? 'ring-2 ring-white/30 shadow-lg shadow-white/5 scale-105'
          : 'hover:brightness-110 hover:shadow-md',
      ].join(' ')}
      style={{
        left: node.position.x,
        top: node.position.y,
        width: isGate ? 380 : NODE_WIDTH,
        minHeight: NODE_HEIGHT,
      }}
      onClick={() => onClick(node)}
      onKeyDown={(e) => e.key === 'Enter' && onClick(node)}
    >
      <div className="text-sm font-semibold leading-tight">{node.label}</div>
      <div className="text-xs mt-0.5 opacity-60 truncate">{node.subtitle}</div>
    </div>
  )
}
