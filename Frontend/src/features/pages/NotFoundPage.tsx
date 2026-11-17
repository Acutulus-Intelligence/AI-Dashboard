import { useLocation, useNavigate } from 'react-router-dom';
import { ArrowLeft, FileQuestion } from 'lucide-react';
import PageMeta from '../components/PageMeta';
import PublicButton from '../components/PublicButton';
import PublicLayout from '../layouts/PublicLayout';
import { publicPageClass } from '../layouts/publicChrome';

export default function NotFoundPage() {
  const navigate = useNavigate();
  const { pathname } = useLocation();

  return (
    <PublicLayout>
      <PageMeta
        title="Page not found — AI Dashboard"
        description="That address is not part of AI Dashboard."
        path={pathname}
      />
      <div className={`flex flex-1 items-center justify-center px-5 py-16 sm:px-6 ${publicPageClass}`}>
        <div className="w-full max-w-lg text-center">
          <div className="mx-auto mb-6 flex size-14 items-center justify-center rounded-2xl bg-blue-50 text-blue-700">
            <FileQuestion className="size-7" aria-hidden="true" />
          </div>
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-800">404</p>
          <h1 className="mt-3 text-balance text-3xl font-semibold tracking-tight text-slate-900 sm:text-4xl">
            Page not found
          </h1>
          <p className="mt-3 text-pretty text-base leading-relaxed text-slate-600 sm:text-lg">
            That address is not part of AI Dashboard. Check the link, or head back to a page that exists.
          </p>
          <div className="mt-8 flex justify-center">
            <PublicButton type="button" variant="secondary" onClick={() => navigate(-1)}>
              <ArrowLeft className="size-4" aria-hidden="true" />
              Back
            </PublicButton>
          </div>
        </div>
      </div>
    </PublicLayout>
  );
}
