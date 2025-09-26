using AutoInventoryBackend.DTOs;
using AutoInventoryBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AutoInventoryBackend.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly RoleManager<IdentityRole> _roles;

        public UsersController(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
        {
            _users = users; _roles = roles;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var lst = _users.Users.Select(u => new { u.Id, u.Email, u.UserName, u.TwoFactorEnabled });
            return Ok(lst);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserDto dto)
        {
            if (!await _roles.RoleExistsAsync(dto.Role)) return BadRequest("Rol inválido");
            var u = new ApplicationUser { Email = dto.Email, UserName = dto.Email, EmailConfirmed = true };
            var res = await _users.CreateAsync(u, dto.Password);
            if (!res.Succeeded) return BadRequest(res.Errors);

            await _users.AddToRoleAsync(u, dto.Role);
            return Ok(new { u.Id, u.Email, dto.Role });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var u = await _users.FindByIdAsync(id);
            if (u == null) return NotFound();
            var res = await _users.DeleteAsync(u);
            if (!res.Succeeded) return BadRequest(res.Errors);
            return NoContent();
        }
    }
}
