import { Loader2, Sparkles } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import type { ChartSuggestion } from '../../services/graphsApi';
import { get } from '../charts/registry';

interface ChartRecommendationCardsProps {
  recommendations: ChartSuggestion[];
  loading?: boolean;
  selectedType?: string;
  onUse: (chartType: string) => void;
}

const CONFIDENCE_LABEL: Record<ChartSuggestion['confidence'], string> = {
  high: 'Strong match',
  medium: 'Good match',
  low: 'Possible',
};

export default function ChartRecommendationCards({
  recommendations,
  loading,
  selectedType,
  onUse,
}: ChartRecommendationCardsProps) {
  if (loading) {
    return (
      <div className="text-muted-foreground flex items-center gap-2 text-sm">
        <Loader2 className="size-4 animate-spin" />
        Analysing the columns…
      </div>
    );
  }

  const known = recommendations
    .map((recommendation) => ({ recommendation, descriptor: get(recommendation.chartType) }))
    .filter((entry): entry is { recommendation: ChartSuggestion; descriptor: NonNullable<ReturnType<typeof get>> } =>
      Boolean(entry.descriptor),
    )
    .slice(0, 3);

  if (known.length === 0) return null;

  return (
    <div className="grid gap-3">
      <div className="flex items-center gap-2">
        <Sparkles className="text-primary size-4" />
        <span className="text-sm font-medium">Recommended for this data</span>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        {known.map(({ recommendation, descriptor }) => {
          const Icon = descriptor.icon;
          const selected = selectedType === recommendation.chartType;
          return (
            <div
              key={recommendation.chartType}
              className={`flex flex-col gap-2 rounded-xl border p-3 transition-colors ${
                selected ? 'border-primary bg-primary/5' : 'border-border'
              }`}
            >
              <div className="flex items-center gap-2">
                <Icon className="text-muted-foreground size-4" />
                <span className="font-medium">{descriptor.label}</span>
                <Badge
                  variant={recommendation.confidence === 'high' ? 'default' : 'secondary'}
                  className="ml-auto"
                >
                  {CONFIDENCE_LABEL[recommendation.confidence]}
                </Badge>
              </div>
              <p className="text-muted-foreground text-xs leading-snug">
                {recommendation.reasons.join(' ')}
              </p>
              <Button
                type="button"
                size="sm"
                variant={selected ? 'default' : 'outline'}
                className="mt-auto w-fit"
                onClick={() => onUse(recommendation.chartType)}
              >
                {selected ? 'Selected' : 'Use this type'}
              </Button>
            </div>
          );
        })}
      </div>
    </div>
  );
}
