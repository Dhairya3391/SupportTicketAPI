using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTicketAPI.Data;
using SupportTicketAPI.DTOs;
using SupportTicketAPI.Models;

namespace SupportTicketAPI.Controllers;

[ApiController]
[Route("tickets")]
[Tags("Tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TicketsController(AppDbContext db) => _db = db;

    private int CallerId
    {
        get
        {
            var claim = User.Claims.FirstOrDefault(c => 
                c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub || 
                c.Type == ClaimTypes.NameIdentifier || 
                c.Type == "sub");
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }

    private string CallerRole
    {
        get
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role);
            return claim?.Value ?? "";
        }
    }

    // ── Valid status transitions ───────────────────────────────
    private static readonly Dictionary<string, string> ValidTransitions = new()
    {
        { "OPEN", "IN_PROGRESS" },
        { "IN_PROGRESS", "RESOLVED" },
        { "RESOLVED", "CLOSED" }
    };

    private static readonly string[] ValidStatuses = { "OPEN", "IN_PROGRESS", "RESOLVED", "CLOSED" };
    private static readonly string[] ValidPriorities = { "LOW", "MEDIUM", "HIGH" };

    // ── POST /tickets ─────────────────────────────────────────
    /// <summary>Create ticket (USER, MANAGER)</summary>
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // SUPPORT cannot create tickets
        if (CallerRole == "SUPPORT")
            return StatusCode(403, new { message = "SUPPORT role cannot create tickets." });

        // Validate priority
        var priority = dto.Priority?.ToUpper() ?? "MEDIUM";
        if (!ValidPriorities.Contains(priority))
            return BadRequest(new { message = "Priority must be one of: LOW, MEDIUM, HIGH." });

        if (dto.Title.Length < 5)
            return BadRequest(new { message = "Title must be at least 5 characters." });

        if (dto.Description.Length < 10)
            return BadRequest(new { message = "Description must be at least 10 characters." });

        var ticket = new Ticket
        {
            Title = dto.Title,
            Description = dto.Description,
            Priority = priority,
            Status = "OPEN",
            CreatedBy = CallerId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        return StatusCode(201, await LoadTicketResponse(ticket.Id));
    }

    // ── GET /tickets ──────────────────────────────────────────
    /// <summary>Get tickets (MANAGER=all, SUPPORT=assigned, USER=own)</summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetTickets()
    {
        IQueryable<Ticket> query = _db.Tickets
            .Include(t => t.Creator).ThenInclude(u => u.Role)
            .Include(t => t.Assignee!).ThenInclude(u => u.Role);

        query = CallerRole switch
        {
            "MANAGER" => query,
            "SUPPORT" => query.Where(t => t.AssignedTo == CallerId),
            _ => query.Where(t => t.CreatedBy == CallerId) // USER
        };

        var tickets = await query.OrderBy(t => t.Id).ToListAsync();
        return Ok(tickets.Select(MapTicket));
    }

    // ── PATCH /tickets/{id}/assign ────────────────────────────
    /// <summary>Assign ticket (MANAGER, SUPPORT)</summary>
    [HttpPatch("{id}/assign")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AssignTicket(int id, [FromBody] AssignDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (CallerRole == "USER")
            return StatusCode(403, new { message = "USER role cannot assign tickets." });

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null)
            return NotFound(new { message = "Ticket not found." });

        var targetUser = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == dto.UserId);

        if (targetUser == null)
            return NotFound(new { message = "User not found." });

        // Cannot assign to USER role
        if (targetUser.Role.Name == "USER")
            return BadRequest(new { message = "Cannot assign ticket to a user with role USER." });

        ticket.AssignedTo = targetUser.Id;
        await _db.SaveChangesAsync();

        return Ok(await LoadTicketResponse(ticket.Id));
    }

    // ── PATCH /tickets/{id}/status ────────────────────────────
    /// <summary>Update ticket status (MANAGER, SUPPORT)</summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (CallerRole == "USER")
            return StatusCode(403, new { message = "USER role cannot update ticket status." });

        if (!ValidStatuses.Contains(dto.Status))
            return BadRequest(new { message = "Status must be one of: OPEN, IN_PROGRESS, RESOLVED, CLOSED." });

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null)
            return NotFound(new { message = "Ticket not found." });

        // SUPPORT can only update tickets assigned to them
        if (CallerRole == "SUPPORT" && ticket.AssignedTo != CallerId)
            return StatusCode(403, new { message = "You can only update tickets assigned to you." });

        // Enforce linear transition
        if (ticket.Status == dto.Status)
            return BadRequest(new { message = $"Ticket is already in status '{dto.Status}'." });

        if (!ValidTransitions.TryGetValue(ticket.Status, out var expectedNext) || expectedNext != dto.Status)
            return BadRequest(new { message = $"Invalid status transition: {ticket.Status} → {dto.Status}. Allowed: {ticket.Status} → {(ValidTransitions.ContainsKey(ticket.Status) ? ValidTransitions[ticket.Status] : "none")}." });

        var oldStatus = ticket.Status;
        ticket.Status = dto.Status;

        // Log the status change
        _db.TicketStatusLogs.Add(new TicketStatusLog
        {
            TicketId = ticket.Id,
            OldStatus = oldStatus,
            NewStatus = dto.Status,
            ChangedBy = CallerId,
            ChangedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok(await LoadTicketResponse(ticket.Id));
    }

    // ── DELETE /tickets/{id} ──────────────────────────────────
    /// <summary>Delete ticket (MANAGER)</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteTicket(int id)
    {
        if (CallerRole != "MANAGER")
            return StatusCode(403, new { message = "Only MANAGER can delete tickets." });

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null)
            return NotFound(new { message = "Ticket not found." });

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────
    private async Task<object> LoadTicketResponse(int ticketId)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Creator).ThenInclude(u => u.Role)
            .Include(t => t.Assignee!).ThenInclude(u => u.Role)
            .FirstAsync(t => t.Id == ticketId);

        return MapTicket(ticket);
    }

    public static object MapTicket(Ticket t) => new
    {
        id = t.Id,
        title = t.Title,
        description = t.Description,
        status = t.Status,
        priority = t.Priority,
        created_by = new
        {
            id = t.Creator.Id,
            name = t.Creator.Name,
            email = t.Creator.Email,
            role = new { id = t.Creator.Role.Id, name = t.Creator.Role.Name },
            created_at = t.Creator.CreatedAt
        },
        assigned_to = t.Assignee == null ? null : (object)new
        {
            id = t.Assignee.Id,
            name = t.Assignee.Name,
            email = t.Assignee.Email,
            role = new { id = t.Assignee.Role.Id, name = t.Assignee.Role.Name },
            created_at = t.Assignee.CreatedAt
        },
        created_at = t.CreatedAt
    };
}
