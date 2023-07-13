using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NostrStreamer.Database;

namespace NostrStreamer.Migrations;

[DbContext(typeof(StreamerContext))]
[Migration("20230713140000_MillisatsBalance")]
public class MillisatsBalance : Migration {
    
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("update \"Users\" set \"Balance\" = \"Balance\" * 1000");
    }
}
