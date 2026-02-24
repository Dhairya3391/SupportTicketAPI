using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTicketAPI.Data;
using SupportTicketAPI.DTOs;
using SupportTicketAPI.Models;

namespace SupportTicketAPI.Controllers;

[ApiController]
[Tags("Comments")]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CommentsController(AppDbContext db) => _db = db;

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

    // ── POST /tickets/{id}/comments ───────────────────────────
    /// <summary>Add comment to ticket</summary>
    [HttpPost("tickets/{id}/comments")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddComment(int id, [FromBody] CommentDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null)
            return NotFound(new { message = "Ticket not found." });

        // RBAC check
        if (!CanAccessTicket(ticket))
            return StatusCode(403, new { message = "You do not have permission to comment on this ticket." });

        var comment = new TicketComment
        {
            TicketId = id,
            UserId = CallerId,
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _db.TicketComments.Add(comment);
        await _db.SaveChangesAsync();

        await _db.Entry(comment).Reference(c => c.User).LoadAsync();
        await _db.Entry(comment.User).Reference(u => u.Role).LoadAsync();

        return StatusCode(201, MapComment(comment));
    }

    // ── GET /tickets/{id}/comments ────────────────────────────
    /// <summary>List comments for a ticket with pagination</summary>
    [HttpGet("tickets/{id}/comments")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetComments(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null)
            return NotFound(new { message = "Ticket not found." });

        if (!CanAccessTicket(ticket))
            return StatusCode(403, new { message = "You do not have permission to view comments on this ticket." });

        // Validate pagination parameters
        if (page < 1)
            return BadRequest(new { message = "Page must be at least 1." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Page size must be between 1 and 100." });

        var query = _db.TicketComments
            .Include(c => c.User).ThenInclude(u => u.Role)
            .Where(c => c.TicketId == id);

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply ordering and pagination
        var comments = await query
            .OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new
        {
            data = comments.Select(MapComment),
            pagination = new
            {
                page,
                pageSize,
                totalCount,
                totalPages,
                hasNextPage = page < totalPages,
                hasPreviousPage = page > 1
            }
        });
    }

    // ── PATCH /comments/{id} ──────────────────────────────────
    /// <summary>Edit comment (author or MANAGER)</summary>
    [HttpPatch("comments/{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EditComment(int id, [FromBody] CommentDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var comment = await _db.TicketComments
            .Include(c => c.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment == null)
            return NotFound(new { message = "Comment not found." });

        // Only MANAGER or the author can edit
        if (CallerRole != "MANAGER" && comment.UserId != CallerId)
            return StatusCode(403, new { message = "You do not have permission to edit this comment." });

        comment.Comment = dto.Comment;
        await _db.SaveChangesAsync();

        return Ok(MapComment(comment));
    }

    // ── DELETE /comments/{id} ─────────────────────────────────
    /// <summary>Delete comment (author or MANAGER)</summary>
    [HttpDelete("comments/{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var comment = await _db.TicketComments.FindAsync(id);
        if (comment == null)
            return NotFound(new { message = "Comment not found." });

        // Only MANAGER or the author can delete
        if (CallerRole != "MANAGER" && comment.UserId != CallerId)
            return StatusCode(403, new { message = "You do not have permission to delete this comment." });

        _db.TicketComments.Remove(comment);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// RBAC rule: MANAGER always; SUPPORT only if assigned; USER only if owner.
    /// </summary>
    private bool CanAccessTicket(Ticket ticket)
    {
        return CallerRole switch
        {
            "MANAGER" => true,
            "SUPPORT" => ticket.AssignedTo == CallerId,
            _ => ticket.CreatedBy == CallerId  // USER
        };
    }

    private static object MapComment(TicketComment c) => new
    {
        id = c.Id,
        comment = c.Comment,
        user = new
        {
            id = c.User.Id,
            name = c.User.Name,
            email = c.User.Email,
            role = new { id = c.User.Role.Id, name = c.User.Role.Name },
            created_at = c.User.CreatedAt
        },
        created_at = c.CreatedAt
    };
}
