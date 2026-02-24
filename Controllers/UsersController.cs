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

    /// <summary>List users (MANAGER)</summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetUsers()
    {
        if (CallerRole != "MANAGER")
            return StatusCode(403, new { message = "Only MANAGER can list users." });

        var users = await _db.Users
            .Include(u => u.Role)
            .OrderBy(u => u.Id)
            .ToListAsync();

        return Ok(users.Select(MapUser));
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
