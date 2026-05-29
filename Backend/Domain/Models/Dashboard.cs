namespace Domain.Models
{
    public class Dashboard
    {
        public Guid Id { get; set; }
        public Guid? CompanyId { get; set; }
        public Company? Company { get; set; }
        public List<string> AllowedRoles { get; set; } = [];
        public ICollection<User> Users { get; set; } = [];
    }
}
