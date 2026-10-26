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

## 3. Security model

**One mechanism, enforced on the server.** `Services/EntityAccess.cs` is the only place that answers two questions:
- "May the current user do *capability X* in *entity E*?"
- "In which entities may the user do *X*?"

Controllers, services, queries, reports, exports and files all use it. The navigation only mirrors it.

| Layer | What it enforces |
|---|---|
| `AdminArea` policy (`AdminAreaHandler`) | The admin area is open only to the central administrator or a holder of an active assignment. This is checked against the database on every request, so revocation is immediate. |
| `[Capability(X)]` filter | The action requires capability X in at least one entity; otherwise 403. |
| `[CentralOnly]` filter | System settings, central branding and the login page, and entity create, edit, move and disable. |
| Record-level checks | Every record is loaded *through* the scope (`InScope`, `ScopedAsync`, `Visible`). A record outside the scope is answered with **404**, so its existence in another entity is not confirmed. This applies to users, content, attachments, assessments, questions, attempts, entities, branding and directory. |
| Query scope | `ScopeQueries.InScope` / `InSubtree` and `EntityVisibility` expressions are used by lists, reports, exports, reminders, notifications and the dashboard. |
| Employees | `EntityVisibility.Content` / `.Assessment` decide what each employee sees. They are applied to the dashboard, content pages, acknowledgment, reading completion, assessments (start, take, submit) and attachments. |

**Safe by default:**
- **New entities** may only be granted `DefaultAllowed`: no branding, directory or administrator delegation until the central administrator allows it.
- **Domain users** are never made administrators.
- **Delegated administrators** cannot:
  - grant more than they hold themselves (and never more than the entity allows);
  - edit their own assignment;
  - assign users outside their scope;
  - change accounts that hold administrative rights (no takeover by password reset);
  - create central administrators;
  - touch centrally owned content.
- **Disabling an entity** blocks sign-in for its whole subtree (checked at sign-in and at the cookie re-validation every minute) and suspends its administrators.
- **Directory secrets** are encrypted with ASP.NET Data Protection (DPAPI-protected keys on Windows). They are never rendered, logged or written to the audit log.
- **Entity branding:**
  - Images of an entity are served only to signed-in users of that subtree and to administrators who have that entity in scope.
  - `theme.css` for a signed-in user is computed from *their own* entity chain, so an entity can never affect another entity's branding.

**Audit.** Every audit row records:
- the actor (user name and id);
- the actor's entity (`ActorEntityId`);
- the affected entity (`ScopeEntityId`);
- the action, a timestamp, the IP address and meaningful details.

New actions:
- Entities: `ENTITY_CREATED`, `ENTITY_UPDATED`, `ENTITY_MOVED`, `ENTITY_ENABLED`, `ENTITY_DISABLED`, `ENTITY_CAPABILITIES_CHANGED`.
- Administrators: `ADMIN_ASSIGNED`, `ADMIN_PERMISSIONS_CHANGED`, `ADMIN_REVOKED`.
- Branding and directory: `ENTITY_BRANDING_UPDATED`, `ENTITY_BRANDING_RESET`, `DIRECTORY_CONFIG_CHANGED`.
- Content and users: `CONTENT_TARGETING_CHANGED`, `USER_ENTITY_CHANGED`.

Existing content, user and report actions now also carry the affected entity. The central administrator sees the complete history, filterable by entity subtree. An entity administrator with `ViewAudit` sees only rows whose affected entity is in scope.

## 4. Permission matrix

**C** = central administrator (Admin role; every entity). **E** = entity administrator holding the capability, inside the assigned entity (and its descendants when the assignment includes them), capped by the entity's `AllowedCapabilities`.

| Capability | What it opens | C | E |
|---|---|:-:|:-:|
| — (any assignment) | Administration area, dashboard (figures limited to the scope), "My entities" (read-only view of the entities in scope) | ✔ | ✔ |
| `ManageUsers` | Users list, create, edit, password reset and CSV import, for employees of entities in scope. Accounts with administrative rights: central only. | ✔ | ✔ |
| `ManageContent` | Content list, authoring wizard and classic form, attachments, copies. Content must be *owned* by an entity in scope; the audience can only target entities in scope. | ✔ | ✔ |
| `PublishContent` | Publish and unpublish content and assessments owned in scope | ✔ | ✔ |
| `ManageAssessments` | Assessments, questions and the wizard's assessment step, for assessments owned in scope | ✔ | ✔ |
| `ManageAcknowledgments` | Reset acknowledgments (content owned in scope; only employees in scope) | ✔ | ✔ |
| `ViewReports` | Assessment, attempt and acknowledgment reports and the entity comparison, limited to employees in scope | ✔ | ✔ |
| `ExportReports` | CSV/Excel exports, limited to the export scope | ✔ | ✔ |
| `ManageNotifications` | Content and assessment e-mails and report reminders: audience ∩ scope | ✔ | ✔ |
| `ManageBranding` | Entity branding page (overrides) for entities in scope | ✔ | ✔ only if allowed for the entity |
| `ManageDirectory` | Directory source of entities in scope | ✔ | ✔ only if allowed for the entity |
| `ManageAdmins` | Assign, change or revoke administrators in scope, with a subset of own capabilities | ✔ | ✔ only if allowed for the entity |
| `ViewAudit` | Audit log rows whose affected entity is in scope | ✔ | ✔ |
| Central only | System settings (general, SMTP), central branding and the login page, entity structure (create, edit, move, disable, allowed capabilities) | ✔ | ✖ |

