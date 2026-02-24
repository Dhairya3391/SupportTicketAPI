namespace SupportTicketAPI.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = null!; // MANAGER | SUPPORT | USER
    public ICollection<User> Users { get; set; } = new List<User>();
}
