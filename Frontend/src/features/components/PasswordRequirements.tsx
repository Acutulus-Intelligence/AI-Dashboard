import { Check } from 'lucide-react';
import { PASSWORD_RULES } from '../validation/registerForm';

interface PasswordRequirementsProps {
  password: string;
  /** Landing-page colors for public auth forms. Profile keeps the default. */
  tone?: 'default' | 'public';
}

export default function PasswordRequirements({ password, tone = 'default' }: PasswordRequirementsProps) {
  const metClass = tone === 'public' ? 'text-blue-700' : 'text-brand';
  const unmetClass = tone === 'public' ? 'text-slate-500' : 'text-muted-foreground';

  return (
    <ul className="mt-2 space-y-1" aria-label="Password requirements">
      {PASSWORD_RULES.map((rule) => {
        const met = password.length > 0 && rule.test(password);
        return (
          <li
            key={rule.id}
            className={`flex items-center gap-2 text-sm ${met ? metClass : unmetClass}`}
          >
            <Check size={14} className={met ? 'opacity-100' : 'opacity-30'} />
            <span>{rule.label}</span>
          </li>
        );
      })}
    </ul>
  );
}
