using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportAgent.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase142ExternalRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Concepts",
                table: "ExternalRelationshipMappings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ExternalRelationshipMappings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DiscoveredForeignKeyName",
                table: "ExternalRelationshipMappings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ExternalRelationshipMappings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RelationshipType",
                table: "ExternalRelationshipMappings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ManyToOne");

            migrationBuilder.Sql("""
                UPDATE [ExternalRelationshipMappings]
                SET [Name] = LEFT(CONCAT([ToEntity], '_', REPLACE(CONVERT(nvarchar(36), [Id]), '-', '')), 128)
                WHERE [Name] = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRelationshipMappings_OrganizationId_ExternalDataSourceId_FromEntity_Name",
                table: "ExternalRelationshipMappings",
                columns: new[] { "OrganizationId", "ExternalDataSourceId", "FromEntity", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalRelationshipMappings_OrganizationId_ExternalDataSourceId_FromEntity_Name",
                table: "ExternalRelationshipMappings");

            migrationBuilder.DropColumn(
                name: "Concepts",
                table: "ExternalRelationshipMappings");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ExternalRelationshipMappings");

            migrationBuilder.DropColumn(
                name: "DiscoveredForeignKeyName",
                table: "ExternalRelationshipMappings");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ExternalRelationshipMappings");

            migrationBuilder.DropColumn(
                name: "RelationshipType",
                table: "ExternalRelationshipMappings");
        }
    }
}
