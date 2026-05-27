import { footerGroups } from '../../data/homePage.js';

export default function Footer() {
  return (
    <footer className="w-full border-t border-outline-variant bg-surface-container-lowest py-8">
      <div className="content-shell grid grid-cols-1 gap-gutter md:grid-cols-2 lg:grid-cols-4">
        <div>
          <div className="mb-4 text-headline-md font-bold text-primary">AI Dashboard</div>
          <p className="max-w-xs text-body-sm text-on-surface-variant">
            Intelligent dataanalys för moderna företag. Visualisera framtiden med AI.
          </p>
        </div>

        {footerGroups.map((group) => (
          <div key={group.title}>
            <h4 className="mb-4 font-mono text-label-caps text-on-surface">{group.title}</h4>
            <ul className="space-y-2">
              {group.links.map((link) => (
                <li key={link}>
                  <a
                    className="text-body-sm text-on-surface-variant transition-all hover:text-primary hover:underline"
                    href="#product"
                  >
                    {link}
                  </a>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <div className="content-shell mt-12 border-t border-outline-variant/50 pt-6">
        <p className="text-body-sm text-on-surface-variant">
          © 2026 AI Dashboard Intelligence. All rights reserved.
        </p>
      </div>
    </footer>
  );
}
