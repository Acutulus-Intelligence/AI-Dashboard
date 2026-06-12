using Domain.Enums;
namespace Application.Dtos.Request; 
public sealed record RegisterRequest(string Email, 
    string Password, 
    string FirstName, 
    string LastName, 
    UserType UserType, 
    string? CompanyName = null, 
    string? InviteToken = null);
