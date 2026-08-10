import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import {
  BarChart3,
  CreditCard,
  LayoutDashboard,
  type LucideIcon,
  Shield,
} from 'lucide-react';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from '@/components/ui/sidebar';
import { ROUTES } from '../routes';
import { useAuth } from '../store/useAuth';
import * as companyApi from '../../lib/api/company';
import logoSrc from '../../assets/images/IconTransNoText.png';

interface NavItem {
  title: string;
  url: string;
  icon: LucideIcon;
}

interface NavGroup {
  label: string;
  items: NavItem[];
}

export default function AppSidebar() {
  const { user, hasActiveSubscription } = useAuth();
  const { pathname } = useLocation();
  const [companyName, setCompanyName] = useState<string | null>(null);
  const isCompany = user?.userType === 1;
  const isOwner = user?.companyRoleName === 'Owner';

  useEffect(() => {
    if (!isCompany) return;
    let active = true;
    companyApi
      .getMyCompany()
      .then((c) => {
        if (active) setCompanyName(c.name);
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, [isCompany]);

  const { mainGroups, adminGroup } = useMemo(() => {
    const main: NavGroup[] = [
      {
        label: 'Overview',
        items: [{ title: 'Dashboard', url: ROUTES.DASHBOARD, icon: LayoutDashboard }],
      },
    ];

    if (hasActiveSubscription) {
      main.push({
        label: 'Data',
        items: [{ title: 'Charts', url: ROUTES.CHARTS, icon: BarChart3 }],
      });
    }

    let admin: NavGroup | null = null;
    if (isCompany) {
      admin = {
        label: 'Administration',
        items: [
          { title: isOwner ? 'Admin settings' : 'Company', url: ROUTES.ADMIN, icon: Shield },
        ],
      };
    }

    return { mainGroups: main, adminGroup: admin };
  }, [hasActiveSubscription, isCompany, isOwner]);

  const label = useMemo(() => {
    if (isCompany) return companyName ?? 'Company';
    const name = [user?.firstName, user?.lastName].filter(Boolean).join(' ').trim();
    return name || user?.email || 'Account';
  }, [isCompany, companyName, user?.firstName, user?.lastName, user?.email]);

  function isActive(item: NavItem) {
    if (item.url === ROUTES.DASHBOARD) return pathname === ROUTES.DASHBOARD;
    return pathname === item.url || pathname.startsWith(`${item.url}/`);
  }

  function renderGroup(group: NavGroup, className?: string) {
    return (
      <SidebarGroup key={group.label} className={className}>
        <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            {group.items.map((item) => (
              <SidebarMenuItem key={item.url}>
                <SidebarMenuButton asChild isActive={isActive(item)} tooltip={item.title}>
                  <Link to={item.url}>
                    <item.icon />
                    <span>{item.title}</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            ))}
          </SidebarMenu>
        </SidebarGroupContent>
      </SidebarGroup>
    );
  }

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton asChild size="lg" tooltip={label}>
              <Link to={ROUTES.DASHBOARD}>
                <img
                  src={logoSrc}
                  alt=""
                  className="size-8 shrink-0 rounded-md object-contain dark:brightness-0 dark:invert"
                />
                <div className="grid flex-1 text-left leading-tight">
                  <span className="truncate font-semibold">Actulus</span>
                  <span className="text-muted-foreground truncate text-xs">{label}</span>
                </div>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent className="justify-between">
        <div className="flex flex-col gap-0">{mainGroups.map((group) => renderGroup(group))}</div>
        {adminGroup && renderGroup(adminGroup)}
      </SidebarContent>

      <SidebarFooter>
        {!hasActiveSubscription && (
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton asChild tooltip="Upgrade plan">
                <Link to={isCompany ? ROUTES.SUBSCRIPTION : ROUTES.SETTINGS}>
                  <CreditCard />
                  <span>Upgrade plan</span>
                </Link>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        )}
      </SidebarFooter>

      <SidebarRail />
    </Sidebar>
  );
}
