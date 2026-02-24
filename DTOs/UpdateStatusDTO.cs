using System.ComponentModel.DataAnnotations;

namespace SupportTicketAPI.DTOs;

public class UpdateStatusDTO
{
    [Required]
    public string Status { get; set; } = null!; // OPEN | IN_PROGRESS | RESOLVED | CLOSED
}
