namespace Application.DTos.Request;

public sealed record TransferOwnershipRequest(
    Guid NewOwnerId
);
