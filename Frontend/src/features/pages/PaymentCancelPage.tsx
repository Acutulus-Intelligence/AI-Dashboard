import { Link } from 'react-router-dom';
import { XCircle } from 'lucide-react';
import PageMeta from '../components/PageMeta';
import PublicLayout from '../layouts/PublicLayout';
import { publicButtonClass, publicCardClass, publicPageClass } from '../layouts/publicChrome';
import { ROUTES } from '../routes';

export default function PaymentCancelPage() {
  return (
    <PublicLayout>
      <PageMeta
        title="Payment canceled — AI Dashboard"
        description="No charge was completed. You can choose another AI Dashboard plan whenever you are ready."
        path={ROUTES.PAYMENT_CANCEL}
      />
      <div className={`flex flex-1 items-center justify-center px-5 py-16 sm:px-6 ${publicPageClass}`}>
        <div className={`w-full max-w-md p-8 text-center sm:p-10 ${publicCardClass}`}>
          <XCircle className="mx-auto mb-5 size-12 text-red-600" aria-hidden="true" />
          <h1 className="text-3xl font-semibold tracking-tight text-slate-900">Payment was canceled</h1>
          <p className="mt-3 text-base leading-relaxed text-slate-600">
            No charge was completed. You can choose another plan whenever you are ready.
          </p>
          <div className="mt-8 flex justify-center">
            <Link to={ROUTES.PRICING} className={publicButtonClass('primary')}>
              View Plans
            </Link>
          </div>
        </div>
      </div>
    </PublicLayout>
  );
}
