using System.Text.Json.Serialization;
using Epik.Crud.Api.Application;
using Epik.Crud.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Epik.Crud.Api.Api;
public sealed record UpdateAgeRequest([property: JsonPropertyName("edad")] int Age);
public static class PersonEndpoints
{
    public static IEndpointRouteBuilder MapPersonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/personas").WithTags("Personas");
        group.MapPost("/", Create).WithSummary("Insertar una persona");
        group.MapGet("/", List).WithSummary("Listar todas las personas (paginado: ?page=1&pageSize=10)");
        group.MapGet("/mujeres", ListWomen).WithSummary("Listar las mujeres desde la vista VW_Mujeres (paginado: ?page=1&pageSize=10)");
        group.MapGet("/{id}", Get).WithSummary("Buscar una persona por identificación");
        group.MapPatch("/{id}/edad", UpdateAge).WithSummary("Actualizar únicamente la edad, por identificación");
        group.MapDelete("/{id}", Delete).WithSummary("Eliminar una persona por identificación");
        return app;
    }

    private static async Task<Created<ApiResponse<Person>>> Create(Person person, IPersonService service)
    {
        var created = await service.CreateAsync(person);
        return TypedResults.Created($"/api/personas/{created.Id}", ApiResponse.Ok("Persona creada correctamente.", created));
    }

    private static async Task<Results<Ok<ApiResponse<Person>>, NotFound<ApiResponse<object>>>> Get(string id, IPersonService service) =>
        await service.GetAsync(id) is { } person
            ? TypedResults.Ok(ApiResponse.Ok("Persona encontrada.", person))
            : NotFound();

    private static async Task<Ok<ApiResponse<IReadOnlyList<Person>>>> List(IPersonService service, int page = 1, int pageSize = 10) =>
        TypedResults.Ok(ApiResponse.List("Listado de personas.", await service.ListAsync(page, pageSize)));

    private static async Task<Ok<ApiResponse<IReadOnlyList<Person>>>> ListWomen(IPersonService service, int page = 1, int pageSize = 10) =>
        TypedResults.Ok(ApiResponse.List("Listado de mujeres.", await service.ListWomenAsync(page, pageSize)));

    private static async Task<Results<Ok<ApiResponse<object>>, NotFound<ApiResponse<object>>>> UpdateAge(
        string id, UpdateAgeRequest request, IPersonService service) =>
        await service.UpdateAgeAsync(id, request.Age)
            ? TypedResults.Ok(ApiResponse.Ok("Edad actualizada correctamente."))
            : NotFound();

    private static async Task<Results<Ok<ApiResponse<object>>, NotFound<ApiResponse<object>>>> Delete(string id, IPersonService service) =>
        await service.DeleteAsync(id)
            ? TypedResults.Ok(ApiResponse.Ok("Persona eliminada correctamente."))
            : NotFound();

    private static NotFound<ApiResponse<object>> NotFound() =>
        TypedResults.NotFound(ApiResponse.Error("No se encontró una persona con esa identificación."));
}
