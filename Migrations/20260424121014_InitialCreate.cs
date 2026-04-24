using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Hummingbird.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DefaultSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShiftType = table.Column<string>(type: "text", nullable: false),
                    ClockInTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ClockOutTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TotalHours = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ServiceChargePercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeCode = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ShiftType = table.Column<string>(type: "text", nullable: false),
                    ClockIn = table.Column<TimeSpan>(type: "interval", nullable: true),
                    ClockOut = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TotalHours = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IsMissing = table.Column<bool>(type: "boolean", nullable: false),
                    IsPenalty = table.Column<bool>(type: "boolean", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeAttendanceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeCode = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Surname = table.Column<string>(type: "text", nullable: false),
                    PositionId = table.Column<int>(type: "integer", nullable: false),
                    Salary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DateJoined = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Deductions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ServiceChargeBonus = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalScheduledDays = table.Column<int>(type: "integer", nullable: false),
                    WorkDays = table.Column<int>(type: "integer", nullable: false),
                    PenaltyDays = table.Column<int>(type: "integer", nullable: false),
                    MissingDays = table.Column<int>(type: "integer", nullable: false),
                    ServiceChargeInput = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ServiceChargeVersion = table.Column<string>(type: "text", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppConfigs",
                columns: new[] { "Id", "Description", "Key", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { 1, "A = Fix-rate per employee, B = Workday-rate", "ServiceChargeVersion", new DateTime(2026, 4, 24, 12, 10, 14, 423, DateTimeKind.Utc).AddTicks(6890), "A" },
                    { 2, "Company / property name", "CompanyName", new DateTime(2026, 4, 24, 12, 10, 14, 423, DateTimeKind.Utc).AddTicks(6890), "My Hotel" },
                    { 3, "Whether initial setup has been completed", "Onboarded", new DateTime(2026, 4, 24, 12, 10, 14, 423, DateTimeKind.Utc).AddTicks(6890), "false" }
                });

            migrationBuilder.InsertData(
                table: "Positions",
                columns: new[] { "Id", "ClockInTime", "ClockOutTime", "DefaultSalary", "Name", "ServiceChargePercentage", "ShiftType", "TotalHours" },
                values: new object[,]
                {
                    { 1, new TimeSpan(0, 10, 0, 0, 0), new TimeSpan(0, 14, 0, 0, 0), 15000m, "Food and Beverage", 60m, "Morning", 4m },
                    { 2, new TimeSpan(0, 7, 0, 0, 0), new TimeSpan(0, 16, 0, 0, 0), 12000m, "Cleaner", 20m, "Morning", 9m },
                    { 3, new TimeSpan(0, 8, 0, 0, 0), new TimeSpan(0, 17, 0, 0, 0), 18000m, "Receptionist", 20m, "Morning", 9m },
                    { 4, new TimeSpan(0, 9, 0, 0, 0), new TimeSpan(0, 17, 0, 0, 0), 35000m, "Manager", 0m, "Morning", 8m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigs_Key",
                table: "AppConfigs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PositionId",
                table: "Employees",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Status",
                table: "Employees",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRecords_EmployeeId_Month_Year",
                table: "PayrollRecords",
                columns: new[] { "EmployeeId", "Month", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRecords_Year_Month",
                table: "PayrollRecords",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeAttendanceRecords_EmployeeCode_Date",
                table: "TimeAttendanceRecords",
                columns: new[] { "EmployeeCode", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeAttendanceRecords_Year_Month",
                table: "TimeAttendanceRecords",
                columns: new[] { "Year", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConfigs");

            migrationBuilder.DropTable(
                name: "PayrollRecords");

            migrationBuilder.DropTable(
                name: "TimeAttendanceRecords");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Positions");
        }
    }
}
