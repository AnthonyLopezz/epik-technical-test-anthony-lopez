using System.Text.RegularExpressions;
using Epik.Crud.Api.Domain;

namespace Epik.Crud.Api.Application;

public sealed class PersonService(IPersonRepository repository) : IPersonService
{
    private const int MinAge = 0;
    private const int MaxAge = 150;
    private const int MaxPageSize = 100;
    private static readonly Regex DigitsOnly = new(@"^[0-9]+$");
    private static readonly Regex LettersOnly = new(@"^\p{L}+(?:[ '-]+\p{L}+)*$");
    private static readonly Regex WordStart = new(@"(?<=^|[ '-])\p{L}");
    private static readonly Regex RepeatedSpaces = new(" {2,}");

    public async Task<Person> CreateAsync(Person person)
    {
        var normalized = person with
        {
            Id = Required(person.Id, "Identificación", 20, DigitsOnly, "solo números"),
            FirstNames = Capitalize(Required(person.FirstNames, "Nombres", 100, LettersOnly, "solo letras")),
            LastNames = Capitalize(Required(person.LastNames, "Apellidos", 100, LettersOnly, "solo letras"))
        };
        ValidateAge(normalized.Age);
        if (!Enum.IsDefined(normalized.Gender))
            throw new ValidationException("El género debe ser Masculino o Femenino.");

        if (await repository.GetByIdAsync(normalized.Id) is not null)
            throw new ConflictException($"Ya existe una persona con identificación '{normalized.Id}'.");

        await repository.InsertAsync(normalized);
        return normalized;
    }

    public Task<Person?> GetAsync(string id) => repository.GetByIdAsync(id.Trim());

    public Task<PagedResult<Person>> ListAsync(int page, int pageSize)
    {
        ValidatePaging(page, pageSize);
        return repository.ListAsync(page, pageSize);
    }

    public Task<PagedResult<Person>> ListWomenAsync(int page, int pageSize)
    {
        ValidatePaging(page, pageSize);
        return repository.ListWomenAsync(page, pageSize);
    }

    public Task<bool> UpdateAgeAsync(string id, int age)
    {
        ValidateAge(age);
        return repository.UpdateAgeAsync(id.Trim(), age);
    }

    public Task<bool> DeleteAsync(string id) => repository.DeleteAsync(id.Trim());

    private static string Required(string? value, string field, int maxLength, Regex format, string formatDescription)
    {
        string? clean;
        // NFC: an "é" typed as "e" + combining accent becomes a single character (what LettersOnly expects).
        try { clean = value?.Normalize().Trim(); }
        catch (ArgumentException) { throw new ValidationException($"El campo {field} contiene caracteres inválidos."); }
        if (string.IsNullOrEmpty(clean))
            throw new ValidationException($"El campo {field} es obligatorio.");
        if (clean.Length > maxLength)
            throw new ValidationException($"El campo {field} admite máximo {maxLength} caracteres.");
        if (!format.IsMatch(clean))
            throw new ValidationException($"El campo {field} admite {formatDescription}.");
        return clean;
    }

    private static string Capitalize(string value) =>
        WordStart.Replace(RepeatedSpaces.Replace(value.ToLowerInvariant(), " "), m => m.Value.ToUpperInvariant());

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1)
            throw new ValidationException("El parámetro page debe ser 1 o mayor.");
        if (pageSize is < 1 or > MaxPageSize)
            throw new ValidationException($"El parámetro pageSize debe estar entre 1 y {MaxPageSize}.");
    }

    private static void ValidateAge(int age)
    {
        if (age is < MinAge or > MaxAge)
            throw new ValidationException($"La edad debe estar entre {MinAge} y {MaxAge}.");
    }
}
