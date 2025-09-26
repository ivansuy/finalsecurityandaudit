namespace AutoInventoryBackend.DTOs
{
    public class VehicleCreateDto
    {
        public string Brand { get; set; } = default!;
        public string Model { get; set; } = default!;
        public int Year { get; set; }
        public decimal Price { get; set; }
    }

    public class VehicleUpdateDto : VehicleCreateDto { }
}
