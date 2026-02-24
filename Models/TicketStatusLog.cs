namespace SupportTicketAPI.Models;

public class TicketStatusLog
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public string OldStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
    public int ChangedBy { get; set; }
    public User Changer { get; set; } = null!;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
