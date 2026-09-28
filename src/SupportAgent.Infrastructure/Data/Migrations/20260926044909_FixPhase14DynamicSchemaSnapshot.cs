using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportAgent.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPhase14DynamicSchemaSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsValid",
                table: "ExternalEntityMappings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationMessage",
                table: "ExternalEntityMappings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalDataScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalDataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalDataScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalDataScopes_ExternalDataSources_ExternalDataSourceId",
                        column: x => x.ExternalDataSourceId,
                        principalTable: "ExternalDataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalDataScopes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalDataScopeEntities",
                columns: table => new
                {
                    ExternalDataScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalEntityMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalDataScopeEntities", x => new { x.ExternalDataScopeId, x.ExternalEntityMappingId });
                    table.ForeignKey(
                        name: "FK_ExternalDataScopeEntities_ExternalDataScopes_ExternalDataScopeId",
                        column: x => x.ExternalDataScopeId,
                        principalTable: "ExternalDataScopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalDataScopeEntities_ExternalEntityMappings_ExternalEntityMappingId",
                        column: x => x.ExternalEntityMappingId,
                        principalTable: "ExternalEntityMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDataScopeEntities_ExternalEntityMappingId",
                table: "ExternalDataScopeEntities",
                column: "ExternalEntityMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDataScopes_ExternalDataSourceId",
                table: "ExternalDataScopes",
                column: "ExternalDataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDataScopes_OrganizationId",
                table: "ExternalDataScopes",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDataScopes_OrganizationId_ExternalDataSourceId_Name",
                table: "ExternalDataScopes",
                columns: new[] { "OrganizationId", "ExternalDataSourceId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalDataScopeEntities");

            migrationBuilder.DropTable(
                name: "ExternalDataScopes");

            migrationBuilder.DropColumn(
                name: "IsValid",
                table: "ExternalEntityMappings");

            migrationBuilder.DropColumn(
                name: "ValidationMessage",
                table: "ExternalEntityMappings");
        }
    }
}
