using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberLms.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentAcknowledgmentText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcknowledgmentText",
                table: "Contents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcknowledgmentText",
                table: "Contents");
        }
    }
}
