using System.Text.Json.Serialization;

namespace Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SslMode
{
    None,
    Prefer,
    Require,
    VerifyFull
}