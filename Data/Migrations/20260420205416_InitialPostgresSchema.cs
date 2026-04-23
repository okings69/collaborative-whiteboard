using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollaborativeBoard.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccentColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastConnectionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ShareCode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    AccentColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByNickname = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boards_Participants_OwnerParticipantId",
                        column: x => x.OwnerParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BoardPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardPages_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrawingElements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardPageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ElementType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    StrokeColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FillColor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StrokeWidth = table.Column<float>(type: "real", nullable: false),
                    X = table.Column<float>(type: "real", nullable: false),
                    Y = table.Column<float>(type: "real", nullable: false),
                    Width = table.Column<float>(type: "real", nullable: false),
                    Height = table.Column<float>(type: "real", nullable: false),
                    FontSize = table.Column<float>(type: "real", nullable: false),
                    TextContent = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PointsJson = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    VersionToken = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LayerOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedByNickname = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrawingElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrawingElements_BoardPages_BoardPageId",
                        column: x => x.BoardPageId,
                        principalTable: "BoardPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrawingElements_Participants_CreatedByParticipantId",
                        column: x => x.CreatedByParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardPages_BoardId_SortOrder",
                table: "BoardPages",
                columns: new[] { "BoardId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boards_OwnerParticipantId",
                table: "Boards",
                column: "OwnerParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_ShareCode",
                table: "Boards",
                column: "ShareCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrawingElements_BoardPageId_CreatedAtUtc",
                table: "DrawingElements",
                columns: new[] { "BoardPageId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DrawingElements_BoardPageId_LayerOrder",
                table: "DrawingElements",
                columns: new[] { "BoardPageId", "LayerOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DrawingElements_CreatedByParticipantId",
                table: "DrawingElements",
                column: "CreatedByParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_Nickname",
                table: "Participants",
                column: "Nickname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrawingElements");

            migrationBuilder.DropTable(
                name: "BoardPages");

            migrationBuilder.DropTable(
                name: "Boards");

            migrationBuilder.DropTable(
                name: "Participants");
        }
    }
}
