using Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Domain.Models
{
    public class User : IdentityUser<Guid>
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Guid? CompanyId { get; set; }
        public Company? Company { get; set; }
        public Guid? CompanyRoleId { get; set; }
        public CompanyRole? CompanyRole { get; set; }
        public UserType UserType { get; set; } = UserType.Individual;

        public ICollection<Dashboard> Dashboards { get; set; } = [];
        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    }
}
