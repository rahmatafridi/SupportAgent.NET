import { useEffect, useState, type FormEvent } from "react";
import { AppHeader } from "../components/AppHeader";
import * as api from "../services/externalDataSourceService";

export function AdminDataSourcesPage() {
  const [sources, setSources] = useState<api.Source[]>([]),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [status, setStatus] = useState(""),
    [objects, setObjects] = useState<api.DbObject[]>([]),
    [active, setActive] = useState(""),
    [name, setName] = useState(""),
    [secretKey, setSecretKey] = useState(""),
    [search, setSearch] = useState(""),
    [type, setType] = useState("All");
  const load = () =>
    api
      .listSources()
      .then(setSources)
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  useEffect(() => {
    void load();
  }, []);
  async function discover(id: string) {
    setStatus("Discovering schema...");
    setError("");
    try {
      const [discovered, mappings] = await Promise.all([
        api.discoverSource(id),
        api.listMappings(id),
      ]);
      setObjects(
        discovered.map((object) => ({
          ...object,
          mapping: mappings.find(
            (mapping) =>
              mapping.externalSchemaName === object.schema &&
              mapping.externalTableName === object.name,
          ),
        })),
      );
      setActive(id);
      setStatus(
        "Schema discovered. Existing approvals were restored; new objects remain unapproved.",
      );
    } catch (e) {
      setError((e as Error).message);
      setStatus("");
    }
  }
  const visible = objects
    .filter(
      (o) =>
        `${o.schema}.${o.name}`.toLowerCase().includes(search.toLowerCase()) &&
        (type === "All" ||
          (type === "Tables" && o.objectType === "BASE TABLE") ||
          (type === "Views" && o.objectType === "VIEW")),
    )
    .sort((a, b) =>
      `${a.schema}.${a.name}`.localeCompare(`${b.schema}.${b.name}`),
    );
  return (
    <>
      <AppHeader />
      <main className="portal-page">
        <h1>External data sources</h1>
        <p>
          Connection strings stay in environment variables or User Secrets. Only
          the secret reference is stored.
        </p>
        <section>
          <h2>Connection</h2>
          <form
            className="portal-form"
            onSubmit={async (e) => {
              e.preventDefault();
              setError("");
              try {
                await api.createSource({
                  name,
                  secretKey,
                  description: "",
                  enabled: true,
                });
                setName("");
                setSecretKey("");
                await load();
                setStatus("Data source added.");
              } catch (x) {
                setError((x as Error).message);
              }
            }}
          >
            <label>
              Name
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                maxLength={200}
                required
              />
            </label>
            <label>
              Provider
              <input value="SQL Server / Azure SQL" disabled />
            </label>
            <label>
              Secret key
              <input
                value={secretKey}
                onChange={(e) => setSecretKey(e.target.value)}
                placeholder="ERP_MAIN_DB"
                maxLength={200}
                pattern="[A-Za-z0-9_]+"
                required
              />
              <small>
                Enter only a configuration reference such as ERP_MAIN_DB. Store
                the actual connection string in User Secrets or an environment
                variable.
              </small>
            </label>
            <button>Add data source</button>
          </form>
        </section>
        {loading ? (
          <p>Loading data sources...</p>
        ) : sources.length === 0 ? (
          <p>No external data sources configured.</p>
        ) : (
          <div className="portal-ticket-list">
            {sources.map((s) => (
              <article className="card" key={s.id}>
                <strong>{s.name}</strong>
                <p>
                  {s.provider} · {s.enabled ? "Enabled" : "Disabled"} ·{" "}
                  {s.approvedEntities} approved entities
                </p>
                <button
                  onClick={async () => {
                    setStatus("Testing connection...");
                    try {
                      const r = await api.testSource(s.id);
                      setStatus(
                        r.success
                          ? `Connected to ${r.databaseName} in ${r.durationMs} ms`
                          : (r.error ?? "Connection failed."),
                      );
                    } catch (e) {
                      setStatus("");
                      setError((e as Error).message);
                    }
                  }}
                >
                  Test connection
                </button>{" "}
                <button onClick={() => discover(s.id)}>
                  Discover / refresh schema
                </button>{" "}
                <button
                  onClick={async () => {
                    await api.setSourceEnabled(s.id, !s.enabled);
                    await load();
                  }}
                >
                  {s.enabled ? "Disable" : "Enable"}
                </button>
              </article>
            ))}
          </div>
        )}
        {status && <p className="panel-success">{status}</p>}
        {error && <p className="panel-error">{error}</p>}
        {active && (
          <>
            <section className="card">
              <h2>Discovered schema</h2>
              <label>
                Search
                <input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Schema, table, or view"
                />
              </label>
              <label>
                Type
                <select value={type} onChange={(e) => setType(e.target.value)}>
                  <option>All</option>
                  <option>Tables</option>
                  <option>Views</option>
                </select>
              </label>
              <p>
                Discovery does not approve objects or columns. Configure only
                the data AI may query.
              </p>
            </section>
            {visible.map((o) => (
              <Mapping
                key={`${o.schema}.${o.name}`}
                sourceId={active}
                object={o}
                onSaved={() =>
                  setStatus(`${o.schema}.${o.name} mapping saved.`)
                }
              />
            ))}
            <RelationshipEditor
              sourceId={active}
              objects={objects}
              onSaved={() => setStatus("Relationship approved.")}
            />
            <ScopeEditor
              sourceId={active}
              onSaved={() => setStatus("Data scope saved.")}
            />
          </>
        )}
      </main>
    </>
  );
}

