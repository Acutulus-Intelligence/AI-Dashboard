import { Link } from 'react-router-dom';
import LegalDocument, { LegalSection } from '../layouts/LegalDocument';
import { ROUTES } from '../routes';

export default function PrivacyPage() {
  return (
    <LegalDocument
      title="Privacy Policy"
      summary="How Acutulus Intelligence handles information when you use AI Dashboard."
    >
      <LegalSection title="Who we are">
        <p>
          AI Dashboard is operated by Acutulus Intelligence. This policy describes the information
          the service collects, why we use it, and the choices you have. It is written for customers
          in plain language and may be updated as the product changes.
        </p>
      </LegalSection>

      <LegalSection title="Information we collect">
        <ul className="list-disc space-y-2 pl-5">
          <li>Account details you provide, such as your name, email address, and password.</li>
          <li>Company and workspace details you add while setting up AI Dashboard.</li>
          <li>Billing status for your subscription. Card payments are processed by Stripe; we do not store full card numbers.</li>
          <li>Connection settings for databases you choose to link, including host, port, database name, and credentials.</li>
          <li>Prompts you write, the chart definitions we save for you, and basic product usage needed to run the service.</li>
        </ul>
      </LegalSection>

      <LegalSection title="Your database contents">
        <p>
          When you connect a PostgreSQL or MySQL database, we inspect the schema: table names,
          column names, data types, and relationships. That metadata is what we send to the AI
          provider so it can suggest a query and a chart. We do not send the rows in your tables
          as part of schema inspection.
        </p>
        <p>
          If you run a chart, the service executes a read-only query against the database you
          connected and returns the result so the chart can render in your browser. You decide
          which databases to connect and which questions to ask.
        </p>
      </LegalSection>

      <LegalSection title="How we use information">
        <ul className="list-disc space-y-2 pl-5">
          <li>To create and secure your account and company workspace.</li>
          <li>To store encrypted database credentials and reconnect on your behalf.</li>
          <li>To generate, save, and display charts and dashboards you request.</li>
          <li>To manage subscriptions, invoices, and access to paid features.</li>
          <li>To respond to support requests and keep the service reliable.</li>
        </ul>
        <p>We do not sell your personal information.</p>
      </LegalSection>

      <LegalSection title="AI processing">
        <p>
          Chart generation sends your prompt and database schema metadata to an AI provider through
          OpenRouter. Use prompts that you are comfortable sharing with that provider. Do not put
          secrets or unnecessary personal data in a prompt.
        </p>
      </LegalSection>

      <LegalSection title="Who we share information with">
        <p>We share information with service providers that help us run AI Dashboard, including:</p>
        <ul className="list-disc space-y-2 pl-5">
          <li>Stripe, for subscription billing.</li>
          <li>The AI provider reached through OpenRouter, for chart generation from prompts and schema metadata.</li>
          <li>Infrastructure providers that host the application and its internal database.</li>
        </ul>
        <p>
          We may also disclose information if required by law, or to protect the service, our
          customers, or the public from harm.
        </p>
      </LegalSection>

      <LegalSection title="Retention and security">
        <p>
          We keep account, company, connection, and saved-dashboard data for as long as your
          workspace is active, and for a limited period afterward if we need it for billing,
          security, or legal obligations. Database credentials are encrypted before they are stored.
        </p>
        <p>
          No method of transmission or storage is perfectly secure. Protect your password and the
          credentials of any database you connect.
        </p>
      </LegalSection>

      <LegalSection title="Your choices">
        <p>
          You can update profile details in the app, disconnect a database, and delete saved charts
          and dashboards. To ask for access, correction, or deletion of account information, contact
          us and we will respond within a reasonable time.
        </p>
      </LegalSection>

      <LegalSection title="Changes">
        <p>
          If we change this policy, we will update the date above and publish the new version on
          this page. Continued use of AI Dashboard after an update means you accept the revised policy.
        </p>
      </LegalSection>

      <LegalSection title="Contact">
        <p>
          Questions about this policy:{' '}
          <a href="mailto:sales@actulusintelligence.com" className="font-medium text-primary hover:underline">
            sales@actulusintelligence.com
          </a>
          , or the{' '}
          <Link to={ROUTES.CONTACT} className="font-medium text-primary hover:underline">
            contact page
          </Link>
          .
        </p>
      </LegalSection>
    </LegalDocument>
  );
}
