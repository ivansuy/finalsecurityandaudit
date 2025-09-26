using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Frontend.Models
{
    public class Vehicle
    {
        public int Id { get; set; }

        [JsonPropertyName("brand")]
        [Required]
        public string Brand { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        [Required]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("year")]
        [Required]
        public int Year { get; set; }

        [JsonPropertyName("price")]
        [Required]
        public decimal Price { get; set; }
    }
}