function RelationshipEditor({
  sourceId,
  objects,
  onSaved,
}: {
  sourceId: string;
  objects: api.DbObject[];
  onSaved: () => void;
}) {
  const [mappings, setMappings] = useState<api.Mapping[]>([]),
    [relationships, setRelationships] = useState<api.Relationship[]>([]),
    [name, setName] = useState(""),
    [description, setDescription] = useState(""),
    [concepts, setConcepts] = useState(""),
    [relationshipType, setRelationshipType] = useState("ManyToOne"),
    [fromEntity, setFromEntity] = useState(""),
    [fromField, setFromField] = useState(""),
    [toEntity, setToEntity] = useState(""),
    [toField, setToField] = useState(""),
    [error, setError] = useState("");
  const load = () =>
    Promise.all([
      api.listMappings(sourceId),
      api.listRelationships(sourceId),
    ]).then(([m, r]) => {
      setMappings(m);
      setRelationships(r);
    });
  useEffect(() => {
    void load().catch((e) => setError(e.message));
  }, [sourceId]);
  const approved = mappings.filter((x) => x.isEnabled && x.isValid),
    from = approved.find((x) => x.businessEntityName === fromEntity),
    to = approved.find((x) => x.businessEntityName === toEntity);
  const candidates = objects
    .flatMap((o) => o.relationships ?? [])
    .map((fk) => {
      const fm = approved.find(
          (m) =>
            m.externalSchemaName === fk.fromSchema &&
            m.externalTableName === fk.fromObject,
        ),
        tm = approved.find(
          (m) =>
            m.externalSchemaName === fk.toSchema &&
            m.externalTableName === fk.toObject,
        ),
        ff = fm?.fields.find(
          (f) =>
            f.externalColumnName === fk.fromColumn &&
            f.isQueryable &&
            !f.isSensitive,
        ),
        tf = tm?.fields.find(
          (f) =>
            f.externalColumnName === fk.toColumn &&
            f.isQueryable &&
            !f.isSensitive,
        );
      return { fk, fm, tm, ff, tf, ready: !!fm && !!tm && !!ff && !!tf };
    });
  const save = async (body: Parameters<typeof api.saveRelationship>[1]) => {
    setError("");
    try {
      await api.saveRelationship(sourceId, body);
      await load();
      onSaved();
    } catch (e) {
      setError((e as Error).message);
    }
  };
  const suggestedName = (sourceField: string, targetEntity: string) => {
    const semantic = sourceField
      .replace(/_id$/i, "")
      .replace(/Id$/i, "")
      .replace(/[^A-Za-z0-9_]/g, "");
    return (
      semantic || targetEntity.replace(/[^A-Za-z0-9_]/g, "") || "RelatedEntity"
    );
  };
  return (
    <section className="card">
      <h2>Relationships</h2>
      <p>
        Approve discovered foreign keys or define a manual relationship using
        approved logical fields. Physical join SQL is never accepted.
      </p>
      <h3>Discovered relationships</h3>
      {candidates.length === 0 ? (
        <p>No foreign keys were discovered.</p>
      ) : (
        candidates.map(({ fk, fm, tm, ff, tf, ready }) => (
          <article
            className="card"
            key={`${fk.name}:${fk.fromSchema}.${fk.fromObject}.${fk.fromColumn}`}
          >
            <strong>{fk.name}</strong>
            <p>
              {fm?.businessEntityName ?? `${fk.fromSchema}.${fk.fromObject}`}.
              {ff?.businessFieldName ?? fk.fromColumn} →{" "}
              {tm?.businessEntityName ?? `${fk.toSchema}.${fk.toObject}`}.
              {tf?.businessFieldName ?? fk.toColumn}
            </p>
            <small>
              Physical: {fk.fromSchema}.{fk.fromObject}.{fk.fromColumn} →{" "}
              {fk.toSchema}.{fk.toObject}.{fk.toColumn}
            </small>
            <div>
              <button
                disabled={
                  !ready ||
                  relationships.some(
                    (r) =>
                      r.discoveredForeignKeyName === fk.name &&
                      r.fromField === ff?.businessFieldName,
                  )
                }
                onClick={() =>
                  void save({
                    name: suggestedName(
                      ff!.businessFieldName,
                      tm!.businessEntityName,
                    ),
                    description: `${suggestedName(ff!.businessFieldName, tm!.businessEntityName)} related ${tm!.displayName || tm!.businessEntityName}`,
                    concepts: [ff!.concepts, tm!.concepts]
                      .filter(Boolean)
                      .join(", "),
                    relationshipType: "ManyToOne",
                    discoveredForeignKeyName: fk.name,
                    fromEntity: fm!.businessEntityName,
                    fromField: ff!.businessFieldName,
                    toEntity: tm!.businessEntityName,
                    toField: tf!.businessFieldName,
                    enabled: true,
                  })
                }
              >
                {ready
                  ? "Approve relationship"
                  : "Approve both entities and fields first"}
              </button>
            </div>
          </article>
        ))
      )}
      <h3>Manual relationship</h3>
      <label>
        Relationship name
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Owner"
        />
      </label>
      <label>
        Description
        <input
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </label>
      <label>
        Concepts
        <input
          value={concepts}
          onChange={(e) => setConcepts(e.target.value)}
          placeholder="owner, customer, person"
        />
      </label>
      <label>
        Type
        <select
          value={relationshipType}
          onChange={(e) => setRelationshipType(e.target.value)}
        >
          <option>ManyToOne</option>
          <option>OneToMany</option>
          <option>OneToOne</option>
        </select>
      </label>
      <label>
        Source entity
        <select
          value={fromEntity}
          onChange={(e) => {
            setFromEntity(e.target.value);
            setFromField("");
          }}
        >
          <option value="">Select</option>
          {approved.map((x) => (
            <option key={x.id}>{x.businessEntityName}</option>
          ))}
        </select>
      </label>
      <label>
        Source field
        <select
          value={fromField}
          onChange={(e) => setFromField(e.target.value)}
        >
          <option value="">Select</option>
          {from?.fields
            .filter((x) => x.isQueryable && !x.isSensitive)
            .map((x) => (
              <option key={x.businessFieldName}>{x.businessFieldName}</option>
            ))}
        </select>
      </label>
      <label>
        Target entity
        <select
          value={toEntity}
          onChange={(e) => {
            setToEntity(e.target.value);
            setToField("");
          }}
        >
          <option value="">Select</option>
          {approved.map((x) => (
            <option key={x.id}>{x.businessEntityName}</option>
          ))}
        </select>
      </label>
      <label>
        Target field
        <select value={toField} onChange={(e) => setToField(e.target.value)}>
          <option value="">Select</option>
          {to?.fields
            .filter((x) => x.isQueryable && !x.isSensitive)
            .map((x) => (
              <option key={x.businessFieldName}>{x.businessFieldName}</option>
            ))}
        </select>
      </label>
      <button
        disabled={!name || !fromField || !toField}
        onClick={() =>
          void save({
            name,
            description,
            concepts,
            relationshipType,
            fromEntity,
            fromField,
            toEntity,
            toField,
            enabled: true,
          })
        }
      >
        Approve manual relationship
      </button>
      <h3>Approved relationships</h3>
      {relationships.length === 0 ? (
        <p>No relationships approved.</p>
      ) : (
        relationships.map((r) => (
          <p key={r.id}>
            <strong>{r.name}</strong>: {r.fromEntity}.{r.fromField} →{" "}
            {r.toEntity}.{r.toField} ({r.relationshipType})
          </p>
        ))
      )}
      {error && <p className="panel-error">{error}</p>}
    </section>
  );
}

