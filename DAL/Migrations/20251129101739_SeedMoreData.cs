using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class SeedMoreData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Airplanes",
                keyColumn: "Id",
                keyValue: 1,
                column: "SeatCapacity",
                value: (short)8);

            migrationBuilder.UpdateData(
                table: "Airplanes",
                keyColumn: "Id",
                keyValue: 2,
                column: "SeatCapacity",
                value: (short)7);

            migrationBuilder.UpdateData(
                table: "Airplanes",
                keyColumn: "Id",
                keyValue: 3,
                column: "SeatCapacity",
                value: (short)5);

            migrationBuilder.UpdateData(
                table: "Airplanes",
                keyColumn: "Id",
                keyValue: 4,
                column: "SeatCapacity",
                value: (short)4);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 1,
                column: "FlightId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 2,
                column: "FlightId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 3,
                column: "FlightId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 4,
                column: "FlightId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 5,
                column: "FlightId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 6,
                column: "FlightId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 7,
                column: "FlightId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 8,
                column: "FlightId",
                value: 1);

            migrationBuilder.InsertData(
                table: "FlightSeats",
                columns: new[] { "Id", "FlightId", "IsAvailable", "SeatId", "TicketId" },
                values: new object[,]
                {
                    { 9, 2, true, 1, null },
                    { 10, 2, true, 2, null },
                    { 11, 2, true, 3, null },
                    { 12, 2, true, 4, null },
                    { 13, 2, true, 5, null },
                    { 14, 2, true, 6, null },
                    { 15, 2, true, 7, null },
                    { 16, 3, true, 1, null },
                    { 17, 3, true, 2, null },
                    { 18, 3, true, 3, null },
                    { 19, 3, true, 4, null },
                    { 20, 3, true, 5, null },
                    { 23, 4, true, 1, null },
                    { 24, 4, true, 2, null },
                    { 25, 4, true, 3, null },
                    { 26, 4, true, 4, null },
                    { 27, 5, true, 1, null },
                    { 28, 5, true, 2, null },
                    { 29, 5, true, 3, null },
                    { 30, 5, true, 4, null },
                    { 31, 5, true, 5, null },
                    { 32, 5, true, 6, null },
                    { 33, 5, true, 7, null },
                    { 34, 5, true, 8, null },
                    { 35, 6, true, 1, null },
                    { 36, 6, true, 2, null },
                    { 37, 6, true, 3, null },
                    { 38, 6, true, 4, null },
                    { 39, 6, true, 5, null },
                    { 40, 6, true, 6, null },
                    { 41, 6, true, 7, null },
                    { 42, 7, true, 2, null },
                    { 43, 7, true, 3, null },
                    { 44, 7, true, 4, null },
                    { 45, 7, true, 5, null },
                    { 46, 7, true, 6, null },
                    { 47, 7, true, 7, null },
                    { 48, 8, true, 1, null },
                    { 49, 8, true, 2, null },
                    { 50, 8, true, 3, null },
                    { 51, 8, true, 4, null },
                    { 52, 8, true, 5, null },
                    { 53, 8, true, 6, null },
                    { 54, 8, true, 7, null },
                    { 55, 9, true, 1, null },
                    { 56, 9, true, 2, null },
                    { 57, 9, true, 3, null },
                    { 58, 9, true, 4, null },
                    { 59, 9, true, 5, null },
                    { 60, 9, true, 6, null },
                    { 61, 9, true, 8, null },
                    { 62, 9, true, 7, null },
                    { 63, 10, true, 1, null },
                    { 64, 10, true, 2, null },
                    { 65, 10, true, 3, null },
                    { 66, 10, true, 4, null },
                    { 67, 11, true, 1, null },
                    { 68, 11, true, 2, null },
                    { 69, 11, true, 3, null },
                    { 70, 11, true, 4, null },
                    { 71, 11, true, 5, null },
                    { 72, 11, true, 6, null },
                    { 73, 11, true, 7, null },
                    { 74, 12, true, 1, null },
                    { 75, 12, true, 2, null },
                    { 76, 12, true, 3, null },
                    { 77, 12, true, 4, null },
                    { 78, 12, true, 5, null },
                    { 79, 12, true, 6, null },
                    { 80, 12, true, 8, null },
                    { 81, 12, true, 7, null },
                    { 82, 7, true, 1, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.UpdateData(
                table: "Airplanes",
                keyColumn: "Id",
                keyValue: 1,
                column: "SeatCapacity",
                value: (short)180);

            migrationBuilder.UpdateData(
                table: "Airplanes",
                keyColumn: "Id",
                keyValue: 2,
                column: "SeatCapacity",
                value: (short)150);

            migrationBuilder.UpdateData(
                table: "Airplanes",
                keyColumn: "Id",
                keyValue: 3,
                column: "SeatCapacity",
                value: (short)396);

            migrationBuilder.UpdateData(
                table: "Airplanes",
                keyColumn: "Id",
                keyValue: 4,
                column: "SeatCapacity",
                value: (short)396);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 1,
                column: "FlightId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 2,
                column: "FlightId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 3,
                column: "FlightId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 4,
                column: "FlightId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 5,
                column: "FlightId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 6,
                column: "FlightId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 7,
                column: "FlightId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "FlightSeats",
                keyColumn: "Id",
                keyValue: 8,
                column: "FlightId",
                value: 5);
        }
    }
}
