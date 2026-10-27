/// <reference types="vite/client" />
declare module 'virtual:frontier-content' {
  export const catalogText: string;
  export const mapTexts: Record<string, string>;
  export const contentHash: string;
  export const protocolVersion: number;
  export const rulesVersion: number;
  export const spanishCatalog: { code: string; entries: { key: string; value: string; template?: boolean }[] };
  export const musicUrls: Record<string, string>;
}
