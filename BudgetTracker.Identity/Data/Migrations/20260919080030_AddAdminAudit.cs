using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Identity.Data.Migrations
{
    /// <summary>
    /// Dodaje tabelę <c>AdminAuditLog</c> — dziennik audytu usuwania kont i operacji na danych bez właściciela.
    /// </summary>
    /// <remarks>
    /// Tylko nowa tabela i trzy indeksy, istniejących danych nie dotyka. Nie ma kolumny z adresem e-mail: patrz
    /// <c>AdminAuditEntry.SubjectEmailHash</c>.
    /// </remarks>
    public partial class AddAdminAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectEmailHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rows = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLog_OccurredAt",
                table: "AdminAuditLog",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLog_SubjectEmailHash",
                table: "AdminAuditLog",
                column: "SubjectEmailHash");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLog_SubjectId",
                table: "AdminAuditLog",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLog");
        }
    }
}
