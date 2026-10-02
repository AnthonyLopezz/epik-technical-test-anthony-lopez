using System.Net;
using System.Net.Http.Json;
using Epik.Crud.Api.Api;
using Epik.Crud.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Epik.Crud.Tests;

public class PersonApiTests(PersonApiTests.ApiFactory factory) : IClassFixture<PersonApiTests.ApiFactory>
{
    private const string Route = "/api/personas";
    private readonly HttpClient _client = factory.CreateClient();
    private static string NewId() => Random.Shared.NextInt64(1_000_000_000_000).ToString();
    private async Task<ApiResponse<T>> ReadAsync<T>(string url) => (await _client.GetFromJsonAsync<ApiResponse<T>>(url))!;
    private async Task<Person> CreateAsync(Gender gender = Gender.Female)
    {
        var person = new Person(NewId(), "Laura", "Martínez", 22, gender);
        (await _client.PostAsJsonAsync(Route, person)).EnsureSuccessStatusCode();
        return person;
    }

    [Fact]
    public async Task Create_and_get_by_id()
    {
        var person = new Person(NewId(), "Laura", "Martínez", 22, Gender.Female);
        var response = await _client.PostAsJsonAsync(Route, person);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ApiResponse<Person>>();
        Assert.True(created!.Success);
        Assert.Equal(person, created.Data);

        var found = await ReadAsync<Person>($"{Route}/{person.Id}");
        Assert.True(found.Success);
        Assert.Equal(person, found.Data);
    }

    [Fact]
    public async Task Json_contract_stays_in_spanish()
    {
        var person = await CreateAsync();
        var json = await _client.GetStringAsync($"{Route}/{person.Id}");

        Assert.Contains($"\"identificacion\":\"{person.Id}\"", json);
        Assert.Contains("\"nombres\":\"Laura\"", json);
        Assert.Contains("\"genero\":\"Femenino\"", json);
    }

    [Fact]
    public async Task Root_serves_the_ui()
    {
        var html = await _client.GetStringAsync("/");
        Assert.Contains("Registro de personas", html);
    }

    [Fact]
    public async Task Responses_include_security_headers()
    {
        var response = await _client.GetAsync($"{Route}/missing");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Create_duplicate_returns_409()
    {
        var person = await CreateAsync();
        var response = await _client.PostAsJsonAsync(Route, person);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.False(error!.Success);
        Assert.Contains(person.Id, error.Message);
        Assert.Null(error.Data);
    }

    [Fact]
    public async Task Create_invalid_returns_400()
    {
        var response = await _client.PostAsJsonAsync(Route, new Person(NewId(), "", "Martínez", 22, Gender.Female));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_unknown_gender_returns_400()
    {
        var response = await _client.PostAsJsonAsync(Route,
            new { identificacion = NewId(), nombres = "Ana", apellidos = "Ruiz", edad = 30, genero = "Otro" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_missing_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Route}/missing")).StatusCode);

    [Fact]
    public async Task List_returns_men_and_women()
    {
        var woman = await CreateAsync(Gender.Female);
        var man = await CreateAsync(Gender.Male);
        var all = (await ReadAsync<List<Person>>($"{Route}?pageSize=100")).Data!;

        Assert.Contains(woman, all);
        Assert.Contains(man, all);
    }

    [Fact]
    public async Task List_women_only_returns_female()
    {
        var woman = await CreateAsync(Gender.Female);
        var man = await CreateAsync(Gender.Male);
        var women = (await ReadAsync<List<Person>>($"{Route}/mujeres?pageSize=100")).Data!;

        Assert.Contains(woman, women);
        Assert.DoesNotContain(man, women);
        Assert.All(women, p => Assert.Equal(Gender.Female, p.Gender));
    }

    [Fact]
    public async Task List_paginates_results()
    {
        for (var i = 0; i < 3; i++) await CreateAsync();

        var page1 = await ReadAsync<List<Person>>($"{Route}?page=1&pageSize=2");
        var page2 = await ReadAsync<List<Person>>($"{Route}?page=2&pageSize=2");

        var total = page1.Pagination!.TotalItems;
        Assert.True(total >= 3);
        Assert.Equal(new Pagination(1, 2, total, (total + 1) / 2), page1.Pagination);
        Assert.Equal(2, page1.Data!.Count);
        Assert.Equal(2, page2.Pagination!.Page);
        Assert.Empty(page1.Data.Intersect(page2.Data!));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task List_with_invalid_paging_returns_400(string query)
    {
        var response = await _client.GetAsync($"{Route}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<ApiResponse<object>>())!.Success);
    }

    [Fact]
    public async Task UpdateAge_only_changes_age()
    {
        var person = await CreateAsync();
        var response = await _client.PatchAsJsonAsync($"{Route}/{person.Id}/edad", new { edad = 40 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(person with { Age = 40 }, (await ReadAsync<Person>($"{Route}/{person.Id}")).Data);
    }

    [Fact]
    public async Task UpdateAge_of_missing_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PatchAsJsonAsync($"{Route}/missing/edad", new { edad = 40 })).StatusCode);

    [Fact]
    public async Task Delete_removes_the_record()
    {
        var person = await CreateAsync();

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{Route}/{person.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Route}/{person.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{Route}/{person.Id}")).StatusCode);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"epik-test-{Guid.NewGuid():N}.db");
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:Epik", $"Data Source={_dbPath};Pooling=False");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            File.Delete(_dbPath);
        }
    }
}
