using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportAgent.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessQueryingAndExternalDataSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DataSourceId",
                table: "BusinessQueryAudits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceName",
                table: "BusinessQueryAudits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceProvider",
                table: "BusinessQueryAudits",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalDataSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SecretKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalDataSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalDataSources_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalRelationshipMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalDataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromEntity = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FromField = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ToEntity = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ToField = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalRelationshipMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalRelationshipMappings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalEntityMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalDataSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalSchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExternalTableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ObjectType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BusinessEntityName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Concepts = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalEntityMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalEntityMappings_ExternalDataSources_ExternalDataSourceId",
                        column: x => x.ExternalDataSourceId,
                        principalTable: "ExternalDataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalEntityMappings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalFieldMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalEntityMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalColumnName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BusinessFieldName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Concepts = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsQueryable = table.Column<bool>(type: "bit", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalFieldMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalFieldMappings_ExternalEntityMappings_ExternalEntityMappingId",
                        column: x => x.ExternalEntityMappingId,
                        principalTable: "ExternalEntityMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalFieldMappings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDataSources_OrganizationId",
                table: "ExternalDataSources",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDataSources_OrganizationId_Name",
                table: "ExternalDataSources",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalEntityMappings_ExternalDataSourceId",
                table: "ExternalEntityMappings",
                column: "ExternalDataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalEntityMappings_OrganizationId",
                table: "ExternalEntityMappings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFieldMappings_ExternalEntityMappingId",
                table: "ExternalFieldMappings",
                column: "ExternalEntityMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFieldMappings_OrganizationId",
                table: "ExternalFieldMappings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalRelationshipMappings_OrganizationId",
                table: "ExternalRelationshipMappings",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalFieldMappings");

            migrationBuilder.DropTable(
                name: "ExternalRelationshipMappings");

            migrationBuilder.DropTable(
                name: "ExternalEntityMappings");

            migrationBuilder.DropTable(
                name: "ExternalDataSources");

            migrationBuilder.DropColumn(
                name: "DataSourceId",
                table: "BusinessQueryAudits");

            migrationBuilder.DropColumn(
                name: "DataSourceName",
                table: "BusinessQueryAudits");

            migrationBuilder.DropColumn(
                name: "DataSourceProvider",
                table: "BusinessQueryAudits");
        }
    }
}
