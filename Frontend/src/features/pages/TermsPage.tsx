import { Link } from 'react-router-dom';
import LegalDocument, { LegalSection } from '../layouts/LegalDocument';
import { ROUTES } from '../routes';

export default function TermsPage() {
  return (
    <LegalDocument
      title="Terms of Service"
      summary="The rules for using AI Dashboard, provided by Acutulus Intelligence."
    >
      <LegalSection title="Agreement">
        <p>
          By creating an account or using AI Dashboard, you agree to these terms. If you use the
          service for a company, you confirm that you can bind that company. If you do not agree,
          do not use the service.
        </p>
        <p>
          These terms are a plain-language summary of how we offer the product. They are not a
          substitute for advice from your own lawyer, and we may revise them as the product changes.
        </p>
      </LegalSection>

      <LegalSection title="The service">
        <p>
          AI Dashboard lets you connect a PostgreSQL or MySQL database, describe the chart you want,
          and place the result on a dashboard. Features can change, and some features require an
          active subscription.
        </p>
      </LegalSection>

      <LegalSection title="Accounts">
        <p>
          You must provide accurate account information and keep your login credentials private.
          You are responsible for activity under your account and for making sure the people you
          invite are allowed to see the data they can access.
        </p>
      </LegalSection>

      <LegalSection title="Subscriptions and billing">
        <p>
          Paid plans are billed through Stripe. Fees, trials, and plan limits are shown before you
          subscribe. Unless a plan says otherwise, subscriptions renew until you cancel. Taxes may
          apply. If a payment fails, we may pause paid features until the account is current.
        </p>
      </LegalSection>

      <LegalSection title="Your databases and content">
        <p>
          You keep ownership of the databases you connect and the prompts, charts, and dashboards
          you create. You grant us permission to host that material and to process it only so we
          can provide the service — for example, to store an encrypted connection, read schema
          metadata, run the queries you request, and render charts.
        </p>
        <p>
          You confirm that you have the right to connect each database and to let the service read
          the information needed to answer your questions.
        </p>
      </LegalSection>

      <LegalSection title="Acceptable use">
        <p>You agree not to:</p>
        <ul className="list-disc space-y-2 pl-5">
          <li>Connect a database, account, or system you are not authorized to access.</li>
          <li>Use the service to break the law, invade privacy, or distribute malware.</li>
          <li>Probe, disrupt, or overload AI Dashboard or another customer&apos;s workspace.</li>
          <li>Resell or misuse the service in a way these terms do not allow.</li>
          <li>Attempt to extract, copy, or reverse engineer the product except where the law allows.</li>
        </ul>
      </LegalSection>

      <LegalSection title="AI-generated output">
        <p>
          Charts, SQL, and other suggestions come from an AI model. They can be incomplete or wrong.
          Review queries before you rely on them, and confirm that a chart matches the question you
          asked. You are responsible for decisions you make based on the output.
        </p>
      </LegalSection>

      <LegalSection title="Intellectual property">
        <p>
          Acutulus Intelligence owns AI Dashboard, including its software, design, and branding.
          These terms do not give you ownership of the product. They give you a limited right to use
          it while your account is in good standing.
        </p>
      </LegalSection>

      <LegalSection title="Suspension and ending use">
        <p>
          You may stop using the service at any time. We may suspend or close an account that
          violates these terms, creates a security risk, or fails to pay applicable fees. Sections
          that should survive — including ownership, disclaimers, and limits on liability — still
          apply after the account ends.
        </p>
      </LegalSection>

      <LegalSection title="Disclaimers">
        <p>
          AI Dashboard is provided &quot;as is.&quot; We do not warrant that the service will be
          uninterrupted, that AI output will be accurate, or that it will meet a particular business
          or compliance need. Database analysis depends on the data you connect and the questions
          you ask.
        </p>
      </LegalSection>

      <LegalSection title="Limitation of liability">
        <p>
          To the extent the law allows, Acutulus Intelligence is not liable for indirect, incidental,
          or consequential damages, or for lost profits, data, or goodwill arising from use of the
          service. Our total liability for a claim relating to the service is limited to the amount
          you paid us for AI Dashboard in the three months before the claim.
        </p>
      </LegalSection>

      <LegalSection title="Changes">
        <p>
          We may update these terms by posting a new version on this page and changing the date
          above. If you keep using AI Dashboard after an update, you accept the revised terms.
        </p>
      </LegalSection>

      <LegalSection title="Contact">
        <p>
          Questions about these terms:{' '}
          <a href="mailto:sales@actulusintelligence.com" className="font-medium text-primary hover:underline">
            sales@actulusintelligence.com
          </a>
          , or the{' '}
          <Link to={ROUTES.CONTACT} className="font-medium text-primary hover:underline">
            contact page
          </Link>
          . See also our{' '}
          <Link to={ROUTES.PRIVACY} className="font-medium text-primary hover:underline">
            Privacy Policy
          </Link>
          .
        </p>
      </LegalSection>
    </LegalDocument>
  );
}
