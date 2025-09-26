using Frontend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Frontend.Controllers
{
    public class UsersController : Controller
    {
        private readonly HttpClient _http;

        public UsersController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _http = httpClientFactory.CreateClient();
            _http.BaseAddress = new Uri(config["BackendApi:BaseUrl"]);
        }

        // 🔐 Requiere sesión (el backend valida rol SuperAdmin)
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var token = context.HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            base.OnActionExecuting(context);
        }

        // GET /Users/Index → lista usuarios
        public async Task<IActionResult> Index()
        {
            var users = new List<UserSummary>();

            var resp = await _http.GetAsync("api/users");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                users = JsonSerializer.Deserialize<List<UserSummary>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            else if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                TempData["Error"] = "No tienes permisos para ver usuarios (requiere SuperAdmin).";
            }
            else if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }
            else
            {
                TempData["Error"] = "No se pudo cargar la lista de usuarios.";
            }

            return View(users);
        }

        // POST /Users/Create → crea usuario (email, password, role)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserRequest model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Datos inválidos para crear usuario.";
                return RedirectToAction(nameof(Index));
            }

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(model, options);

            var resp = await _http.PostAsync("api/users",
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (resp.IsSuccessStatusCode)
            {
                TempData["Ok"] = $"Usuario {model.Email} creado.";
            }
            else if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                TempData["Error"] = "No tienes permisos para crear usuarios (requiere SuperAdmin).";
            }
            else if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var err = await resp.Content.ReadAsStringAsync();
                TempData["Error"] = $"Validación falló: {err}";
            }
            else
            {
                TempData["Error"] = "Error al crear usuario.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /Users/Delete → elimina por id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Id inválido.";
                return RedirectToAction(nameof(Index));
            }

            var resp = await _http.DeleteAsync($"api/users/{id}");

            if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                TempData["Ok"] = "Usuario eliminado.";
            }
            else if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                TempData["Error"] = "No tienes permisos para eliminar usuarios (requiere SuperAdmin).";
            }
            else if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "Usuario no encontrado.";
            }
            else
            {
                TempData["Error"] = "Error al eliminar usuario.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
