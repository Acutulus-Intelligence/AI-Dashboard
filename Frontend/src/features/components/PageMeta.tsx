type PageMetaProps = {
  title: string;
  description: string;
  /** Site path beginning with `/`, used for the canonical URL. */
  path: string;
};

export default function PageMeta({ title, description, path }: PageMetaProps) {
  const origin = typeof window === 'undefined' ? '' : window.location.origin;
  const canonical = `${origin}${path}`;

  return (
    <>
      <title>{title}</title>
      <meta name="description" content={description} />
      <link rel="canonical" href={canonical} />
    </>
  );
}
