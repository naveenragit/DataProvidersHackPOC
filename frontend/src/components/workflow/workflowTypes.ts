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
  /** true = dashed line (escalation / error path) */
  dashed?: boolean
}

export interface WorkflowTab {
  id: string
  label: string
  description: string
  nodes: WorkflowNode[]
  edges: WorkflowEdge[]
}
