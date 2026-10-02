using System.Text.Json.Serialization;
using Epik.Crud.Api.Application;

namespace Epik.Crud.Api.Api;
public sealed record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Pagination? Pagination = null);

public sealed record Pagination(int Page, int PageSize, int TotalItems, int TotalPages);
public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(string message, T data) => new(true, message, data);
    public static ApiResponse<object> Ok(string message) => new(true, message, null);
    public static ApiResponse<object> Error(string message) => new(false, message, null);
    public static ApiResponse<IReadOnlyList<T>> List<T>(string message, PagedResult<T> page) =>
        new(true, message, page.Items, new Pagination(page.Page, page.PageSize, page.TotalItems, page.TotalPages));
}
