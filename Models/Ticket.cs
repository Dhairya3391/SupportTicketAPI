namespace SupportTicketAPI.Models;

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = "OPEN"; // OPEN | IN_PROGRESS | RESOLVED | CLOSED
    public string Priority { get; set; } = "MEDIUM"; // LOW | MEDIUM | HIGH
    public int CreatedBy { get; set; }
    public User Creator { get; set; } = null!;
    public int? AssignedTo { get; set; }
    public User? Assignee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketStatusLog> StatusLogs { get; set; } = new List<TicketStatusLog>();
}
