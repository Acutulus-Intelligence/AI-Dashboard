import { dashboards, graphIcon, visualDashboard } from '../../../data/customerDashboard.js';
import DashboardCard from './DashboardCard.jsx';
import NewDashboardCard from './NewDashboardCard.jsx';
import VisualDashboardCard from './VisualDashboardCard.jsx';

const GraphIcon = graphIcon;

export default function DashboardGrid() {
  return (
    <section
      className="grid grid-cols-1 gap-gutter md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4"
      aria-label="Dashboard cards"
    >
      {dashboards.map((dashboard) => (
        <DashboardCard key={dashboard.title} dashboard={dashboard} />
      ))}
      <VisualDashboardCard dashboard={visualDashboard} />
      <NewDashboardCard icon={GraphIcon} />
    </section>
  );
}
