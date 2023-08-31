using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NostrStreamer.Database;

namespace NostrStreamer.Migrations;

[DbContext(typeof(StreamerContext))]
[Migration("20230831134200_AddCredits")]
public class AddCredits : Migration {
    
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("create extension if not exists pgcrypto");
        migrationBuilder.Sql(@"insert into ""Payments""
select ""PubKey"", null, true, encode(digest(concat(""PubKey"", now()::text), 'sha256'), 'hex'), 1000 * 1000, now(), null, 2 from ""Users""");
    }
    
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("delete from \"Payments\" where \"Type\" = 2");
    }
}
