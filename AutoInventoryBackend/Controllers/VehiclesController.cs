using AutoInventoryBackend.Data;
using AutoInventoryBackend.DTOs;
using AutoInventoryBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoInventoryBackend.Controllers
{
    [ApiController]
    [Route("api/vehicles")]
    public class VehiclesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _http;

        public VehiclesController(AppDbContext db, IHttpContextAccessor http)
        {
            _db = db; _http = http;
        }

        // Catálogo público (sin token)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] string? q, int page = 1, int pageSize = 20)
        {
            var query = _db.Vehicles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(v => v.Brand.Contains(q) || v.Model.Contains(q));

            var total = await query.CountAsync();
            var data = await query
                .OrderByDescending(v => v.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new { total, data });
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOne(int id)
        {
            var v = await _db.Vehicles.FindAsync(id);
            return v is null ? NotFound() : Ok(v);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Create(VehicleCreateDto dto)
        {
            var v = new Vehicle { Brand = dto.Brand, Model = dto.Model, Year = dto.Year, Price = dto.Price };
            _db.Vehicles.Add(v);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetOne), new { id = v.Id }, v);
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Update(int id, VehicleUpdateDto dto)
        {
            var v = await _db.Vehicles.FindAsync(id);
            if (v is null) return NotFound();
            v.Brand = dto.Brand; v.Model = dto.Model; v.Year = dto.Year; v.Price = dto.Price;
            await _db.SaveChangesAsync();
            return Ok(v);
        }

        // Soft delete
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var v = await _db.Vehicles.FindAsync(id);
            if (v is null) return NotFound();

            v.IsDeleted = true;
            v.DeletedAt = DateTime.UtcNow;
            v.DeletedByUserId = _http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
