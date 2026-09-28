using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportAgent.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase142TrustedRecencyMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultRecencyField",
                table: "ExternalEntityMappings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultRecencyField",
                table: "ExternalEntityMappings");
        }
    }
}
