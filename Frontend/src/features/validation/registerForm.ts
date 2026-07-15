export type RegisterField =
  | 'firstName'
  | 'lastName'
  | 'email'
  | 'password'
  | 'confirmPassword';

export type RegisterFieldErrors = Partial<Record<RegisterField, string>>;

export interface RegisterValidationResult {
  fieldErrors: RegisterFieldErrors;
  formError?: string;
  isValid: boolean;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export const PASSWORD_RULES = [
  { id: 'length', label: 'At least 8 characters', test: (p: string) => p.length >= 8 },
  { id: 'upper', label: 'One uppercase letter', test: (p: string) => /[A-Z]/.test(p) },
  { id: 'lower', label: 'One lowercase letter', test: (p: string) => /[a-z]/.test(p) },
  { id: 'digit', label: 'One number', test: (p: string) => /[0-9]/.test(p) },
  { id: 'special', label: 'One special character', test: (p: string) => /[^a-zA-Z0-9]/.test(p) },
] as const;

export function validateRegisterForm(values: {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}): RegisterValidationResult {
  const fieldErrors: RegisterFieldErrors = {};

  if (!values.firstName.trim()) {
    fieldErrors.firstName = 'First name is required.';
  } else if (values.firstName.trim().length > 100) {
    fieldErrors.firstName = 'First name must be 100 characters or fewer.';
  }

  if (!values.lastName.trim()) {
    fieldErrors.lastName = 'Last name is required.';
  } else if (values.lastName.trim().length > 100) {
    fieldErrors.lastName = 'Last name must be 100 characters or fewer.';
  }

  if (!values.email.trim()) {
    fieldErrors.email = 'Email is required.';
  } else if (!EMAIL_PATTERN.test(values.email.trim())) {
    fieldErrors.email = 'Enter a valid email address.';
  }

  if (!values.password) {
    fieldErrors.password = 'Password is required.';
  } else {
    for (const rule of PASSWORD_RULES) {
      if (!rule.test(values.password)) {
        fieldErrors.password = `Password must meet all requirements below.`;
        break;
      }
    }
  }

  if (!values.confirmPassword) {
    fieldErrors.confirmPassword = 'Please confirm your password.';
  } else if (values.password !== values.confirmPassword) {
    fieldErrors.confirmPassword = 'Passwords do not match.';
  }

  return {
    fieldErrors,
    isValid: Object.keys(fieldErrors).length === 0,
  };
}

const API_FIELD_MAP: Record<string, RegisterField> = {
  FirstName: 'firstName',
  LastName: 'lastName',
  Email: 'email',
  Password: 'password',
};

export function mapApiFieldErrors(errors: Record<string, string[]>): RegisterFieldErrors {
  const fieldErrors: RegisterFieldErrors = {};
  for (const [key, messages] of Object.entries(errors)) {
    const field = API_FIELD_MAP[key];
    if (field && messages[0]) {
      fieldErrors[field] = messages[0];
    }
  }
  return fieldErrors;
}

export function mapRegisterApiError(error: { status: number; message: string; code?: string; fieldErrors?: Record<string, string[]> }): RegisterValidationResult {
  if (error.fieldErrors && Object.keys(error.fieldErrors).length > 0) {
    const fieldErrors = mapApiFieldErrors(error.fieldErrors);
    return { fieldErrors, isValid: false };
  }

  if (error.status === 409 || error.code === 'email_conflict' || error.message.toLowerCase().includes('email')) {
    return {
      fieldErrors: { email: 'An account with this email already exists.' },
      isValid: false,
    };
  }

  return {
    fieldErrors: {},
    formError: error.message || 'Registration failed. Please try again.',
    isValid: false,
  };
}
