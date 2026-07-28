import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Pencil, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { ROUTES } from '../routes';
import AppShell from '../layouts/AppShell';
import DashboardEditHeader from '../layouts/DashboardEditHeader';
import DashboardGrid from '../sections/DashboardGrid';
import SavedChartsPicker from '../components/SavedChartsPicker';
import ConfirmDialog from '../components/ConfirmDialog';
import TextWidgetDropdown from '../components/TextWidgetDropdown';
import type { DashboardGridHandle } from '../sections/DashboardGrid';

export default function DashboardPage() {
  const navigate = useNavigate();
  const gridRef = useRef<DashboardGridHandle>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [saving, setSaving] = useState(false);
  const [resetConfirmOpen, setResetConfirmOpen] = useState(false);

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

  return (
    <>
      <AppShell
        breadcrumbs={[{ label: 'Dashboard' }]}
        onNewChart={() => navigate(ROUTES.GRAPHS_NEW)}
        onNewDashboard={() => setResetConfirmOpen(true)}
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
        <DashboardGrid ref={gridRef} editMode={editMode} />
      </AppShell>

      <SavedChartsPicker
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(savedChartId) => {
          gridRef.current?.addWidget(savedChartId);
        }}
      />

      <ConfirmDialog
        open={resetConfirmOpen}
        onOpenChange={setResetConfirmOpen}
        title="Reset dashboard?"
        description="This resets the dashboard to the default layout. Unsaved layout changes may be lost."
        confirmLabel="Reset"
        variant="destructive"
        onConfirm={() => {
          gridRef.current?.resetDashboard();
          setResetConfirmOpen(false);
        }}
      />
    </>
  );
}
