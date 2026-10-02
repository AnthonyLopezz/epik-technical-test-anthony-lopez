using Epik.Crud.Api.Application;
using Epik.Crud.Api.Domain;

namespace Epik.Crud.Tests;

public class PersonServiceTests
{
    private readonly InMemoryRepository _repository = new();
    private readonly PersonService _service;
    public PersonServiceTests() => _service = new PersonService(_repository);
    private static Person Ana(string id = "1") => new(id, "Ana", "Gómez", 28, Gender.Female);

    [Fact]
    public async Task Create_trims_and_saves()
    {
        var created = await _service.CreateAsync(Ana() with { Id = " 1 ", FirstNames = "  Ana " });

        Assert.Equal("1", created.Id);
        Assert.Equal("Ana", created.FirstNames);
        Assert.Equal(created, await _repository.GetByIdAsync("1"));
    }

    [Theory]
    [InlineData("jANE", "Jane")]
    [InlineData("josé  MARÍA", "José María")]
    [InlineData("pérez-gómez", "Pérez-Gómez")]
    [InlineData("o'brien", "O'Brien")]
    [InlineData("josé", "José")] // combining accent (NFD) -> NFC
    public async Task Create_capitalizes_names(string input, string expected)
    {
        var created = await _service.CreateAsync(Ana() with { FirstNames = input, LastNames = input });
        Assert.Equal(expected, created.FirstNames);
        Assert.Equal(expected, created.LastNames);
    }

    [Fact]
    public async Task Create_with_duplicate_id_throws_conflict()
    {
        await _service.CreateAsync(Ana());
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Ana()));
    }

    [Theory]
    [InlineData("", "Ana", "Gómez", 20)]
    [InlineData("1", " ", "Gómez", 20)]
    [InlineData("1", "Ana", "", 20)]
    [InlineData("1", "Ana", "Gómez", -1)]
    [InlineData("1", "Ana", "Gómez", 151)]
    [InlineData("12A", "Ana", "Gómez", 20)]
    [InlineData("1", "Ana2", "Gómez", 20)]
    [InlineData("1", "Ana", "Gómez!", 20)]
    public async Task Create_with_invalid_data_throws_validation(string id, string firstNames, string lastNames, int age)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new Person(id, firstNames, lastNames, age, Gender.Female)));
        Assert.Empty(_repository.People);
    }

    [Fact]
    public async Task Create_with_undefined_gender_throws_validation() =>
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(Ana() with { Gender = (Gender)99 }));

    [Fact]
    public async Task UpdateAge_validates_range_before_touching_the_repository()
    {
        await _service.CreateAsync(Ana());

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateAgeAsync("1", 200));
        Assert.Equal(28, _repository.People["1"].Age);
    }

    [Fact]
    public async Task UpdateAge_and_Delete_return_false_when_missing()
    {
        Assert.False(await _service.UpdateAgeAsync("missing", 30));
        Assert.False(await _service.DeleteAsync("missing"));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_with_invalid_paging_throws_validation(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ListAsync(page, pageSize));
        await Assert.ThrowsAsync<ValidationException>(() => _service.ListWomenAsync(page, pageSize));
    }

    private sealed class InMemoryRepository : IPersonRepository
    {
        public Dictionary<string, Person> People { get; } = [];
        public Task InsertAsync(Person person)
        {
            People.Add(person.Id, person);
            return Task.CompletedTask;
        }

        public Task<Person?> GetByIdAsync(string id) => Task.FromResult(People.GetValueOrDefault(id));
        public Task<PagedResult<Person>> ListAsync(int page, int pageSize) => Paginate(People.Values, page, pageSize);
        public Task<PagedResult<Person>> ListWomenAsync(int page, int pageSize) =>
            Paginate(People.Values.Where(p => p.Gender == Gender.Female), page, pageSize);

        private static Task<PagedResult<Person>> Paginate(IEnumerable<Person> people, int page, int pageSize)
        {
            var all = people.ToList();
            return Task.FromResult(new PagedResult<Person>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count));
        }

        public Task<bool> UpdateAgeAsync(string id, int age)
        {
            if (!People.TryGetValue(id, out var p)) return Task.FromResult(false);
            People[id] = p with { Age = age };
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(string id) => Task.FromResult(People.Remove(id));
    }
}
