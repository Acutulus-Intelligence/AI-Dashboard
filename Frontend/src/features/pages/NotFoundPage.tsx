import { useNavigate } from 'react-router-dom';
import { ArrowLeft, FileQuestion } from 'lucide-react';
import Button from '../components/Button';
import Header from '../layouts/Header';

export default function NotFoundPage() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-background text-on-background">
      <Header />
      <main className="flex min-h-[calc(100vh-4rem)] items-center justify-center px-4 pt-16">
        <div className="w-full max-w-lg text-center">
          <div className="mx-auto mb-6 flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/10 text-primary">
            <FileQuestion size={28} aria-hidden="true" />
          </div>
          <p className="font-mono text-label-caps text-primary">404</p>
          <h1 className="mt-3 text-display-md font-bold text-on-background">Page not found</h1>
          <p className="mt-3 text-body-lg text-on-surface-variant">
            That address is not part of AI Dashboard. Check the link, or head back to a page that exists.
          </p>
          <div className="mt-8 flex justify-center">
            <Button type="button" variant="ghost" onClick={() => navigate(-1)}>
              <ArrowLeft size={18} aria-hidden="true" />
              Back
            </Button>
          </div>
        </div>
      </main>
    </div>
  );
}
