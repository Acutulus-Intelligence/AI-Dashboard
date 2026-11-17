import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import PageMeta from '../components/PageMeta';
import PublicLayout from './PublicLayout';
import PublicPageIntro from './PublicPageIntro';
import { publicContainerClass, publicLinkClass, publicPageClass } from './publicChrome';
import { ROUTES } from '../routes';

interface LegalDocumentProps {
  title: string;
  summary: string;
  path: string;
  children: ReactNode;
}

export function LegalSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section>
      <h2 className="text-xl font-semibold tracking-tight text-slate-900">{title}</h2>
      <div className="mt-3 space-y-3 text-base leading-relaxed text-slate-600">{children}</div>
    </section>
  );
}

export default function LegalDocument({ title, summary, path, children }: LegalDocumentProps) {
  return (
    <PublicLayout>
      <PageMeta title={`${title} — AI Dashboard`} description={summary} path={path} />
      <article className={`${publicPageClass} py-16 md:py-24`}>
        <div className={publicContainerClass}>
          <div className="mx-auto max-w-3xl">
            <PublicPageIntro eyebrow="Acutulus Intelligence" title={title} align="start">
              {summary}
            </PublicPageIntro>
            <p className="mt-4 text-sm text-slate-500">Last updated 25 September 2026</p>

            <div className="mt-10 space-y-8">{children}</div>

            <p className="mt-12 border-t border-slate-200 pt-6 text-sm text-slate-600">
              Related:{' '}
              <Link to={ROUTES.PRIVACY} className={publicLinkClass}>
                Privacy Policy
              </Link>
              {' · '}
              <Link to={ROUTES.TERMS} className={publicLinkClass}>
                Terms of Service
              </Link>
              {' · '}
              <Link to={ROUTES.CONTACT} className={publicLinkClass}>
                Contact
              </Link>
            </p>
          </div>
        </div>
      </article>
    </PublicLayout>
  );
}
