using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoCortex.Core.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddReportDirtySince : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReportDirtySince",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportNextAttemptAt",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportRetryCount",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportDirtySince",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ReportNextAttemptAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ReportRetryCount",
                table: "Projects");
        }
    }
}
