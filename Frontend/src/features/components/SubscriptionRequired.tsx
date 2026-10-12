import { useNavigate } from 'react-router-dom';
import { Lock } from 'lucide-react';
import AppShell from '../layouts/AppShell';
import Button from './Button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ROUTES } from '../routes';

export default function SubscriptionRequired() {
  const navigate = useNavigate();

  return (
    <AppShell breadcrumbs={[{ label: 'Subscription required' }]}>
      <div className="flex flex-1 items-center justify-center py-12">
        <Card className="w-full max-w-md text-center">
          <CardHeader className="items-center text-center">
            <div className="mb-2 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Lock size={24} />
            </div>
            <CardTitle>An active subscription is required</CardTitle>
            <CardDescription>
              Subscribe to create dashboards, charts and database connections. You can still manage
              your profile and settings.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <Button onClick={() => navigate(ROUTES.PRICING)}>View plans</Button>
            <Button variant="outline" onClick={() => navigate(ROUTES.PROFILE)}>
              Go to profile
            </Button>
          </CardContent>
        </Card>
      </div>
    </AppShell>
  );
}
