namespace Application.Dtos.Request;

public sealed record TransferOwnershipRequest(
    Guid NewOwnerId);
