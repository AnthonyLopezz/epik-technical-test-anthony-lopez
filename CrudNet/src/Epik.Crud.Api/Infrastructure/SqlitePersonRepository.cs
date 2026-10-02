using Epik.Crud.Api.Application;
using Epik.Crud.Api.Domain;
using Microsoft.Data.Sqlite;

namespace Epik.Crud.Api.Infrastructure;

public sealed class SqlitePersonRepository(string connectionString) : IPersonRepository
{
    private const string Columns = "Identificacion, Nombres, Apellidos, Edad, Genero";

    public Task InsertAsync(Person p) =>
        ExecuteAsync(
            $"INSERT INTO Persona ({Columns}) VALUES (@id, @firstNames, @lastNames, @age, @gender)",
            ("@id", p.Id), ("@firstNames", p.FirstNames), ("@lastNames", p.LastNames),
            ("@age", p.Age), ("@gender", ToDb(p.Gender)));

    public async Task<Person?> GetByIdAsync(string id) =>
        (await QueryAsync($"SELECT {Columns} FROM Persona WHERE Identificacion = @id", ("@id", id))).SingleOrDefault();

    public Task<PagedResult<Person>> ListAsync(int page, int pageSize) => PageAsync("Persona", page, pageSize);

    public Task<PagedResult<Person>> ListWomenAsync(int page, int pageSize) => PageAsync("VW_Mujeres", page, pageSize);

    public async Task<bool> UpdateAgeAsync(string id, int age) =>
        await ExecuteAsync("UPDATE Persona SET Edad = @age WHERE Identificacion = @id", ("@age", age), ("@id", id)) > 0;

    public async Task<bool> DeleteAsync(string id) =>
        await ExecuteAsync("DELETE FROM Persona WHERE Identificacion = @id", ("@id", id)) > 0;

    private static string ToDb(Gender gender) => gender == Gender.Female ? "Femenino" : "Masculino";

    private static Gender FromDb(string gender) => gender == "Femenino" ? Gender.Female : Gender.Male;

    private async Task<PagedResult<Person>> PageAsync(string source, int page, int pageSize)
    {
        var items = await QueryAsync(
            $"SELECT {Columns} FROM {source} ORDER BY Apellidos, Nombres, Identificacion LIMIT @pageSize OFFSET @offset",
            ("@pageSize", pageSize), ("@offset", (long)(page - 1) * pageSize));

        await using var connection = await OpenAsync();
        await using var command = CreateCommand(connection, $"SELECT COUNT(*) FROM {source}", []);
        var total = Convert.ToInt32(await command.ExecuteScalarAsync());
        return new PagedResult<Person>(items, page, pageSize, total);
    }

    private async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = CreateCommand(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<IReadOnlyList<Person>> QueryAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var command = CreateCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync();

        var people = new List<Person>();
        while (await reader.ReadAsync())
            people.Add(new Person(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetInt32(3), FromDb(reader.GetString(4))));
        return people;
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, string sql, (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return command;
    }
}
