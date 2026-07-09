import { Mail, Phone, Clock } from 'lucide-react';
import Header from '../layouts/Header';
import Footer from '../layouts/Footer';

export default function ContactPage() {
  return (
    <div className="min-h-screen bg-background text-on-background">
      <Header />

      <main className="pt-24">
        <div className="mx-auto max-w-2xl px-gutter pb-24 pt-12">
          <h1 className="text-display-md font-bold text-on-background">Contact us</h1>
          <p className="mt-3 text-body-lg text-on-surface-variant">
            Get in touch with our team for enterprise inquiries and support.
          </p>

          <div className="mt-12 space-y-6">
            <div className="flex items-start gap-4 rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
                <Mail size={24} />
              </div>
              <div>
                <h2 className="text-headline-sm font-semibold text-on-background">Email</h2>
                <p className="mt-1 text-body-md text-on-surface-variant">
                  Send us an email and we&apos;ll get back to you within 24 hours.
                </p>
                <a
                  href="mailto:sales@actulusintelligence.com"
                  className="mt-2 inline-block text-body-md font-medium text-primary hover:underline"
                >
                  sales@actulusintelligence.com
                </a>
              </div>
            </div>

            <div className="flex items-start gap-4 rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
                <Phone size={24} />
              </div>
              <div>
                <h2 className="text-headline-sm font-semibold text-on-background">Phone</h2>
                <p className="mt-1 text-body-md text-on-surface-variant">
                  Call us during business hours for immediate assistance.
                </p>
                <a
                  href="tel:+46123456789"
                  className="mt-2 inline-block text-body-md font-medium text-primary hover:underline"
                >
                  +46 12 345 67 89
                </a>
              </div>
            </div>

            <div className="flex items-start gap-4 rounded-2xl border border-outline-variant bg-surface p-6 shadow-sm">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
                <Clock size={24} />
              </div>
              <div>
                <h2 className="text-headline-sm font-semibold text-on-background">Business hours</h2>
                <p className="mt-1 text-body-md text-on-surface-variant">
                  Monday – Friday: 09:00 – 17:00 CET
                </p>
                <p className="text-body-md text-on-surface-variant">
                  Saturday – Sunday: Closed
                </p>
              </div>
            </div>
          </div>
        </div>
      </main>

      <Footer />
    </div>
  );
}
