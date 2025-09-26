using Frontend.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Frontend.Controllers
{
    public class VehiclesPublicController : Controller
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public VehiclesPublicController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _http = httpClientFactory.CreateClient();
            _http.BaseAddress = new Uri(config["BackendApi:BaseUrl"]);
            _config = config;
        }

        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
        {
            List<Vehicle> vehicles = new();
            int total = 0;

            var url = $"api/vehicles?q={q}&page={page}&pageSize={pageSize}";
            var response = await _http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                total = doc.RootElement.GetProperty("total").GetInt32();
                vehicles = doc.RootElement.GetProperty("data").Deserialize<List<Vehicle>>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<Vehicle>();
            }

            ViewBag.Query = q;
            ViewBag.Total = total;
            return View(vehicles);
        }
    }
}
