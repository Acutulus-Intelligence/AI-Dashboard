import { useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Pencil, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { ROUTES } from '../routes';
import AppShell from '../layouts/AppShell';
import DashboardEditHeader from '../layouts/DashboardEditHeader';
import DashboardGrid from '../sections/DashboardGrid';
import SavedChartsPicker from '../components/SavedChartsPicker';
import TextWidgetDropdown from '../components/TextWidgetDropdown';
import type { DashboardGridHandle } from '../sections/DashboardGrid';

export default function DashboardPage() {
  const navigate = useNavigate();
  const { dashboardId } = useParams<{ dashboardId: string }>();
  const gridRef = useRef<DashboardGridHandle>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dashboardName, setDashboardName] = useState<string | null>(null);

  const handleSaveEdit = async () => {
    setSaving(true);
    try {
      await gridRef.current?.saveEdit();
      setEditMode(false);
    } finally {
      setSaving(false);
    }
  };

  const handleCancelEdit = () => {
    gridRef.current?.cancelEdit();
    setEditMode(false);
  };

  const editHeader = (
    <DashboardEditHeader saving={saving} onSave={handleSaveEdit} onCancel={handleCancelEdit}>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="ghost" size="icon" onClick={() => setPickerOpen(true)}>
            <Plus />
            <span className="sr-only">Add existing chart</span>
          </Button>
        </TooltipTrigger>
        <TooltipContent>Add existing chart</TooltipContent>
      </Tooltip>
      <TextWidgetDropdown onSelect={(variant) => gridRef.current?.addTextWidget(variant)} />
    </DashboardEditHeader>
  );

  if (!dashboardId) {
    return null;
  }

  return (
    <>
      <AppShell
        breadcrumbs={[
          { label: 'Dashboards', to: ROUTES.DASHBOARD },
          { label: dashboardName ?? 'Dashboard' },
        ]}
        onNewChart={() => navigate(ROUTES.GRAPHS_NEW)}
        onNewDashboard={() => navigate(ROUTES.DASHBOARD)}
        header={editMode ? editHeader : undefined}
        headerActions={
          <Tooltip>
            <TooltipTrigger asChild>
              <Button variant="ghost" size="icon" onClick={() => setEditMode(true)}>
                <Pencil />
                <span className="sr-only">Edit dashboard</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent>Edit dashboard</TooltipContent>
          </Tooltip>
        }
      >
        <DashboardGrid
          key={dashboardId}
          ref={gridRef}
          dashboardId={dashboardId}
          editMode={editMode}
          onLoaded={setDashboardName}
        />
      </AppShell>

      <SavedChartsPicker
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(savedChartId) => {
          gridRef.current?.addWidget(savedChartId);
        }}
      />
    </>
  );
}
