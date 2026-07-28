import { Fragment } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { CreditCard, LogOut, Shield, User } from 'lucide-react';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Separator } from '@/components/ui/separator';
import { SidebarTrigger } from '@/components/ui/sidebar';
import CreateDropdown from '../components/CreateDropdown';
import ThemeToggle from '../components/ThemeToggle';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';

export interface Crumb {
  label: string;
  to?: string;
}

interface AppHeaderProps {
  breadcrumbs: Crumb[];
  onNewChart?: () => void;
  onNewDashboard?: () => void;
  /** Extra controls rendered left of the theme toggle. */
  actions?: React.ReactNode;
}

export default function AppHeader({
  breadcrumbs,
  onNewChart,
  onNewDashboard,
  actions,
}: AppHeaderProps) {
  const { user, hasActiveSubscription, logout } = useAuth();
  const navigate = useNavigate();
  const isCompany = user?.userType === 1;

  const initials =
    [user?.firstName?.[0], user?.lastName?.[0]].filter(Boolean).join('').toUpperCase() ||
    user?.email?.[0]?.toUpperCase() ||
    '?';

  const showCreate = hasActiveSubscription && (onNewChart || onNewDashboard);

  return (
    <header className="bg-background/95 supports-[backdrop-filter]:bg-background/60 sticky top-0 z-30 flex h-16 shrink-0 items-center gap-2 border-b backdrop-blur">
      <div className="flex w-full items-center gap-2 px-4">
        <SidebarTrigger className="-ml-1" />
        <Separator orientation="vertical" className="mr-1 data-[orientation=vertical]:h-4" />

        <Breadcrumb>
          <BreadcrumbList>
            {breadcrumbs.map((crumb, i) => {
              const isLast = i === breadcrumbs.length - 1;
              return (
                <Fragment key={`${crumb.label}-${i}`}>
                  <BreadcrumbItem className={isLast ? undefined : 'hidden md:block'}>
                    {isLast || !crumb.to ? (
                      <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
                    ) : (
                      <BreadcrumbLink asChild>
                        <Link to={crumb.to}>{crumb.label}</Link>
                      </BreadcrumbLink>
                    )}
                  </BreadcrumbItem>
                  {!isLast && <BreadcrumbSeparator className="hidden md:block" />}
                </Fragment>
              );
            })}
          </BreadcrumbList>
        </Breadcrumb>

        <div className="ml-auto flex items-center gap-2">
          {actions}
          {showCreate && (
            <CreateDropdown
              onNewChart={onNewChart ?? (() => {})}
              onNewDashboard={onNewDashboard ?? (() => {})}
            />
          )}
          <ThemeToggle />

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="rounded-full" aria-label="Account menu">
                <Avatar className="size-7">
                  <AvatarFallback className="text-xs">{initials}</AvatarFallback>
                </Avatar>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              <DropdownMenuLabel className="font-normal">
                <div className="grid leading-tight">
                  <span className="truncate text-sm font-medium">
                    {[user?.firstName, user?.lastName].filter(Boolean).join(' ') || 'Signed in'}
                  </span>
                  <span className="text-muted-foreground truncate text-xs">{user?.email}</span>
                </div>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => navigate(ROUTES.PROFILE)}>
                <User />
                Profile
              </DropdownMenuItem>
              {isCompany ? (
                <DropdownMenuItem onSelect={() => navigate(ROUTES.ADMIN)}>
                  <Shield />
                  {user?.companyRoleName === 'Owner' ? 'Admin settings' : 'Company'}
                </DropdownMenuItem>
              ) : (
                <DropdownMenuItem onSelect={() => navigate(ROUTES.SETTINGS)}>
                  <CreditCard />
                  Settings
                </DropdownMenuItem>
              )}
              <DropdownMenuSeparator />
              <DropdownMenuItem variant="destructive" onSelect={() => void logout()}>
                <LogOut />
                Sign out
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </header>
  );
}
