export default function AiPulseIndicator() {
  return (
    <div className="fixed bottom-6 right-6 z-50 flex items-center gap-3 rounded-full border border-outline-variant bg-surface-container-lowest px-4 py-2 shadow-xl">
      <div className="relative flex items-center justify-center">
        <span className="absolute inline-flex size-4 animate-ping rounded-full bg-cyan-400 opacity-20" />
        <span className="relative inline-flex size-2 rounded-full bg-cyan-500" />
      </div>
      <p className="text-body-sm text-on-surface-variant">
        <span className="font-bold text-secondary">8 AI Models</span> running diagnostics
      </p>
    </div>
  );
}
