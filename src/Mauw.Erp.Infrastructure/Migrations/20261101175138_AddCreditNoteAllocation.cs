using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditNoteAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_credit_note_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "allocated_amount",
                table: "credit_notes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.CreateIndex(
                name: "i_x_payments_source_credit_note_id",
                table: "payments",
                column: "source_credit_note_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_payments_credit_notes_source_credit_note_id",
                table: "payments",
                column: "source_credit_note_id",
                principalTable: "credit_notes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_payments_credit_notes_source_credit_note_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "i_x_payments_source_credit_note_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "source_credit_note_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "allocated_amount",
                table: "credit_notes");
        }
    }
}
