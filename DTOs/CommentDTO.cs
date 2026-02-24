using System.ComponentModel.DataAnnotations;

namespace SupportTicketAPI.DTOs;

public class CommentDTO
{
    [Required]
    public string Comment { get; set; } = null!;
}
