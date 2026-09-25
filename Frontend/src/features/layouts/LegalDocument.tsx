import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import Header from './Header';
import Footer from './Footer';
import { ROUTES } from '../routes';

interface LegalDocumentProps {
  title: string;
  summary: string;
  children: ReactNode;
}

export function LegalSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section>
      <h2 className="text-headline-sm font-semibold text-on-background">{title}</h2>
      <div className="mt-3 space-y-3 text-body-md leading-relaxed text-on-surface-variant">{children}</div>
    </section>
  );
}

export default function LegalDocument({ title, summary, children }: LegalDocumentProps) {
  return (
    <div className="min-h-screen bg-background text-on-background">
      <Header />
      <main className="pt-24">
        <article className="mx-auto max-w-3xl px-gutter pb-24 pt-12">
          <p className="font-mono text-label-caps text-primary">Acutulus Intelligence</p>
          <h1 className="mt-3 text-display-md font-bold text-on-background">{title}</h1>
          <p className="mt-3 text-body-lg text-on-surface-variant">{summary}</p>
          <p className="mt-2 text-body-sm text-on-surface-variant">Last updated 25 September 2026</p>

          <div className="mt-10 space-y-8">{children}</div>

          <p className="mt-12 border-t border-outline-variant pt-6 text-body-sm text-on-surface-variant">
            Related:{' '}
            <Link to={ROUTES.PRIVACY} className="font-medium text-primary hover:underline">
              Privacy Policy
            </Link>
            {' · '}
            <Link to={ROUTES.TERMS} className="font-medium text-primary hover:underline">
              Terms of Service
            </Link>
            {' · '}
            <Link to={ROUTES.CONTACT} className="font-medium text-primary hover:underline">
              Contact
            </Link>
          </p>
        </article>
      </main>
      <Footer />
    </div>
  );
}
