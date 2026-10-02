using System.Text.Json.Serialization;

namespace Epik.Crud.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<Gender>))]
public enum Gender
{
    [JsonStringEnumMemberName("Masculino")] Male,
    [JsonStringEnumMemberName("Femenino")] Female
}

public sealed record Person(
    [property: JsonPropertyName("identificacion")] string Id,
    [property: JsonPropertyName("nombres")] string FirstNames,
    [property: JsonPropertyName("apellidos")] string LastNames,
    [property: JsonPropertyName("edad")] int Age,
    [property: JsonPropertyName("genero")] Gender Gender);
