using Domain.Enums;

namespace Application.DTos.Request;

public sealed record InviteUserRequest(
    string Email,
    Guid RoleId,
    InviteDeliveryMethod DeliveryMethod = InviteDeliveryMethod.Internal);
