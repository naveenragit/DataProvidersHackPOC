// Copy to frontend/src/lib/utils.ts — the shadcn/ui class-name merge helper.
// `npx shadcn@latest init` generates this; included here for completeness.
import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
