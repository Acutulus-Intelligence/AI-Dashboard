using System.Text.Json.Serialization;

namespace Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectionVisibility
{
    Private = 0,
    Company = 1,
    Roles = 2
}