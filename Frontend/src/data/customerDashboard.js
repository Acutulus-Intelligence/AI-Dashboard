import {
  Activity,
  BarChart3,
  Bell,
  CircleHelp,
  Database,
  Filter,
  History,
  LayoutDashboard,
  MemoryStick,
  MoreVertical,
  Network,
  Plus,
  Search,
  Settings,
  ShieldCheck,
  SlidersHorizontal,
  Sparkles,
  Workflow,
} from 'lucide-react';

export const sidebarItems = [
  { label: 'Dashboards', icon: LayoutDashboard, active: true },
  { label: 'Database', icon: Database },
  { label: 'History', icon: History },
  { label: 'Settings', icon: Settings },
];

export const topbarActions = [
  { label: 'Notifications', icon: Bell },
  { label: 'Help', icon: CircleHelp },
];

export const dashboardControls = [
  { label: 'Filter', icon: Filter },
  { label: 'Sort', icon: SlidersHorizontal },
];

export const dashboards = [
  {
    title: 'Neural Network Traffic',
    description:
      'Real-time monitoring of packet density and throughput across the primary data fabric layers.',
    category: 'Live Analytics',
    modified: 'Modified: 2m ago',
    icon: Activity,
    featured: true,
    live: true,
  },
  {
    title: 'Compute Allocation',
    description: 'GPU/CPU clusters distribution metrics.',
    category: 'Infrastructure',
    modified: 'Jan 24, 2024',
    icon: MemoryStick,
  },
  {
    title: 'Security Audit Log',
    description: 'Anomaly detection and access history.',
    category: 'Security',
    modified: 'Jan 22, 2024',
    icon: ShieldCheck,
  },
  {
    title: 'Model Training Progress',
    description: 'LLM fine-tuning convergence curves.',
    category: 'R&D',
    modified: 'Jan 20, 2024',
    icon: Workflow,
  },
];

export const visualDashboard = {
  title: 'Cognitive Mapping v2',
  description: 'Visualizing semantic relations across datasets.',
  category: 'AI Generated Insight',
  modified: 'Jan 18, 2024',
  icon: Sparkles,
};

export const quickStats = [
  { label: 'Active dashboards', value: '12', accent: 'bg-cyan-action' },
  { label: 'Connected sources', value: '8', accent: 'bg-emerald-500' },
  { label: 'AI jobs today', value: '148', accent: 'bg-tertiary' },
];

export const cardActionIcon = MoreVertical;
export const createIcon = Plus;
export const searchIcon = Search;
export const graphIcon = BarChart3;
export const networkIcon = Network;