function ScopeEditor({
  sourceId,
  onSaved,
}: {
  sourceId: string;
  onSaved: () => void;
}) {
  const [mappings, setMappings] = useState<api.Mapping[]>([]),
    [scopes, setScopes] = useState<api.Scope[]>([]),
    [scopeName, setScopeName] = useState(""),
    [description, setDescription] = useState(""),
    [selected, setSelected] = useState(new Set<string>()),
    [scopeError, setScopeError] = useState(""),
    [scopeSuccess, setScopeSuccess] = useState(""),
    [saving, setSaving] = useState(false);
  const load = () =>
    Promise.all([api.listMappings(sourceId), api.listScopes(sourceId)]).then(
      ([m, s]) => {
        setMappings(m);
        setScopes(s);
      },
    );
  useEffect(() => {
    setScopeError("");
    setScopeSuccess("");
    void load().catch((e) => setScopeError(e.message));
  }, [sourceId]);
  async function save(event: FormEvent) {
    event.preventDefault();
    setScopeError("");
    setScopeSuccess("");
    setSaving(true);
    try {
      await api.saveScope(sourceId, {
        name: scopeName,
        description,
        enabled: true,
        entityIds: [...selected],
      });
      await load();
      setScopeName("");
      setDescription("");
      setSelected(new Set());
      setScopeSuccess("Data scope saved successfully.");
      onSaved();
    } catch (e) {
      setScopeSuccess("");
      setScopeError((e as Error).message);
    } finally {
      setSaving(false);
    }
  }
  async function toggle(scope: api.Scope) {
    setScopeError("");
    setScopeSuccess("");
    try {
      await api.setScopeEnabled(sourceId, scope.id, !scope.isEnabled);
      await load();
      setScopeSuccess(
        `Data scope ${scope.isEnabled ? "disabled" : "enabled"} successfully.`,
      );
    } catch (e) {
      setScopeError((e as Error).message);
    }
  }
  return (
    <section className="card">
      <h2>Data scopes</h2>
      <p>
        Optionally group approved entities before relevant-schema selection.
      </p>
      <form className="portal-form" onSubmit={save}>
        <label>
          Name
          <input
            value={scopeName}
            onChange={(e) => setScopeName(e.target.value)}
            placeholder="Sales Intelligence"
            disabled={saving}
          />
        </label>
        <label>
          Description
          <input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            disabled={saving}
          />
        </label>
        {mappings
          .filter((x) => x.isEnabled && x.isValid)
          .map((x) => (
            <label className="mapping-column" key={x.id}>
              <input
                type="checkbox"
                disabled={saving}
                checked={selected.has(x.id)}
                onChange={(e) => {
                  const next = new Set(selected);
                  e.target.checked ? next.add(x.id) : next.delete(x.id);
                  setSelected(next);
                }}
              />{" "}
              {x.displayName || x.businessEntityName}
            </label>
          ))}
        <button type="submit" disabled={saving}>
          {saving ? "Saving..." : "Save data scope"}
        </button>
      </form>
      {scopeSuccess && (
        <p className="panel-success" role="status">
          {scopeSuccess}
        </p>
      )}
      {scopeError && (
        <p className="panel-error" role="alert">
          {scopeError}
        </p>
      )}
      <h3>Saved data scopes</h3>
      {scopes.length === 0 ? (
        <p>No data scopes have been created yet.</p>
      ) : (
        scopes.map((scope) => (
          <article className="card" key={scope.id}>
            <strong>{scope.name}</strong>
            {scope.description && <p>{scope.description}</p>}
            <p>Status: {scope.isEnabled ? "Enabled" : "Disabled"}</p>
            <p>
              Entities ({scope.entityIds.length}):{" "}
              {scope.entityNames.join(", ") || "None"}
            </p>
            <button type="button" onClick={() => void toggle(scope)}>
              {scope.isEnabled ? "Disable" : "Enable"}
            </button>
          </article>
        ))
      )}
    </section>
  );
}

