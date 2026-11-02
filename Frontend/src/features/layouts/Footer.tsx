import SectionLink from '../components/SectionLink';
import { ROUTES } from '../routes';
import type { LandingSectionId } from '../navigation/publicNav';
import logoSrc from '../../assets/images/IconTransNoText.png';

type FooterLink = {
  label: string;
  to: string;
  sectionId?: LandingSectionId;
};

const footerGroups: { title: string; links: FooterLink[] }[] = [
  {
    title: 'PRODUCT',
    links: [
      { label: 'Features', to: ROUTES.HOME, sectionId: 'features' },
      { label: 'Platform', to: ROUTES.HOME, sectionId: 'platform' },
      { label: 'Pricing', to: ROUTES.PRICING },
    ],
  },
  {
    title: 'COMPANY',
    links: [
      { label: 'Privacy Policy', to: ROUTES.PRIVACY },
      { label: 'Terms of Service', to: ROUTES.TERMS },
    ],
  },
  {
    title: 'SUPPORT',
    links: [{ label: 'Contact', to: ROUTES.CONTACT }],
  },
];

export default function Footer() {
  return (
    <footer className="w-full border-t border-outline-variant bg-surface-container-lowest py-8">
      <div className="mx-auto grid w-full max-w-container-max grid-cols-1 gap-gutter px-gutter md:grid-cols-2 lg:grid-cols-4">
        <div>
          <div className="mb-4 flex items-center gap-2.5 text-headline-md font-bold text-primary">
            <img src={logoSrc} alt="" className="size-8 object-contain" />
            AI Dashboard
          </div>
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
                  <SectionLink
                    className="text-body-sm text-on-surface-variant transition-all hover:text-primary hover:underline"
                    to={link.to}
                    sectionId={link.sectionId}
                  >
                    {link.label}
                  </SectionLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <div className="mx-auto mt-12 w-full max-w-container-max border-t border-outline-variant/50 px-gutter pt-6">
        <p className="text-body-sm text-on-surface-variant">
          © 2026 Acutulus Intelligence. All rights reserved.
        </p>
      </div>
    </footer>
  );
}
