using System.ComponentModel.DataAnnotations;

namespace VideoCortex.Features.Projects.Models;

/// <summary>
/// Bound by the add-project <c>EditForm</c>. A class (not a record) so Blazor two-way binding
/// and <c>DataAnnotationsValidator</c> work.
/// </summary>
public class ProjectFormModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, ErrorMessage = "Name must be 120 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "AI Instructions")]
    [StringLength(4000)]
    public string? AIInstructions { get; set; }
}
