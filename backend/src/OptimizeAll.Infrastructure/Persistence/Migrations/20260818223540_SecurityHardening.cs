using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OptimizeAll.Infrastructure.Persistence.Migrations;

/// <summary>
/// Applies the controls that cannot be expressed through the EF model: row-level security
/// policies, database roles, append-only enforcement on the audit trail, integrity triggers, and
/// the HNSW vector index.
/// <para>
/// The statements live in an embedded <c>.sql</c> file rather than inline strings so that they are
/// reviewable as SQL, diffable, and runnable directly against a database during an incident —
/// which is precisely when nobody wants to reconstruct them from C# string literals.
/// </para>
/// </summary>
public partial class SecurityHardening : Migration
{
    private const string UpScript = "OptimizeAll.Infrastructure.Persistence.Migrations.Sql.002_security_hardening.sql";
    private const string DownScript = "OptimizeAll.Infrastructure.Persistence.Migrations.Sql.002_security_hardening_down.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(ReadEmbeddedScript(UpScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(ReadEmbeddedScript(DownScript));
    }

    private static string ReadEmbeddedScript(string resourceName)
    {
        Assembly assembly = typeof(SecurityHardening).Assembly;

        using Stream? stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Migration script '{resourceName}' is not embedded in {assembly.GetName().Name}. " +
                "Check the EmbeddedResource glob in the project file.");

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
