import {
  BarChart3,
  Bolt,
  Brain,
  Check,
  Database,
  History,
  LineChart,
  LockKeyhole,
  RefreshCw,
  Sparkles,
  X,
} from 'lucide-react';

export const navItems = [
  { label: 'Product', href: '#product' },
  { label: 'Features', href: '#features' },
  { label: 'Benefits', href: '#benefits' },
  { label: 'Pricing', href: '#pricing' },
];

export const comparisonCards = [
  {
    title: 'Manuellt Arbete',
    icon: History,
    tone: 'muted',
    points: [
      { text: 'Timmar av SQL-kodning', icon: X },
      { text: 'Svåra Excel-formler', icon: X },
      { text: 'Statiska och utdaterade PDF-rapporter', icon: X },
    ],
  },
  {
    title: 'AI Dashboard',
    icon: Bolt,
    tone: 'accent',
    points: [
      { text: 'Direkt koppling till din data', icon: Check },
      { text: 'Fråga i fritext, få svar direkt', icon: Check },
      { text: 'Instrumentpaneler i realtid', icon: Check },
    ],
  },
];

export const features = [
  {
    title: 'Sömlös Koppling',
    description:
      'Anslut säkert till SQL, NoSQL eller molndatabaser. Vi stödjer alla stora leverantörer med krypterad dataöverföring.',
    icon: Database,
  },
  {
    title: 'AI-Driven Visualisering',
    description:
      'Ställ frågor i fritext och få omedelbara grafer. Vår AI förstår komplexa frågor och väljer rätt diagramtyp för dina behov.',
    icon: Brain,
    highlighted: true,
  },
  {
    title: 'Realtidsuppdateringar',
    description:
      'Dina instrumentpaneler uppdateras i takt med din data. Se förändringar i realtid utan att behöva uppdatera sidan.',
    icon: RefreshCw,
  },
];

export const trustLogos = ['TECHCORP', 'DATAFLUX', 'NORDIC AI', 'CLOUDWISE'];

export const footerGroups = [
  {
    title: 'PRODUKT',
    links: ['Features', 'Integrations', 'Pricing'],
  },
  {
    title: 'FÖRETAG',
    links: ['Privacy Policy', 'Terms of Service', 'Security'],
  },
  {
    title: 'SUPPORT',
    links: ['Status', 'Contact'],
  },
];

export const previewMetrics = [
  { label: 'Revenue', value: '+24.8%', trend: 'Q2 forecast', accent: 'bg-cyan-action' },
  { label: 'Churn risk', value: '-11.2%', trend: 'AI detected', accent: 'bg-emerald-500' },
  { label: 'Latency', value: '84 ms', trend: 'Live source', accent: 'bg-amber-400' },
];

export const previewInsights = [
  { icon: Sparkles, text: 'AI föreslår linjediagram för månadstrend' },
  { icon: LineChart, text: '3 avvikelser hittade i produktsegment' },
  { icon: LockKeyhole, text: 'Krypterad anslutning till datalagret' },
  { icon: BarChart3, text: 'Automatisk dashboard skapad på 8 sekunder' },
];
