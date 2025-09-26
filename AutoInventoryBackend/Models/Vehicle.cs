using System.ComponentModel.DataAnnotations;

namespace AutoInventoryBackend.Models
{
    public class Vehicle
    {
        public int Id { get; set; }

        [Required, MaxLength(60)]
        public string Brand { get; set; } = default!;

        [Required, MaxLength(60)]
        public string Model { get; set; } = default!;

        public int Year { get; set; }
        public decimal Price { get; set; }

        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
    }
}
