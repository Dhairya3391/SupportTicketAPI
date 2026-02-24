using System.ComponentModel.DataAnnotations;

namespace SupportTicketAPI.DTOs;

public class CreateTicketDTO
{
    [Required]
    [MinLength(5)]
    public string Title { get; set; } = null!;

    [Required]
    [MinLength(10)]
    public string Description { get; set; } = null!;

    public string? Priority { get; set; } // LOW | MEDIUM | HIGH, defaults to MEDIUM
}
