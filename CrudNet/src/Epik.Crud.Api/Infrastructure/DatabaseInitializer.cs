using Microsoft.Data.Sqlite;

namespace Epik.Crud.Api.Infrastructure;

public static class DatabaseInitializer
{
    public static void RunScript(string connectionString, string scriptPath)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = File.ReadAllText(scriptPath);
        command.ExecuteNonQuery();
    }
}
