/**
 * How wide a title or a description is in a search result, in columns: a Chinese, Japanese or Korean character
 * takes two, any other one. A result is limited by width, not by characters, and the SEO field's limits are in
 * columns. The same rule as `SeoTextWidth` on the server.
 */
export function seoTextColumns(text: string | null | undefined): number {
  let columns = 0;
  for (const character of text ?? '') {
    columns += isWide(character.codePointAt(0) ?? 0) ? 2 : 1;
  }
  return columns;
}

function isWide(c: number): boolean {
  return (
    (c >= 0x1100 && c <= 0x115f) ||
    (c >= 0x2e80 && c <= 0x303e) ||
    (c >= 0x3041 && c <= 0x33ff) ||
    (c >= 0x3400 && c <= 0x4dbf) ||
    (c >= 0x4e00 && c <= 0x9fff) ||
    (c >= 0xa000 && c <= 0xa4cf) ||
    (c >= 0xac00 && c <= 0xd7a3) ||
    (c >= 0xf900 && c <= 0xfaff) ||
    (c >= 0xfe30 && c <= 0xfe4f) ||
    (c >= 0xff00 && c <= 0xff60) ||
    (c >= 0xffe0 && c <= 0xffe6) ||
    (c >= 0x20000 && c <= 0x2fffd) ||
    (c >= 0x30000 && c <= 0x3fffd)
  );
}
