// frontend/src/pages/IssuersPage.tsx
// Lists the issuer cast from the REAL API via TanStack Query, rendered with TanStack Table.
// Honest states only (P1): explicit loading, error, and empty — never fabricated rows.
import { Link } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { PageHeader } from '@/components/ui/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useIssuers } from '@/hooks/useIssuers'
import { ApiError } from '@/lib/apiClient'
import { PROVIDER_LABELS } from '@/lib/settings'
import type { IssuerListItem } from '@/types/prism'

const columnHelper = createColumnHelper<IssuerListItem>()

const columns = [
  columnHelper.accessor('legalName', {
    header: 'Legal name',
    cell: (info) => <span className="font-medium text-foreground">{info.getValue()}</span>,
  }),
  columnHelper.accessor('ticker', {
    header: 'Ticker',
    cell: (info) => <span className="font-mono text-xs">{info.getValue()}</span>,
  }),
  columnHelper.accessor('sector', {
    header: 'Sector',
    cell: (info) => <span className="text-muted-foreground">{info.getValue()}</span>,
  }),
  columnHelper.accessor('sampleBondIsin', {
    header: 'Sample bond ISIN',
    cell: (info) => <span className="font-mono text-xs">{info.getValue()}</span>,
  }),
  columnHelper.display({
    id: 'coverage',
    header: 'Coverage',
    cell: ({ row }) => {
      const coverage = row.original.coverage ?? []
      if (coverage.length === 0) {
        return <span className="text-xs text-muted-foreground">—</span>
      }
      return (
        <div className="flex flex-wrap gap-1">
          {coverage.map((provider) => (
            <Badge key={provider} variant="secondary" className="text-[10px]">
              {PROVIDER_LABELS[provider]}
            </Badge>
          ))}
        </div>
      )
    },
  }),
  columnHelper.display({
    id: 'action',
    header: '',
    cell: ({ row }) => (
      <div className="flex justify-end">
        <Button asChild size="sm" variant="outline">
          <Link to={`/reconciliation?issuer=${encodeURIComponent(row.original.issuerId)}`}>
            Reconcile
          </Link>
        </Button>
      </div>
    ),
  }),
]

export default function IssuersPage() {
  const { data, isPending, isError, error } = useIssuers()

  const table = useReactTable({
    data: data ?? [],
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  return (
    <div className="max-w-7xl space-y-6">
      <PageHeader
        title="Issuers"
        subtitle="Select a corporate-bond issuer to reconcile its provider ratings."
      />

      {isPending ? (
        <Card>
          <CardContent className="flex items-center gap-3 p-8 text-muted-foreground">
            <Loader2 className="h-5 w-5 animate-spin text-primary" />
            <span className="text-sm">Loading issuers from the reconciliation API…</span>
          </CardContent>
        </Card>
      ) : isError ? (
        <Card className="border-destructive/40">
          <CardHeader>
            <CardTitle className="text-destructive">Could not load issuers</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <p className="text-sm text-muted-foreground">
              Couldn&rsquo;t load issuers. The list comes from the real{' '}
              <span className="font-mono">/api/v1/issuers</span> endpoint, which is unavailable
              right now.
            </p>
            <p className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 font-mono text-xs text-destructive">
              Error code: {error instanceof ApiError ? error.code : 'UNKNOWN'}
            </p>
          </CardContent>
        </Card>
      ) : data.length === 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>No issuers covered yet</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              No issuers are available from the reconciliation API for the current configuration.
            </p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                {table.getHeaderGroups().map((headerGroup) => (
                  <TableRow key={headerGroup.id}>
                    {headerGroup.headers.map((header) => (
                      <TableHead key={header.id}>
                        {header.isPlaceholder
                          ? null
                          : flexRender(header.column.columnDef.header, header.getContext())}
                      </TableHead>
                    ))}
                  </TableRow>
                ))}
              </TableHeader>
              <TableBody>
                {table.getRowModel().rows.map((row) => (
                  <TableRow key={row.id}>
                    {row.getVisibleCells().map((cell) => (
                      <TableCell key={cell.id}>
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
