import Button from '../ui/Button.jsx';

export default function CtaSection() {
  return (
    <section id="pricing" className="relative overflow-hidden bg-primary-container px-gutter py-16">
      <div
        className="absolute inset-0 opacity-10"
        style={{
          backgroundImage: 'radial-gradient(circle at 2px 2px, #06b6d4 1px, transparent 0)',
          backgroundSize: '24px 24px',
        }}
      />
      <div className="relative z-10 mx-auto max-w-container-max text-center">
        <h2 className="mb-6 text-4xl font-bold leading-tight text-on-primary md:text-display-lg">
          Redo att transformera din data?
        </h2>
        <p className="mx-auto mb-8 max-w-2xl text-body-lg text-on-primary-container">
          Gå med i tusentals analytiker som sparar tid varje dag med AI Dashboard. Starta din
          kostnadsfria provperiod idag.
        </p>
        <div className="flex flex-wrap justify-center gap-4">
          <Button href="/app">Registrera dig nu</Button>
          <Button href="/app" variant="surface">
            Login
          </Button>
        </div>
      </div>
    </section>
  );
}
