using Domain.Enums;

namespace Domain.Models;

/// <summary>
/// A company-scoped (or private) collection of uploaded tabular files. Each
/// uploaded file becomes a separate table (SavedDataset) inside the collection.
/// Company members can view and generate charts; only admins manage it.
/// </summary>
public class DataCollection
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Null for a private collection owned by a single (individual) user.</summary>
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public CollectionVisibility Visibility { get; set; } = CollectionVisibility.Private;

    public List<Guid> AllowedRoleIds { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SavedDataset> Files { get; set; } = [];
}