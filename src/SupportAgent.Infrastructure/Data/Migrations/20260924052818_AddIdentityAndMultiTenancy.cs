using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportAgent.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Email",
                table: "Customers");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TicketMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "KnowledgeDocuments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "KnowledgeChunks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(name: "OrganizationId", table: "AIConversations", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>(name: "OrganizationId", table: "AIConversationMessages", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>(name: "OrganizationId", table: "AIConversationToolAudits", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Organizations",
                columns: new[] { "Id", "Name", "Slug", "IsActive", "CreatedAt" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "SupportAgent Demo", "supportagent-demo", true, new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.Sql("""
                UPDATE Customers SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                UPDATE Orders SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                UPDATE Tickets SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                UPDATE TicketMessages SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                UPDATE KnowledgeDocuments SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                UPDATE KnowledgeChunks SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                UPDATE AIConversations SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                UPDATE AIConversationMessages SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                UPDATE AIConversationToolAudits SET OrganizationId = '11111111-1111-1111-1111-111111111111';
                """);

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AIUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIUsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIUsageRecords_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrganizationId",
                table: "Tickets",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_OrganizationId",
                table: "TicketMessages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId",
                table: "Orders",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_OrderNumber",
                table: "Orders",
                columns: new[] { "OrganizationId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeDocuments_OrganizationId",
                table: "KnowledgeDocuments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_OrganizationId",
                table: "KnowledgeChunks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationId",
                table: "Customers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationId_Email",
                table: "Customers",
                columns: new[] { "OrganizationId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationMessages_OrganizationId",
                table: "AIConversationMessages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversations_OrganizationId",
                table: "AIConversations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationToolAudits_OrganizationId",
                table: "AIConversationToolAudits",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageRecords_OrganizationId",
                table: "AIUsageRecords",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageRecords_OrganizationId_CreatedAt",
                table: "AIUsageRecords",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId_NormalizedEmail",
                table: "AspNetUsers",
                columns: new[] { "OrganizationId", "NormalizedEmail" });

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Organizations_OrganizationId",
                table: "Customers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(name: "FK_AIConversations_Organizations_OrganizationId", table: "AIConversations", column: "OrganizationId", principalTable: "Organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_AIConversationMessages_Organizations_OrganizationId", table: "AIConversationMessages", column: "OrganizationId", principalTable: "Organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_AIConversationToolAudits_Organizations_OrganizationId", table: "AIConversationToolAudits", column: "OrganizationId", principalTable: "Organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeChunks_Organizations_OrganizationId",
                table: "KnowledgeChunks",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeDocuments_Organizations_OrganizationId",
                table: "KnowledgeDocuments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Organizations_OrganizationId",
                table: "Orders",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketMessages_Organizations_OrganizationId",
                table: "TicketMessages",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Organizations_OrganizationId",
                table: "Tickets",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_AIConversations_Organizations_OrganizationId", table: "AIConversations");
            migrationBuilder.DropForeignKey(name: "FK_AIConversationMessages_Organizations_OrganizationId", table: "AIConversationMessages");
            migrationBuilder.DropForeignKey(name: "FK_AIConversationToolAudits_Organizations_OrganizationId", table: "AIConversationToolAudits");
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Organizations_OrganizationId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeChunks_Organizations_OrganizationId",
                table: "KnowledgeChunks");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeDocuments_Organizations_OrganizationId",
                table: "KnowledgeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Organizations_OrganizationId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketMessages_Organizations_OrganizationId",
                table: "TicketMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Organizations_OrganizationId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "AIUsageRecords");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_OrganizationId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_OrganizationId",
                table: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrganizationId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrganizationId_OrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeDocuments_OrganizationId",
                table: "KnowledgeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeChunks_OrganizationId",
                table: "KnowledgeChunks");

            migrationBuilder.DropIndex(
                name: "IX_Customers_OrganizationId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_OrganizationId_Email",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "KnowledgeChunks");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Customers");

            migrationBuilder.DropColumn(name: "OrganizationId", table: "AIConversations");
            migrationBuilder.DropColumn(name: "OrganizationId", table: "AIConversationMessages");
            migrationBuilder.DropColumn(name: "OrganizationId", table: "AIConversationToolAudits");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);
        }
    }
}
