import { Link } from 'react-router-dom';
import { XCircle } from 'lucide-react';
import Button from '../components/Button';
import { ROUTES } from '../routes';

export default function PaymentCancelPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-background px-4 text-on-background">
      <div className="w-full max-w-md rounded-xl border border-outline-variant bg-surface-container-lowest p-8 text-center">
        <XCircle className="mx-auto mb-5 size-12 text-error" aria-hidden="true" />
        <h1 className="text-headline-lg font-semibold">Payment was canceled</h1>
        <p className="mt-3 text-body-md text-on-surface-variant">
          No charge was completed. You can choose another plan whenever you are ready.
        </p>
        <div className="mt-6 flex justify-center">
          <Link to={ROUTES.PRICING}>
            <Button>View Plans</Button>
          </Link>
        </div>
      </div>
    </main>
  );
}
