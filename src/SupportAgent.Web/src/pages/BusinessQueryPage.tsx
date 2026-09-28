import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { AppHeader } from "../components/AppHeader";
import {
  askBusinessQuery,
  getBusinessDataSources,
  getDataScopes,
  type DataSourceSummary,
  type DataScopeSummary,
} from "../services/businessQueryService";
import type { BusinessQueryResponse } from "../types/businessQuery";
import { formatBusinessValue } from "../services/businessQueryFormatting";

export function BusinessQueryPage() {
  const [question, setQuestion] = useState("Show all overdue invoices.");
  const [result, setResult] = useState<BusinessQueryResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sources, setSources] = useState<DataSourceSummary[]>([]);
  const [sourceId, setSourceId] = useState("");
  const [scopes, setScopes] = useState<DataScopeSummary[]>([]);
  const [scopeId, setScopeId] = useState("");
  useEffect(() => {
    getBusinessDataSources()
      .then(setSources)
      .catch(() => setSources([]));
  }, []);
  useEffect(() => {
    setScopeId("");
    if (sourceId)
      getDataScopes(sourceId)
        .then(setScopes)
        .catch(() => setScopes([]));
    else setScopes([]);
  }, [sourceId]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!question.trim()) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      setResult(
        await askBusinessQuery(
          question.trim(),
          sourceId || undefined,
          scopeId || undefined,
        ),
      );
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : "The business query could not be completed.",
      );
    } finally {
      setLoading(false);
    }
  }
  return (
    <>
      <AppHeader />
      <main className="portal-page">
        <h1>Business Intelligence</h1>
        <p>Ask about your business data</p>
        <form className="copilot-form" onSubmit={submit}>
          <label>
            Data source
            <select
              value={sourceId}
              onChange={(e) => setSourceId(e.target.value)}
            >
              <option value="">SupportAgent Demo</option>
              {sources.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </select>
          </label>
          {sourceId && scopes.length > 0 && (
            <label>
              Data scope
              <select
                value={scopeId}
                onChange={(e) => setScopeId(e.target.value)}
              >
                <option value="">All approved entities</option>
                {scopes.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </select>
            </label>
          )}
          <textarea
            rows={3}
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder="Which customers have outstanding balances?"
          />
          <button disabled={loading}>{loading ? "Analyzing..." : "Ask"}</button>
        </form>
        <div className="bi-examples">
          {[
            "Show overdue invoices",
            "Which products are low in stock?",
            "Show sales by customer",
          ].map((x) => (
            <button type="button" key={x} onClick={() => setQuestion(x)}>
              {x}
            </button>
          ))}
        </div>
        {error && <p className="panel-error">{error}</p>}
        {result && (
          <section className="card bi-result">
            <h2>Answer</h2>
            <p className="bi-answer">{result.answer}</p>
            <h2>Results</h2>
            {result.result.rowCount === 0 ? (
              <p>No matching records were found.</p>
            ) : (
              <div className="table-scroll">
                <table>
                  <thead>
                    <tr>
                      {result.result.columns.map((column) => {
                        const m = result.displayColumns.find(
                          (x) => x.name === column,
                        );
                        return (
                          <th
                            key={column}
                            className={
                              m?.dataType === "Money" ||
                              m?.dataType === "Number"
                                ? "numeric"
                                : ""
                            }
                          >
                            {m?.displayName ?? column}
                          </th>
                        );
                      })}
                    </tr>
                  </thead>
                  <tbody>
                    {result.result.rows.map((row, index) => (
                      <tr key={index}>
                        {result.result.columns.map((column) => {
                          const m = result.displayColumns.find(
                            (x) => x.name === column,
                          );
                          return (
                            <td
                              key={column}
                              className={
                                m?.dataType === "Money" ||
                                m?.dataType === "Number"
                                  ? "numeric"
                                  : ""
                              }
                            >
                              {formatBusinessValue(row[column], m)}
                            </td>
                          );
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <details>
              <summary>Query details</summary>
              <dl>
                <dt>Data source</dt>
                <dd>{result.dataSourceName || "SupportAgent Demo"}</dd>
                <dt>Scope</dt>
                <dd>{result.scopeName || "All approved entities"}</dd>
                <dt>Base entity</dt>
                <dd>{result.plan.entity}</dd>
                <dt>Selected entities</dt>
                <dd>
                  {result.selectedEntities?.join(", ") || result.plan.entity}
                </dd>
                <dt>Relationships</dt>
                <dd>{result.relationships?.join("; ") || "None"}</dd>
                <dt>Selected fields</dt>
                <dd>{result.plan.select.join(", ") || "Aggregated result"}</dd>
                <dt>Filters</dt>
                <dd>
                  {result.plan.filters
                    .map(
                      (x) =>
                        `${x.field} ${x.operator} ${JSON.stringify(x.value)}`,
                    )
                    .join("; ") || "None"}
                </dd>
                <dt>Sort</dt>
                <dd>
                  {result.plan.sort
                    .map((x) => `${x.field} ${x.direction}`)
                    .join("; ") || "None"}
                </dd>
                <dt>Limit</dt>
                <dd>{result.plan.limit}</dd>
                <dt>Aggregations</dt>
                <dd>
                  {result.plan.aggregations
                    .map(
                      (x) => `${x.function}(${x.field ?? "*"}) as ${x.alias}`,
                    )
                    .join("; ") || "None"}
                </dd>
                <dt>Rows</dt>
                <dd>
                  {result.result.rowCount}
                  {result.result.truncated ? " (limited)" : ""}
                </dd>
                <dt>Duration</dt>
                <dd>{result.result.durationMs} ms</dd>
              </dl>
            </details>
          </section>
        )}
      </main>
    </>
  );
}
