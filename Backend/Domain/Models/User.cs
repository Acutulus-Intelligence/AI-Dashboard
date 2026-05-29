using Microsoft.AspNetCore.Identity;

namespace Domain.Models
{
    public class User : IdentityUser<Guid>
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
        public Guid? CompanyId { get; set; }
        public Company? Company { get; set; }
        public string? CompanyRole { get; set; }

        public ICollection<Dashboard> Dashboards { get; set; } = [];
    }
}
