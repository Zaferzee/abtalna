using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyberLms.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OwnerEntityId",
                table: "Contents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetScope",
                table: "Contents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ActorEntityId",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeEntityId",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerEntityId",
                table: "Assessments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Entities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentEntityId = table.Column<int>(type: "integer", nullable: true),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedCapabilities = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entities", x => x.Id);
                    table.CheckConstraint("ck_entity_not_own_parent", "\"ParentEntityId\" IS NULL OR \"ParentEntityId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_Entities_Entities_ParentEntityId",
                        column: x => x.ParentEntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentEntityTargets",
                columns: table => new
                {
                    ContentId = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    IncludeDescendants = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentEntityTargets", x => new { x.ContentId, x.EntityId });
                    table.ForeignKey(
                        name: "FK_ContentEntityTargets_Contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentEntityTargets_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntityAdminAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Capabilities = table.Column<int>(type: "integer", nullable: false),
                    IncludeDescendants = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityAdminAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityAdminAssignments_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntityAdminAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntityDirectorySources",
                columns: table => new
                {
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Domain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LdapPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SearchBase = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BindUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    BindSecretProtected = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidationNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityDirectorySources", x => x.EntityId);
                    table.ForeignKey(
                        name: "FK_EntityDirectorySources_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntitySettings",
                columns: table => new
                {
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntitySettings", x => new { x.EntityId, x.Key });
                    table.ForeignKey(
                        name: "FK_EntitySettings_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntityId",
                table: "Users",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Contents_OwnerEntityId",
                table: "Contents",
                column: "OwnerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ScopeEntityId",
                table: "AuditLogs",
                column: "ScopeEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_OwnerEntityId",
                table: "Assessments",
                column: "OwnerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentEntityTargets_EntityId",
                table: "ContentEntityTargets",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Entities_Code",
                table: "Entities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entities_ParentEntityId",
                table: "Entities",
                column: "ParentEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Entities_Path",
                table: "Entities",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_EntityAdminAssignments_EntityId",
                table: "EntityAdminAssignments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityAdminAssignments_UserId_EntityId",
                table: "EntityAdminAssignments",
                columns: new[] { "UserId", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntityDirectorySources_Domain",
                table: "EntityDirectorySources",
                column: "Domain");

            // Exactly one root entity. (A unique index on "ParentEntityId" alone would not work: NULLs are distinct.)
            migrationBuilder.Sql("""CREATE UNIQUE INDEX "IX_Entities_SingleRoot" ON "Entities" ((true)) WHERE "ParentEntityId" IS NULL;""");

            // Backward compatibility: one root entity for the existing single organization; every existing user, content item and
            // assessment belongs to it. Existing content stays visible to everyone (root + all future descendants). Nothing is deleted.
            migrationBuilder.Sql("""
                INSERT INTO "Entities" ("Name", "Code", "ParentEntityId", "Path", "Depth", "IsActive", "AllowedCapabilities", "CreatedAt")
                SELECT left(COALESCE(NULLIF(trim(regexp_replace((SELECT "Value" FROM "SystemSettings" WHERE "Key" = 'Branding.OrgName'), '\s+', ' ', 'g')), ''), 'الجهة الرئيسية'), 300),
                       'ROOT', NULL, '', 0, true, 4095, now()
                WHERE NOT EXISTS (SELECT 1 FROM "Entities" WHERE "ParentEntityId" IS NULL);
                UPDATE "Entities" SET "Path" = '/' || "Id" || '/' WHERE "ParentEntityId" IS NULL;
                UPDATE "Users" SET "EntityId" = (SELECT "Id" FROM "Entities" WHERE "ParentEntityId" IS NULL) WHERE "EntityId" = 0;
                UPDATE "Contents" SET "OwnerEntityId" = (SELECT "Id" FROM "Entities" WHERE "ParentEntityId" IS NULL), "TargetScope" = 1 WHERE "OwnerEntityId" = 0;
                UPDATE "Assessments" SET "OwnerEntityId" = (SELECT "Id" FROM "Entities" WHERE "ParentEntityId" IS NULL) WHERE "OwnerEntityId" = 0;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_Entities_OwnerEntityId",
                table: "Assessments",
                column: "OwnerEntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contents_Entities_OwnerEntityId",
                table: "Contents",
                column: "OwnerEntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Entities_EntityId",
                table: "Users",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_Entities_OwnerEntityId",
                table: "Assessments");

            migrationBuilder.DropForeignKey(
                name: "FK_Contents_Entities_OwnerEntityId",
                table: "Contents");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Entities_EntityId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ContentEntityTargets");

            migrationBuilder.DropTable(
                name: "EntityAdminAssignments");

            migrationBuilder.DropTable(
                name: "EntityDirectorySources");

            migrationBuilder.DropTable(
                name: "EntitySettings");

            migrationBuilder.DropTable(
                name: "Entities");

            migrationBuilder.DropIndex(
                name: "IX_Users_EntityId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Contents_OwnerEntityId",
                table: "Contents");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ScopeEntityId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_OwnerEntityId",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OwnerEntityId",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "TargetScope",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "ActorEntityId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ScopeEntityId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "OwnerEntityId",
                table: "Assessments");
        }
    }
}
