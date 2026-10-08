using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Api.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationAccessAndProcessRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Outbox",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "notification");

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "Outbox",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReadAccess",
                table: "Accounts",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "subtree");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "WriteAccess",
                table: "Accounts",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "subtree");

            migrationBuilder.CreateTable(
                name: "OrgUnits",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgUnits", x => x.Key);
                    table.ForeignKey(
                        name: "FK_OrgUnits_OrgUnits_ParentKey",
                        column: x => x.ParentKey,
                        principalTable: "OrgUnits",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoutingDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<int>(type: "int", nullable: false),
                    RuleId = table.Column<int>(type: "int", nullable: true),
                    RuleVersion = table.Column<long>(type: "bigint", nullable: true),
                    RuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutingDecisions_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoutingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    TargetUnit = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SpecJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutingRules_OrgUnits_TargetUnit",
                        column: x => x.TargetUnit,
                        principalTable: "OrgUnits",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_ParentKey",
                table: "OrgUnits",
                column: "ParentKey");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingDecisions_CaseId",
                table: "RoutingDecisions",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingRules_Enabled_Priority",
                table: "RoutingRules",
                columns: new[] { "Enabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingRules_TargetUnit",
                table: "RoutingRules",
                column: "TargetUnit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutingDecisions");

            migrationBuilder.DropTable(
                name: "RoutingRules");

            migrationBuilder.DropTable(
                name: "OrgUnits");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "ReadAccess",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "WriteAccess",
                table: "Accounts");
        }
    }
}
