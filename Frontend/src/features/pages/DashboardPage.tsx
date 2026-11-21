import { useCallback, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Pencil, Plus } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { ROUTES } from '../routes';
import AppShell from '../layouts/AppShell';
import DashboardEditHeader from '../layouts/DashboardEditHeader';
import DashboardGrid from '../sections/DashboardGrid';
import { dashboardSaveErrorMessage } from '../../lib/api/dashboards';
import SavedChartsPicker from '../components/SavedChartsPicker';
import TextWidgetDropdown from '../components/TextWidgetDropdown';
import type { DashboardGridHandle, DashboardLoadState } from '../sections/DashboardGrid';

export default function DashboardPage() {
  const { dashboardId } = useParams<{ dashboardId: string }>();

  if (!dashboardId) {
    return null;
  }

  return <DashboardView key={dashboardId} dashboardId={dashboardId} />;
}

function DashboardView({ dashboardId }: { dashboardId: string }) {
  const navigate = useNavigate();
  const gridRef = useRef<DashboardGridHandle>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dashboardName, setDashboardName] = useState<string | null>(null);
  const [loadState, setLoadState] = useState<DashboardLoadState>('loading');

  const handleLoadStateChange = useCallback((state: DashboardLoadState) => {
    setLoadState(state);
    if (state !== 'ready') setEditMode(false);
  }, []);

  const handleSaveEdit = async () => {
    setSaving(true);
    try {
      await gridRef.current?.saveEdit();
      setEditMode(false);
    } catch (err) {
      toast.error(dashboardSaveErrorMessage(err));
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

  const breadcrumbLabel =
    loadState === 'not-found'
      ? 'Dashboard not found'
      : loadState === 'error'
        ? 'Dashboard unavailable'
        : dashboardName ?? 'Dashboard';

  return (
    <>
      <AppShell
        breadcrumbs={[
          { label: 'Dashboards', to: ROUTES.DASHBOARD },
          { label: breadcrumbLabel },
        ]}
        onNewChart={() => navigate(ROUTES.GRAPHS_NEW)}
        onNewDashboard={() => navigate(ROUTES.DASHBOARD)}
        header={editMode ? editHeader : undefined}
        headerActions={
          loadState === 'ready' ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="ghost" size="icon" onClick={() => setEditMode(true)}>
                  <Pencil />
                  <span className="sr-only">Edit dashboard</span>
                </Button>
              </TooltipTrigger>
              <TooltipContent>Edit dashboard</TooltipContent>
            </Tooltip>
          ) : undefined
        }
      >
        <DashboardGrid
          ref={gridRef}
          dashboardId={dashboardId}
          editMode={editMode}
          onLoaded={setDashboardName}
          onLoadStateChange={handleLoadStateChange}
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
