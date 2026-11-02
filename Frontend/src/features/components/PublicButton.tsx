import type { ButtonHTMLAttributes } from 'react';
import { publicButtonClass, type PublicButtonVariant } from '../layouts/publicChrome';

type PublicButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: PublicButtonVariant;
};

export default function PublicButton({
  variant = 'primary',
  className,
  type = 'button',
  ...props
}: PublicButtonProps) {
  return <button type={type} className={publicButtonClass(variant, className)} {...props} />;
}