## 5. Content ownership and distribution

- **Owner.** Every content item and assessment has an owning entity.
  - Central content is owned by the root.
  - Content created by an entity administrator is owned by one of their entities, and defaults to "owning entity only".
- **Audience** (`TargetScope`). One content record; targets are rows in `ContentEntityTargets` and are never copies.

  | Scope | Who sees it | For central content |
  |---|---|---|
  | `OwnerOnly` | Employees of the owning entity | "root entity only" |
  | `OwnerAndDescendants` | The owning entity and all its descendants | "all entities" |
  | `Selected` | The listed entities, optionally with their descendants | — |

- **Assessments** linked to content follow the content's owner and audience. Acknowledgment, completion rules and content version stay part of the same lifecycle. A standalone assessment is visible to its owner's subtree.
- **Central content cannot be changed by entity administrators.** It is outside their ManageContent scope and answered with 404.

## 6. Reporting

Every report query is restricted to the employees of the caller's scope (optionally drilled down to an entity subtree). A user/item pair exists only when the item is targeted to the user's entity — the same rule as the employee portal. **Entity comparison** (Reports → Entity comparison, and the entity's Reports tab) shows per entity, aggregating its subtree:
- employees;
- **completion rate**: acknowledgments given plus assessments passed, divided by all acknowledgment and assessment requirements;
- **acknowledgment rate**;
- **pass rate**;
- passed, failed and not-attempted counts.

You can drill down level by level. Counts are grouped by user entity in SQL and rolled up along the materialized paths.

## 7. Branding inheritance

- **Root (central) branding** is the existing branding studio (`SystemSettings`).
- **Entity overrides** live in `EntitySettings`. An entity may override:
  - identity: organization and system name, welcome and footer text, sidebar flags;
  - the sidebar logo and the compact icon;
  - the application colors (presets available).
- **Resolution** walks the entity's ancestor chain, nearest override first, then falls back to the root. Nothing is copied, so central changes reach every entity that did not override the key.
- **The login page stays central**, because no entity is known before sign-in.
- **Permissions.** The central administrator can always edit an entity's branding. An entity administrator can only when the entity allows `ManageBranding` *and* it was granted. Otherwise the entity inherits centrally controlled branding.

## 8. Directory sources (preparation for the Windows Server phase)

- **Fields.** Each entity can have an `EntityDirectorySource`:
  - NetBIOS domain;
  - LDAP path and search base;
  - service account;
  - encrypted password;
  - validation note.
- **Effect today: mapping only.** A new Windows account from an enabled domain is created in that entity (`AuthService.ResolveWindowsAsync`). Without a mapping it goes to the root. Such an account is never an administrator.
- **Authentication is unchanged.** Windows Integrated Authentication (IIS / Kerberos / NTLM) and `Authentication:Windows:AllowedDomains` remain the sign-in mechanism. Per-entity configuration does **not** imply that sign-in works across unrelated domains or forests; that depends on domain trusts and IIS and must be validated on the Windows Server.
- **Pending.** Connecting with the service account (LDAP bind, synchronization, attribute import) is designed for — the secret is stored encrypted and `DirectorySecrets.Unprotect` exists for the connector — but is not implemented. It belongs to the Windows Server acceptance phase. The status shown is "pending validation".

## 9. UI

- **Admin → Organization** ("My entities" for entity administrators):
  - tree and table with code, parent, status, users (own / with sub-entities), administrators, branding status and directory status;
  - entity pages with the tabs Overview, Administrators & permissions, Users, Branding, Directory integration, Content, Reports and Audit.
- **Entity administrators use the same administration area.** It is automatically scoped: a scope banner on the dashboard, a menu reduced to their capabilities, entity filters on users, content, reports and audit, and an owner/audience panel in the authoring wizard.

## 10. Known limitations

- **One primary entity per user.** The model keeps room for more memberships later (a separate membership table), but they are not implemented.
- **No per-assessment audience for linked assessments.** They follow their content.
- **Moving an entity** keeps its users, content and assignments. Content targeted to "owner and descendants" or "selected + descendants" follows the new position automatically.
- **The entity comparison** is computed live and is not cached. Very large organizations (tens of thousands of employees × items) may need an aggregated table later.
- **The directory connector** (LDAP bind, synchronization) and cross-forest Windows authentication are not part of this release.
- **The login page uses the central branding and composition for everyone.**

## 11. Tests

| Test class | Covers |
|---|---|
| `EntitiesTests` | Root and backward compatibility (fresh database and a real legacy-schema migration), hierarchy depth, moves and cycles, capability grants, ceiling and revocation, delegation limits, cross-entity isolation with direct URL attempts (users, content, assessments, entities, branding, directory, audit, reports, exports), the five audience types, linked and standalone assessment visibility, entity comparison, branding inheritance, override and permission, directory permission and secret handling, disabling, audit actor and scope, and browser-style form switches |
| `WindowsAuthTests` | Domain → entity mapping without administrator rights |
| `BrandingTests` | Login compositions (presets, safety clamps, legacy mapping) |
| UI (`tests/ui/enterprise-entities-acceptance.mjs`) | Central administrator plus two entity administrators with different capabilities, RTL, phone width, login compositions |
