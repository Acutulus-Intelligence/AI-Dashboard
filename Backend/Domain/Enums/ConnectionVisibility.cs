using System.Text.Json.Serialization;

namespace Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectionVisibility
{
    Private = 0,
    Company = 1,
    Roles = 2
}
