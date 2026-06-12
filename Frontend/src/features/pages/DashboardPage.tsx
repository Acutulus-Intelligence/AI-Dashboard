import { useRef, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import DashboardHeader from '../layouts/DashboardHeader';
import DashboardGrid from '../sections/DashboardGrid';
import ChartTypePicker from '../components/ChartTypePicker';
import type { DashboardGridHandle } from '../sections/DashboardGrid';

export default function DashboardPage() {
  const { user } = useAuth();
  const gridRef = useRef<DashboardGridHandle>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [editMode, setEditMode] = useState(false);

  return (
    <div className="min-h-screen bg-background">
      <DashboardHeader
        onToggleNavbar={() => console.log('sidebar toggle')}
        onNewChart={() => setPickerOpen(true)}
        onNewDashboard={() => {
          if (window.confirm('Reset dashboard to default layout?')) {
            gridRef.current?.resetDashboard();
          }
        }}
        editMode={editMode}
        onToggleEditMode={() => setEditMode((prev) => !prev)}
      />
      <main className="pt-16">
        <div className="mx-auto max-w-container-max px-gutter py-8">
          <div className="mb-4 text-body-sm text-on-surface-variant">
            Logged in as <span className="font-semibold">{user?.email}</span>
          </div>
          <DashboardGrid ref={gridRef} editMode={editMode} />
        </div>
      </main>
      <ChartTypePicker
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(chartId) => {
          gridRef.current?.addWidget(chartId);
          setPickerOpen(false);
        }}
      />
    </div>
  );
}
