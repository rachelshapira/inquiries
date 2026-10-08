/** Display only: retain original titles, keys and identifiers in persisted data. */
export function businessTitle(title: string, fallback = 'פנייה'): string {
  const display = title
    .replace(/\b(?:E2E|TEST)[\s:_-]+(?:\d{8,}|[a-f\d]{8}(?:-[a-f\d-]+)?)\b/gi, '')
    .replace(/\s+[–—|]\s*:/g, ':')
    .replace(/^[\s–—|:-]+|[\s–—|:-]+$/g, '')
    .trim();
  // Only explicit test-labelled titles lose generated hexadecimal tokens.
  return (/בדיק[תה]|\b(?:test|check)\b/i.test(display)
    ? display.replace(/\s+(?=[a-f\d-]*[a-f])[a-f\d]{8,32}(?:-[a-f\d-]+)?(?=\s|:|$)/gi, '')
    : display) || fallback;
}

export function stateLabel(label: string): string {
  const labels: Record<string, string> = {
    Draft: 'טיוטה', Submitted: 'הוגשה', InTreatment: 'בטיפול',
    ClosedWithResponse: 'נסגרה עם מענה',
  };
  return labels[label] ?? label;
}
