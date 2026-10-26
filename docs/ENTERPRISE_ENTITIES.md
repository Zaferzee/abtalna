# Hierarchical entity administration (enterprise v1)

One Abtalna installation, one PostgreSQL database, one set of tables. Organizational **entities** form a tree
(ministry → agency → directorate → department → …). The central (root) administrator governs everything; entity
administrators receive only explicitly delegated capabilities, and only inside the entity subtrees they are assigned to.

## 1. Implementation plan (written before coding)

### Current architecture (inspected)
- ASP.NET Core 10 MVC modular monolith, EF Core + PostgreSQL, cookie auth (local and/or Windows/IIS), one `Admin` role
  (`[Authorize(Policy="Admin")]` on the whole Admin area), `User` role for employees.
- No organizational concept: every employee sees every published content item/assessment; every admin sees everything.
- Data access is direct `AppDbContext` use in controllers plus a few services (`ReportService`, `ContentAuthoring`,
  `LearningProgress`, `AssessmentQueries`, `AckGate`). Branding is key/value `SystemSettings` (`Branding.*`).

### Affected areas
| Area | Change |
|---|---|
| Domain / DbContext | new `OrgEntity` (table `Entities`), `EntityAdminAssignment`, `ContentEntityTarget`, `EntitySetting`, `EntityDirectorySource`; new columns `Users.EntityId`, `Contents.OwnerEntityId`, `Contents.TargetScope`, `Assessments.OwnerEntityId`, `AuditLogs.ActorEntityId`, `AuditLogs.ScopeEntityId` |
| Services | new `EntityAccess` (per-request scope/capability resolver), `EntityVisibility` (content/assessment targeting expressions), `EntityTree`, `EntityBranding` (inheritance), `DirectorySecrets` (data-protection) |
| Authorization | `AdminArea` policy (central admin **or** active assignment), `[Capability(..)]` action filter, record-level checks through `EntityAccess` (out-of-scope records answer 404) |
| Controllers | every Admin controller scoped; employee controllers (`Home`, `Content`, `Assessments`, `MyResults`, `Files`) use targeting; new `EntitiesController`, `EntityBrandingController` |
| Reports | entity filter + subtree drill-down, entity comparison summary; exports reuse the same scoped queries |
| Branding | root settings → overridden per entity (inheritance chain), entity logo; login page stays central (no entity is known before sign-in) |
| Directory | per-entity directory source (domain → entity mapping for provisioning), secrets encrypted with ASP.NET Data Protection, never rendered or logged |

### Migration and backward-compatibility risks (and mitigations)
- **Existing rows need an entity** → migration creates the root entity (`Id` from the identity sequence, code `ROOT`, name
  from the existing `Branding.OrgName` when present) and back-fills `Users.EntityId`, `Contents.OwnerEntityId`,
  `Assessments.OwnerEntityId` before making them `NOT NULL`. Nothing is deleted.
- **Existing content was visible to everyone** → existing content gets `TargetScope = OwnerAndDescendants` from the root,
  so it stays visible to every employee even after sub-entities are created.
- **Existing administrators** → the `Admin` role keeps its meaning and becomes "central administrator" (root scope, all
  capabilities). No existing admin loses access; no employee gains any.
- **Deployments with `AutoMigrate=false`** → `deploy/sql/02-migrate.sql` is regenerated (idempotent).
- **Sessions** → the cookie principal is re-validated every minute (already in place); capability checks read the database
  on every request, so revocation is immediate.

## 2. Data model
```
Entities(Id, Name, Code UNIQUE, ParentEntityId NULL → Entities, Path '/1/5/9/', Depth, IsActive, AllowedCapabilities, CreatedAt, UpdatedAt)
   └─ exactly one root (ParentEntityId IS NULL) – partial unique index
Users.EntityId → Entities (primary organizational scope; NOT NULL)
EntityAdminAssignments(Id, UserId → Users, EntityId → Entities, Capabilities, IncludeDescendants, CreatedAt, CreatedById)  UNIQUE(UserId, EntityId)
Contents.OwnerEntityId → Entities, Contents.TargetScope (0 OwnerOnly | 1 OwnerAndDescendants | 2 Selected)
ContentEntityTargets(ContentId, EntityId, IncludeDescendants)  PK(ContentId, EntityId)   – no content duplication
Assessments.OwnerEntityId → Entities (standalone assessments are visible to the owner subtree; linked ones follow their content)
EntitySettings(EntityId, Key, Value)  PK(EntityId, Key)       – branding overrides (inheritance)
EntityDirectorySources(EntityId PK, Enabled, Domain, LdapPath, SearchBase, BindUser, BindSecretProtected, UpdatedAt, LastValidatedAt, ValidationNote)
AuditLogs.ActorEntityId, AuditLogs.ScopeEntityId
```
The materialized `Path` makes "entity X and all its descendants" a single indexed `LIKE '/1/5/%'` and lets report queries
join users to targeting rules without recursive SQL. Moving an entity rewrites the paths of its subtree in one transaction;
cycles are rejected.
