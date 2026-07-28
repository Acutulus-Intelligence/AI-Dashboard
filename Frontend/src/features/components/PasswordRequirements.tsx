import { Check } from 'lucide-react';
import { PASSWORD_RULES } from '../validation/registerForm';

interface PasswordRequirementsProps {
  password: string;
}

export default function PasswordRequirements({ password }: PasswordRequirementsProps) {
  return (
    <ul className="mt-2 space-y-1" aria-label="Password requirements">
      {PASSWORD_RULES.map((rule) => {
        const met = password.length > 0 && rule.test(password);
        return (
          <li
            key={rule.id}
            className={`flex items-center gap-2 text-sm ${
              met ? 'text-brand' : 'text-muted-foreground'
            }`}
          >
            <Check size={14} className={met ? 'opacity-100' : 'opacity-30'} />
            <span>{rule.label}</span>
          </li>
        );
      })}
    </ul>
  );
}
