export interface BusinessQueryPlan {
  entity: string;
  select: string[];
  filters: { field: string; operator: string; value: unknown }[];
  sort: { field: string; direction: string }[];
  aggregations: { function: string; field: string | null; alias: string }[];
  groupBy: string[];
  limit: number;
}
export interface BusinessQueryResult {
  columns: string[];
  rows: Record<string, unknown>[];
  rowCount: number;
  truncated: boolean;
  durationMs: number;
}
export interface BusinessQueryColumnDisplay {
  name: string;
  displayName: string;
  dataType: string;
}
export interface BusinessQueryResponse {
  answer: string;
  plan: BusinessQueryPlan;
  result: BusinessQueryResult;
  displayColumns: BusinessQueryColumnDisplay[];
  relationships?: string[];
  dataSourceName?: string;
  scopeName?: string;
  selectedEntities?: string[];
}