function Mapping({
  sourceId,
  object,
  onSaved,
}: {
  sourceId: string;
  object: api.DbObject;
  onSaved: () => void;
}) {
  const saved = object.mapping;
  const [open, setOpen] = useState(false),
    [selected, setSelected] = useState(
      () =>
        new Set(
          saved?.fields
            .filter((f) => f.isQueryable && !f.isSensitive)
            .map((f) => f.externalColumnName) ?? [],
        ),
    ),
    [error, setError] = useState(""),
    [success, setSuccess] = useState(""),
    [saving, setSaving] = useState(false),
    [logicalName, setLogicalName] = useState(
      saved?.businessEntityName ?? object.name,
    ),
    [display, setDisplay] = useState(
      saved?.displayName ?? object.name.replaceAll("_", " "),
    ),
    [description, setDescription] = useState(saved?.description ?? ""),
    [concepts, setConcepts] = useState(
      saved?.concepts ?? object.name.replaceAll("_", " "),
    ),
    [defaultRecencyField, setDefaultRecencyField] = useState(
      saved?.defaultRecencyField ?? "",
    ),
    [fieldNames, setFieldNames] = useState<Record<string, string>>(() =>
      Object.fromEntries(
        object.columns.map((c) => [
          c.name,
          saved?.fields.find((f) => f.externalColumnName === c.name)
            ?.businessFieldName ?? c.name,
        ]),
      ),
    );
  async function save() {
    setSaving(true);
    setError("");
    setSuccess("");
    try {
      await api.saveMapping(
        sourceId,
        object,
        selected,
        {
          name: logicalName,
          display,
          description,
          concepts,
          defaultRecencyField,
        },
        fieldNames,
      );
      setSuccess(`${object.schema}.${object.name} mapping saved successfully.`);
      onSaved();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  }
  return (
    <section className="card">
      <h3>
        <button
          type="button"
          className="link-button"
          onClick={() => setOpen(!open)}
        >
          {object.schema}.{object.name}
        </button>{" "}
        <small>
          {object.objectType === "VIEW" ? "View" : "Table"} ·{" "}
          {object.columns.length} columns ·{" "}
          {object.approved || success ? "Approved" : "Not approved"}
          {object.mappingValid === false ? " · Needs review" : ""}
        </small>
      </h3>
      {open && (
        <>
          <div className="portal-form">
            <label>
              Logical name
              <input
                value={logicalName}
                onChange={(e) => setLogicalName(e.target.value)}
              />
            </label>
            <label>
              Display name
              <input
                value={display}
                onChange={(e) => setDisplay(e.target.value)}
              />
            </label>
            <label>
              Description
              <input
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </label>
            <label>
              Concepts
              <input
                value={concepts}
                onChange={(e) => setConcepts(e.target.value)}
                placeholder="sales, billing, revenue"
              />
            </label>
          </div>
          <p>
            Select only explicitly approved, non-sensitive columns. Physical
            metadata is read-only.
          </p>
          <label>
            Default date/time field for latest and oldest queries
            <select
              value={defaultRecencyField}
              disabled={saving}
              onChange={(e) => setDefaultRecencyField(e.target.value)}
            >
              <option value="">Not configured</option>
              {object.columns
                .filter(
                  (c) =>
                    selected.has(c.name) &&
                    ["date", "datetime", "datetime2", "smalldatetime"].includes(
                      c.sqlType.toLowerCase(),
                    ),
                )
                .map((c) => (
                  <option key={c.name} value={fieldNames[c.name] || c.name}>
                    {fieldNames[c.name] || c.name}
                  </option>
                ))}
            </select>
          </label>
          {object.columns.map((c) => (
            <div key={c.name} className="mapping-column">
              <label>
                <input
                  type="checkbox"
                  disabled={!c.supported || saving}
                  checked={selected.has(c.name)}
                  onChange={(e) => {
                    const next = new Set(selected);
                    e.target.checked ? next.add(c.name) : next.delete(c.name);
                    setSelected(next);
                    setSuccess("");
                  }}
                />{" "}
                {c.name} · {c.sqlType} · {c.nullable ? "nullable" : "required"}
                {c.primaryKey ? " · PK" : ""}
                {c.foreignKey ? " · FK" : ""}
                {!c.supported ? " · unsupported" : ""}
              </label>
              {selected.has(c.name) && (
                <label>
                  Logical field
                  <input
                    value={fieldNames[c.name]}
                    disabled={saving}
                    onChange={(e) => {
                      setFieldNames({
                        ...fieldNames,
                        [c.name]: e.target.value,
                      });
                      setSuccess("");
                    }}
                  />
                </label>
              )}
            </div>
          ))}
          <button type="button" disabled={saving} onClick={save}>
            {saving ? "Saving..." : "Save approved mapping"}
          </button>
          {success && (
            <p className="panel-success" role="status">
              {success}
            </p>
          )}
          {error && (
            <p className="panel-error" role="alert">
              {error}
            </p>
          )}
        </>
      )}
    </section>
  );
}
