// Example of a properly styled page using shadcn/ui primitives.
// If inputs look like plain browser boxes and the background is white with serif fonts,
// Tailwind + shadcn are NOT wired up — fix the config first.
//
// shadcn primitives used (add once):
//   npx shadcn@latest add card button input textarea label badge
import { useMutation } from '@tanstack/react-query'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { PageHeader } from '@/components/ui/PageHeader'
import { apiPost } from '@/lib/apiClient'

export default function ExampleFormPage() {
  // Mutations go through TanStack Query — never a raw fetch in an event handler.
  const submit = useMutation({
    mutationFn: (body: Record<string, unknown>) => apiPost('/loans/assess', body),
  })

  return (
    <div className="space-y-6">
      <PageHeader title="New Loan Application" subtitle="Submit borrower details to run the credit risk pipeline." />

      <Card className="max-w-2xl">
        <CardContent className="space-y-4 p-6">
          <div className="space-y-1.5">
            <Label htmlFor="company">Company name</Label>
            <Input id="company" placeholder="Acme Manufacturing Inc." />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label htmlFor="amount">Loan amount</Label>
              <Input id="amount" placeholder="$2,500,000" />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="purpose">Loan purpose</Label>
              <Input id="purpose" placeholder="Working capital" />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="financials">Financial statements</Label>
            <Textarea id="financials" className="min-h-[140px] resize-y" placeholder="Paste financials..." />
          </div>

          <div className="flex items-center gap-3 pt-2">
            <Button disabled={submit.isPending} onClick={() => submit.mutate({})}>
              {submit.isPending ? 'Submitting...' : 'Submit for assessment'}
            </Button>
            <Button variant="secondary">Cancel</Button>
            <Badge variant="outline" className="ml-auto border-info/40 text-info">
              Live Azure
            </Badge>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
