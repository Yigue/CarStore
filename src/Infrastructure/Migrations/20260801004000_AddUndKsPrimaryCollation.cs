using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// PR4 Slice 12 (D1, REQ: postgres-collation-provisioning).
    /// Creates the und-u-ks-primary ICU non-deterministic collation for accent/case-insensitive search.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260801004000_AddUndKsPrimaryCollation")]
    public partial class AddUndKsPrimaryCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_collation WHERE collprovider = 'i') THEN
                        RAISE EXCEPTION 'This build of PostgreSQL has no ICU provider. Client search requires an ICU-enabled server (postgres:17-alpine ships one).';
                    END IF;
                END $$;
                CREATE COLLATION IF NOT EXISTS public.""und-u-ks-primary""
                    (provider = icu, locale = 'und-u-ks-primary', deterministic = false);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP COLLATION IF EXISTS public.""und-u-ks-primary"";");
        }
    }
}
