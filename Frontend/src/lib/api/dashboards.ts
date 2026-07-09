import { apiFetch } from './client';

export interface WidgetItem {
  savedChartId: string;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
}

export interface DashboardWidgetItem {
  id: string;
  savedChartId: string;
  chartTitle: string;
  chartType: string;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
}

export interface DashboardResponse {
  id: string;
  name: string;
  widgets: DashboardWidgetItem[];
}

export function getDashboard(): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>('/api/dashboards');
}

export function saveWidgets(widgets: WidgetItem[]): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>('/api/dashboards/widgets', {
    method: 'PUT',
    body: JSON.stringify({ widgets }),
  });
}
