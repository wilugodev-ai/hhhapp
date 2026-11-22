export interface SourceFile {
  name: string;
  count: number;
}

export interface SourcePage {
  table: string;
  total: number;
  page: number;
  pageSize: number;
  columns: string[];
  rows: string[][];
}

export interface HealthcareFilePage extends SourcePage {
  names: Record<string, string>;
}
