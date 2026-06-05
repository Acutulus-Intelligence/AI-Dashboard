import type { ComponentPropsWithoutRef, ReactNode } from 'react';

type ButtonProps = (
  | ({ href: string } & ComponentPropsWithoutRef<'a'>)
  | ({ href?: never } & ComponentPropsWithoutRef<'button'>)
) & {
  children: ReactNode;
  variant?: 'primary' | 'dark' | 'outline' | 'surface' | 'ghost';
  className?: string;
};

const variants = {
  primary:
    'bg-cyan-action text-white shadow-action hover:bg-cyan-500 focus-visible:outline-cyan-action',
  dark: 'bg-primary text-on-primary hover:bg-on-background/90 focus-visible:outline-primary',
  outline:
    'border border-on-background text-on-background hover:bg-surface-container focus-visible:outline-secondary',
  surface:
    'bg-surface text-on-background hover:bg-surface-container-low focus-visible:outline-surface',
  ghost: 'text-primary hover:bg-surface-container-low focus-visible:outline-secondary',
};

export default function Button({ children, variant = 'primary', className = '', ...props }: ButtonProps) {
  const Component = 'href' in props && props.href ? 'a' : 'button';
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- discriminated union spread
  const componentProps = props as any;

  return (
    <Component
      className={`inline-flex min-h-11 items-center justify-center gap-2 rounded-xl px-6 py-3 text-body-md font-semibold transition-all active:scale-95 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 ${variants[variant]} ${className}`}
      {...componentProps}
    >
      {children}
    </Component>
  );
}
