import HashLink from '../components/HashLink';

const footerGroups = [
  {
    title: 'PRODUCT',
    links: [
      { label: 'Features', to: '/#features' },
      { label: 'Integrations', to: '/#product' },
      { label: 'Pricing', to: '/#pricing' },
    ],
  },
  {
    title: 'COMPANY',
    links: [
      { label: 'Company Page', to: '#' },
      { label: 'Privacy Policy', to: '#' },
      { label: 'Terms of Service', to: '#' },
      { label: 'Security', to: '#' },
    ],
  },
  {
    title: 'SUPPORT',
    links: [
      { label: 'Status', to: '#' },
      { label: 'Contact', to: '#' },
    ],
  },
];

export default function Footer() {
  return (
    <footer className="w-full border-t border-outline-variant bg-surface-container-lowest py-8">
      <div className="mx-auto grid w-full max-w-container-max grid-cols-1 gap-gutter px-gutter md:grid-cols-2 lg:grid-cols-4">
        <div>
          <div className="mb-4 text-headline-md font-bold text-primary">AI Dashboard</div>
          <p className="max-w-xs text-body-sm text-on-surface-variant">
            Intelligent data analysis for modern businesses. Visualize the future with AI.
          </p>
        </div>

        {footerGroups.map((group) => (
          <div key={group.title}>
            <h4 className="mb-4 font-mono text-label-caps text-on-surface">{group.title}</h4>
            <ul className="space-y-2">
              {group.links.map((link) => (
                <li key={link.label}>
                  <HashLink
                    className="text-body-sm text-on-surface-variant transition-all hover:text-primary hover:underline"
                    to={link.to}
                  >
                    {link.label}
                  </HashLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <div className="mx-auto mt-12 w-full max-w-container-max border-t border-outline-variant/50 px-gutter pt-6">
        <p className="text-body-sm text-on-surface-variant">
          © 2026 Actulus Intelligence. All rights reserved.
        </p>
      </div>
    </footer>
  );
}
