using System.ComponentModel.DataAnnotations;

namespace SupportTicketAPI.DTOs;

public class AssignDTO
{
    [Required]
    public int UserId { get; set; }
}
