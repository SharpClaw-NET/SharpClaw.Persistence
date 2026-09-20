# SharpClaw.Persistence

SharpClaw.Persistence contains the official storage modules for SharpClaw 0.5.0. Storage backends are discovered through the same module system as every other capability: the host selects a provider key, an installed module contributes the EF Core provider configuration, and each relational module carries and owns its own migrations. The repository is licensed under the GNU Affero General Public License v3.0 or later.

## Packages

| Package | `Database:Provider` | Compatibility alias | Migration owner |
|---|---|---|---|
| `SharpClaw.Persistence.JSONColdStore` | `JSONColdStore` | `JsonFile` | Schema-free; calls `EnsureCreated` |
| `SharpClaw.Persistence.PostgreSQL` | `PostgreSQL` | `Postgres` | PostgreSQL module assembly |
| `SharpClaw.Persistence.SQLServer` | `SQLServer` | `SqlServer` | SQL Server module assembly |
| `SharpClaw.Persistence.SQLite` | `SQLite` | — | SQLite module assembly |
| `SharpClaw.Persistence` | — | — | Provider-neutral contracts and the canonical model |

## Configuration

Select one installed module through `Database:Provider`; relational modules resolve a connection string using the configured key, canonical key, or compatibility alias. Existing `JsonFile`, `Postgres`, and `SqlServer` configurations therefore continue to work.

```json
{
  "Database": {
    "Provider": "PostgreSQL",
    "Relational": {
      "EnableRetryOnFailure": true
    }
  },
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=sharpclaw;Username=sharpclaw;Password=change-me"
  }
}
```

## Migrations

Migration files belong to the relational provider project that applies them and must always be generated with the EF Core CLI. Do not author or edit migration files or model snapshots by hand. From this repository, use one of the following commands after installing the pinned `dotnet-ef` tool:

```bash
dotnet ef migrations add <Name> --project SharpClaw.Persistence.PostgreSQL --startup-project SharpClaw.Persistence.PostgreSQL --context SharpClawDbContext --output-dir Migrations
dotnet ef migrations add <Name> --project SharpClaw.Persistence.SQLServer --startup-project SharpClaw.Persistence.SQLServer --context SharpClawDbContext --output-dir Migrations
dotnet ef migrations add <Name> --project SharpClaw.Persistence.SQLite --startup-project SharpClaw.Persistence.SQLite --context SharpClawDbContext --output-dir Migrations
```

The initial migrations retain the IDs originally generated in SharpClaw so an already-migrated database is not made to replay the same schema under a new identifier.

## Publishing

The release workflow builds one immutable package set and publishes those exact `.nupkg` files to both NuGet.org and GitHub Packages. A version can be supplied through `workflow_dispatch`, while a `v*` tag uses the tag name without the leading `v`.
