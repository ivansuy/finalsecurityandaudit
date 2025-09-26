using Frontend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Frontend.Controllers
{
    public class VehiclesController : Controller
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public VehiclesController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _http = httpClientFactory.CreateClient();
            _http.BaseAddress = new Uri(config["BackendApi:BaseUrl"]);
            _config = config;
        }

        // ✅ Verificación de sesión y token
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var token = context.HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            base.OnActionExecuting(context);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(string brand, string model, int year, decimal price)
        {
            // Creamos el objeto anónimo en camelCase
            var vehicle = new
            {
                brand = brand,
                model = model,
                year = year,
                price = price
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(vehicle, options);

            var response = await _http.PostAsync("api/vehicles",
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction("Index", "VehiclesPublic");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                ViewBag.Error = "No tienes permisos para crear vehículos.";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }
            else
            {
                ViewBag.Error = "Error al crear el vehículo.";
            }

            return View();
        }

        // 👉 LISTAR
        public async Task<IActionResult> Administrar()
        {
            List<Vehicle> vehicles = new();

            var response = await _http.GetAsync("api/vehicles");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                vehicles = doc.RootElement.GetProperty("data").Deserialize<List<Vehicle>>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<Vehicle>();
            }

            return View(vehicles);
        }

        // 👉 EDITAR (POST)
        [HttpPost]
        public async Task<IActionResult> Edit(int id, string brand, string model, int year, decimal price)
        {
            var vehicle = new { brand, model, year, price };

            var json = JsonSerializer.Serialize(vehicle, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var response = await _http.PutAsync($"api/vehicles/{id}",
                new StringContent(json, Encoding.UTF8, "application/json"));

            return RedirectToAction("Administrar");
        }

        // 👉 ELIMINAR (POST)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _http.DeleteAsync($"api/vehicles/{id}");
            return RedirectToAction("Administrar");
        }


    }
}
