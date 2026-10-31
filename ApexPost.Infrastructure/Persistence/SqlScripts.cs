namespace ApexPost.Infrastructure.Persistence;

// Migrations deploy the procedures from /sql, so one file stays the single source of each procedure.
public static class SqlScripts
{
    public static string Read(string procedureName)
    {
        using var stream = typeof(SqlScripts).Assembly.GetManifestResourceStream($"ApexPost.Sql.{procedureName}.sql")
            ?? throw new InvalidOperationException($"Embedded SQL script {procedureName} was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // CREATE PROCEDURE must start a batch, but idempotent scripts wrap each migration in IF ... BEGIN.
    // EXEC gives the procedure its own batch. The text only ever comes from the embedded files above.
    public static string ReadForMigration(string procedureName) =>
        $"EXEC(N'{Read(procedureName).Replace("'", "''", StringComparison.Ordinal)}');";
}
