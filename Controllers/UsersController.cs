using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTicketAPI.Data;
using SupportTicketAPI.DTOs;
using SupportTicketAPI.Models;

namespace SupportTicketAPI.Controllers;

[ApiController]
[Route("users")]
[Tags("Users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    private string? CallerRole => User.Claims
        .FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value;

    /// <summary>Create user (MANAGER)</summary>
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (CallerRole != "MANAGER")
            return StatusCode(403, new { message = "Only MANAGER can create users." });

        // Validate role value
        var validRoles = new[] { "MANAGER", "SUPPORT", "USER" };
        if (!validRoles.Contains(dto.Role))
            return BadRequest(new { message = "Role must be one of: MANAGER, SUPPORT, USER." });

        // Check email uniqueness
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest(new { message = "Email is already in use." });

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == dto.Role);
        if (role == null)
            return BadRequest(new { message = $"Role '{dto.Role}' not found." });

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = role.Id,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Reload with role
        await _db.Entry(user).Reference(u => u.Role).LoadAsync();

        return StatusCode(201, MapUser(user));
    }

    /// <summary>List users (MANAGER) with pagination and filtering</summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? role = null,
        [FromQuery] string? search = null)
    {
        if (CallerRole != "MANAGER")
            return StatusCode(403, new { message = "Only MANAGER can list users." });

        // Validate pagination parameters
        if (page < 1)
            return BadRequest(new { message = "Page must be at least 1." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Page size must be between 1 and 100." });

        // Validate role filter
        var validRoles = new[] { "MANAGER", "SUPPORT", "USER" };
        if (!string.IsNullOrEmpty(role) && !validRoles.Contains(role.ToUpper()))
            return BadRequest(new { message = "Invalid role. Must be one of: MANAGER, SUPPORT, USER." });

        IQueryable<User> query = _db.Users.Include(u => u.Role);

        // Apply role filter
        if (!string.IsNullOrEmpty(role))
            query = query.Where(u => u.Role.Name == role.ToUpper());

        // Apply search filter (name or email)
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply ordering and pagination
        var users = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new
        {
            data = users.Select(MapUser),
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

    // ── Helper ────────────────────────────────────────────────
    public static object MapUser(User u) => new
    {
        id = u.Id,
        name = u.Name,
        email = u.Email,
        role = new { id = u.Role.Id, name = u.Role.Name },
        created_at = u.CreatedAt
    };
}
