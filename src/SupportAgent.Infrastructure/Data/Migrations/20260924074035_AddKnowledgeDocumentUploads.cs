using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportAgent.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "KnowledgeDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "KnowledgeDocuments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "KnowledgeDocuments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "KnowledgeDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Completed");

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "KnowledgeDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByUserId",
                table: "KnowledgeDocuments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "KnowledgeDocuments");
        }
    }
}
