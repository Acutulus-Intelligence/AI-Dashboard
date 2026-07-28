import { z } from 'zod';
import { PASSWORD_RULES } from './registerForm';

export const accountSchema = z.object({
  firstName: z
    .string()
    .trim()
    .min(1, 'First name is required.')
    .max(100, 'First name must be 100 characters or fewer.'),
  lastName: z
    .string()
    .trim()
    .min(1, 'Last name is required.')
    .max(100, 'Last name must be 100 characters or fewer.'),
  email: z.email('Enter a valid email address.').trim().min(1, 'Email is required.'),
});

export type AccountFormValues = z.infer<typeof accountSchema>;

/** Applies the same strength rules the registration form enforces. */
const strongPassword = PASSWORD_RULES.reduce(
  (schema, rule) => schema.refine(rule.test, { message: rule.label }),
  z.string().min(1, 'New password is required.'),
);

export const passwordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Current password is required.'),
    newPassword: strongPassword,
    confirmNewPassword: z.string().min(1, 'Please confirm your new password.'),
  })
  .refine((v) => v.newPassword === v.confirmNewPassword, {
    message: 'Passwords do not match.',
    path: ['confirmNewPassword'],
  })
  .refine((v) => v.newPassword !== v.currentPassword, {
    message: 'New password must differ from the current one.',
    path: ['newPassword'],
  });

export type PasswordFormValues = z.infer<typeof passwordSchema>;

export const deleteAccountSchema = z.object({
  currentPassword: z.string().min(1, 'Enter your password to confirm.'),
});

export type DeleteAccountFormValues = z.infer<typeof deleteAccountSchema>;
