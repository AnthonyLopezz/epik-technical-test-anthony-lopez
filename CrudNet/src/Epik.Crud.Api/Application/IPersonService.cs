using Epik.Crud.Api.Domain;

namespace Epik.Crud.Api.Application;

public interface IPersonService
{
    Task<Person> CreateAsync(Person person);
    Task<Person?> GetAsync(string id);
    Task<PagedResult<Person>> ListAsync(int page, int pageSize);
    Task<PagedResult<Person>> ListWomenAsync(int page, int pageSize);
    Task<bool> UpdateAgeAsync(string id, int age);
    Task<bool> DeleteAsync(string id);
}
