using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mauw.Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimaryColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "colors",
                columns: new[] { "id", "name", "html_color", "created_at", "updated_at" },
                values: new object[,]
                {
                    { Guid.NewGuid(), "Bianco", "#FFFFFF", DateTime.UtcNow, DateTime.UtcNow },
                    { Guid.NewGuid(), "Grigio", "#808080", DateTime.UtcNow, DateTime.UtcNow },
                    { Guid.NewGuid(), "Nero", "#1A1A1A", DateTime.UtcNow, DateTime.UtcNow },
                    { Guid.NewGuid(), "Giallo", "#FFFF00", DateTime.UtcNow, DateTime.UtcNow },
                    { Guid.NewGuid(), "Viola", "#800080", DateTime.UtcNow, DateTime.UtcNow },
                    { Guid.NewGuid(), "Rosso", "#FF0000", DateTime.UtcNow, DateTime.UtcNow }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "colors",
                keyColumn: "name",
                keyValues: new object[] { "Bianco", "Grigio", "Nero", "Giallo", "Viola", "Rosso" });
        }
    }
}
