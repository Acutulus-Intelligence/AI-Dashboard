import { useRef, useState } from 'react';
import { Pencil, Plus } from 'lucide-react';
import DashboardHeader from '../layouts/DashboardHeader';
import DashboardGrid from '../sections/DashboardGrid';
import ChartTypePicker from '../components/ChartTypePicker';
import type { DashboardGridHandle } from '../sections/DashboardGrid';

export default function DashboardPage() {
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
      />
      <main className="pt-16">
        <div className="mx-auto max-w-container-max px-gutter py-8">
          <div className="mb-6 flex items-center justify-between border-b border-outline-variant/40 pb-3">
            <h1 className="text-headline-md font-bold text-on-background">Dashboard</h1>
            <div className="flex items-center gap-2">
              {editMode && (
                <button
                  type="button"
                  onClick={() => setPickerOpen(true)}
                  className="inline-flex cursor-pointer items-center justify-center rounded-lg p-2 text-primary transition-colors hover:bg-primary/10 active:scale-90"
                  title="Add existing chart"
                >
                  <Plus size={20} />
                </button>
              )}
              <button
                type="button"
                onClick={() => setEditMode((prev) => !prev)}
                className={`inline-flex cursor-pointer items-center justify-center rounded-lg p-2 transition-colors active:scale-90 ${
                  editMode
                    ? 'bg-primary/10 text-primary'
                    : 'text-on-surface-variant hover:bg-surface-container'
                }`}
                title={editMode ? 'Done editing' : 'Edit dashboard'}
              >
                <Pencil size={18} />
              </button>
            </div>
          </div>
          <div className={editMode ? 'min-h-[calc(100vh-10rem)]' : ''}>
            <DashboardGrid ref={gridRef} editMode={editMode} />
          </div>
        </div>
      </main>
      <ChartTypePicker
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(chartId) => {
          gridRef.current?.addWidget(chartId);
        }}
      />
    </div>
  );
}
