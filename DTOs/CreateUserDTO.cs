using System.ComponentModel.DataAnnotations;

namespace SupportTicketAPI.DTOs;

public class CreateUserDTO
{
    [Required]
    public string Name { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    [Required]
    public string Role { get; set; } = null!; // MANAGER | SUPPORT | USER
}
