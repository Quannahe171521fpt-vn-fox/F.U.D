import type { ReactNode } from 'react';

type BadgeTone = 'slate' | 'green' | 'amber' | 'rose' | 'violet' | 'sky' | 'orange' | 'teal';

interface BadgeProps {
  children: ReactNode;
  tone?: BadgeTone;
  className?: string;
}

const TONE_CLASS: Record<BadgeTone, string> = {
  slate: 'bg-slate-100 text-slate-700 ring-slate-600/20',
  green: 'bg-emerald-100 text-emerald-700 ring-emerald-600/20',
  amber: 'bg-amber-100 text-amber-700 ring-amber-600/20',
  rose: 'bg-rose-100 text-rose-700 ring-rose-600/20',
  violet: 'bg-violet-100 text-violet-700 ring-violet-600/20',
  sky: 'bg-sky-100 text-sky-700 ring-sky-600/20',
  orange: 'bg-orange-100 text-orange-700 ring-orange-600/20',
  teal: 'bg-teal-100 text-teal-700 ring-teal-600/20',
};

export function Badge({ children, tone = 'slate', className = '' }: BadgeProps) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset ${TONE_CLASS[tone]} ${className}`}
    >
      {children}
    </span>
  );
}

export default Badge;
