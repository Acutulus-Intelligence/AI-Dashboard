import { Clock, Mail, Phone, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import PageMeta from '../components/PageMeta';
import PublicLayout from '../layouts/PublicLayout';
import PublicPageIntro from '../layouts/PublicPageIntro';
import { publicCardClass, publicContainerClass, publicLinkClass, publicPageClass } from '../layouts/publicChrome';
import { ROUTES } from '../routes';

function ContactCard({
  icon: Icon,
  title,
  children,
}: {
  icon: LucideIcon;
  title: string;
  children: ReactNode;
}) {
  return (
    <article className={`landing-card flex h-full flex-col p-6 sm:p-8 ${publicCardClass}`}>
      <div className="mb-5 flex size-12 items-center justify-center rounded-2xl bg-blue-50 text-blue-700">
        <Icon className="size-6" aria-hidden="true" />
      </div>
      <h2 className="text-lg font-semibold tracking-tight text-slate-900">{title}</h2>
      <div className="mt-2 text-sm leading-relaxed text-slate-600">{children}</div>
    </article>
  );
}

export default function ContactPage() {
  return (
    <PublicLayout>
      <PageMeta
        title="Contact — AI Dashboard"
        description="Contact Acutulus Intelligence about AI Dashboard enterprise plans, billing, and support."
        path={ROUTES.CONTACT}
      />

      <div className={`${publicPageClass} py-16 md:py-24`}>
        <div className={publicContainerClass}>
          <PublicPageIntro eyebrow="Contact" title="Contact us">
            Get in touch with our team for enterprise inquiries and support.
          </PublicPageIntro>

          <div className="mt-12 grid gap-5 md:grid-cols-3">
            <ContactCard icon={Mail} title="Email">
              <p>Send us an email and we&apos;ll get back to you within 24 hours.</p>
              <a href="mailto:sales@actulusintelligence.com" className={`mt-3 inline-block ${publicLinkClass}`}>
                sales@actulusintelligence.com
              </a>
            </ContactCard>

            <ContactCard icon={Phone} title="Phone">
              <p>Call us during business hours for immediate assistance.</p>
              <a href="tel:+46123456789" className={`mt-3 inline-block ${publicLinkClass}`}>
                +46 12 345 67 89
              </a>
            </ContactCard>

            <ContactCard icon={Clock} title="Business hours">
              <p>Monday – Friday: 09:00 – 17:00 CET</p>
              <p>Saturday – Sunday: Closed</p>
            </ContactCard>
          </div>
        </div>
      </div>
    </PublicLayout>
  );
}
