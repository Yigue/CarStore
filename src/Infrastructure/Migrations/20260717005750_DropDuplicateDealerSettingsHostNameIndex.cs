using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// saas-custom-domains-followups item 2: drops the redundant, PascalCase-named
    /// unique index "IX_DealerSettings_HostName_Unique" (originally added by
    /// AddDealerSettingsHostNameUniqueIndex), which duplicates the snake_case
    /// "ux_dealer_settings_host_name" index added later by
    /// AddDealerSettingsHostNameSlugIndexes. Both cover the same column
    /// (host_name), same uniqueness, and the same partial predicate
    /// (host_name IS NOT NULL) — keeping only the newer, convention-matching name.
    ///
    /// NOTE: `dotnet ef migrations add` produced an empty Up()/Down() for this
    /// migration — EF Core's migrations differ does not detect a removal when two
    /// indexes share the exact same (table, columns, unique, filter) signature and
    /// differ only by name, so the DropIndex/CreateIndex pair below was written by
    /// hand to match the corresponding CreateIndex/DropIndex in
    /// AddDealerSettingsHostNameUniqueIndex.
    /// </remarks>
    public partial class DropDuplicateDealerSettingsHostNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DealerSettings_HostName_Unique",
                schema: "public",
                table: "dealer_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DealerSettings_HostName_Unique",
                schema: "public",
                table: "dealer_settings",
                column: "host_name",
                unique: true,
                filter: "host_name IS NOT NULL");
        }
    }
}
