import { useState, useMemo } from 'react'
import WorkflowNodeComponent from './WorkflowNode'
import WorkflowDetailPanel from './WorkflowDetailPanel'
import type { WorkflowNode, WorkflowEdge, WorkflowTab } from './workflowTypes'

const NODE_WIDTH = 180
const NODE_HEIGHT = 64
const GATE_WIDTH = 380

/** Returns center-bottom of a node as the edge source point */
function getSourcePoint(node: WorkflowNode): { x: number; y: number } {
  const width = node.type === 'gate' ? GATE_WIDTH : NODE_WIDTH
  return { x: node.position.x + width / 2, y: node.position.y + NODE_HEIGHT }
}

/** Returns center-top of a node as the edge target point */
function getTargetPoint(node: WorkflowNode): { x: number; y: number } {
  const width = node.type === 'gate' ? GATE_WIDTH : NODE_WIDTH
  return { x: node.position.x + width / 2, y: node.position.y }
}

/** Cubic bezier path between two points */
function cubicPath(sx: number, sy: number, tx: number, ty: number): string {
  const midY = (sy + ty) / 2
  return `M ${sx} ${sy} C ${sx} ${midY}, ${tx} ${midY}, ${tx} ${ty}`
}

interface WorkflowDiagramProps {
  tab: WorkflowTab
}

export default function WorkflowDiagram({ tab }: WorkflowDiagramProps) {
  const [selectedNode, setSelectedNode] = useState<WorkflowNode | null>(null)

  const nodeMap = useMemo(
    () => Object.fromEntries(tab.nodes.map((n) => [n.id, n])),
    [tab.nodes],
  )

  // Derive canvas size from node positions
  const canvasWidth = useMemo(
    () => Math.max(...tab.nodes.map((n) => n.position.x + (n.type === 'gate' ? GATE_WIDTH : NODE_WIDTH) + 60)),
    [tab.nodes],
  )
  const canvasHeight = useMemo(
    () => Math.max(...tab.nodes.map((n) => n.position.y + NODE_HEIGHT + 60)),
    [tab.nodes],
  )

  function handleNodeClick(node: WorkflowNode) {
    setSelectedNode((prev) => (prev?.id === node.id ? null : node))
  }

  return (
    <div className="flex flex-1 overflow-hidden">
      {/* ── Graph Canvas ── */}
      <div className="flex-1 overflow-auto bg-slate-900 relative p-6">
        <div
          className="relative"
          style={{ width: canvasWidth, height: canvasHeight, minWidth: '100%', minHeight: '100%' }}
        >
          {/* SVG Edges */}
          <svg
            className="absolute inset-0 pointer-events-none"
            width={canvasWidth}
            height={canvasHeight}
            aria-hidden="true"
          >
            <defs>
              <marker id="arrow-solid" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto">
                <polygon points="0 0, 8 3, 0 6" fill="#64748b" />
              </marker>
              <marker id="arrow-dashed" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto">
                <polygon points="0 0, 8 3, 0 6" fill="#475569" />
              </marker>
            </defs>

            {tab.edges.map((edge: WorkflowEdge) => {
              const source = nodeMap[edge.source]
              const target = nodeMap[edge.target]
              if (!source || !target) return null

              const sp = getSourcePoint(source)
              const tp = getTargetPoint(target)

              return (
                <path
                  key={edge.id}
                  d={cubicPath(sp.x, sp.y, tp.x, tp.y)}
                  fill="none"
                  stroke={edge.dashed ? '#475569' : '#64748b'}
                  strokeWidth={1.5}
                  strokeDasharray={edge.dashed ? '6 4' : undefined}
                  markerEnd={edge.dashed ? 'url(#arrow-dashed)' : 'url(#arrow-solid)'}
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
              onClick={handleNodeClick}
            />
          ))}
        </div>
      </div>

      {/* ── Detail Panel (slides in when node selected) ── */}
      {selectedNode && (
        <WorkflowDetailPanel
          node={selectedNode}
          onClose={() => setSelectedNode(null)}
        />
      )}
    </div>
  )
}
