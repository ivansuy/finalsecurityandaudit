using Frontend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QRCoder;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Frontend.Controllers
{
    public class AccountController : Controller
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public AccountController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _http = httpClientFactory.CreateClient();
            _http.BaseAddress = new Uri(config["BackendApi:BaseUrl"]);
            _config = config;
        }

        // ---------- LOGIN ----------
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var json = JsonSerializer.Serialize(model);
            var resp = await _http.PostAsync("api/auth/login",
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Credenciales inválidas.");
                return View(model);
            }

            var payload = await resp.Content.ReadAsStringAsync();
            var login = JsonSerializer.Deserialize<LoginResponse>(payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (login == null)
            {
                ModelState.AddModelError("", "Error de autenticación.");
                return View(model);
            }

            if (login.RequiresMfa)
            {
                // Pedimos OTP en una vista aparte
                var vm = new MfaViewModel { Email = model.Email, Password = model.Password };
                return View("Mfa", vm);
            }

            if (string.IsNullOrEmpty(login.Token))
            {
                ModelState.AddModelError("", login.Message ?? "Error de autenticación.");
                return View(model);
            }

            HttpContext.Session.SetString("JwtToken", login.Token);
            return RedirectToAction("Administrar", "Vehicles"); // o donde prefieras
        }

        // ---------- MFA durante LOGIN ----------
        [HttpPost]
        public async Task<IActionResult> Mfa(MfaViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var otp = (model.OtpCode ?? "").Trim().Replace(" ", "");
            if (otp.Length != 6 || !otp.All(char.IsDigit))
            {
                ModelState.AddModelError("", "El código debe tener 6 dígitos.");
                return View(model);
            }

            var body = JsonSerializer.Serialize(new
            {
                email = model.Email,
                password = model.Password,
                otpCode = otp
            });

            var resp = await _http.PostAsync("api/auth/login",
                new StringContent(body, Encoding.UTF8, "application/json"));

            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                // muestra el texto real (por ejemplo "Código MFA inválido" o "Bloqueado temporalmente")
                ModelState.AddModelError("", string.IsNullOrWhiteSpace(raw) ? "OTP inválido." : raw);
                return View(model);
            }

            var login = JsonSerializer.Deserialize<LoginResponse>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (login == null || string.IsNullOrEmpty(login.Token))
            {
                ModelState.AddModelError("", login?.Message ?? "Error de autenticación.");
                return View(model);
            }

            HttpContext.Session.SetString("JwtToken", login.Token);
            return RedirectToAction("Administrar", "Vehicles");
        }


        // ---------- LOGOUT ----------
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ---------- HABILITAR MFA (requiere token) ----------
        [HttpGet]
        public async Task<IActionResult> EnableMfa()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _http.PostAsync("api/auth/enable-mfa",
                new StringContent("{}", Encoding.UTF8, "application/json"));

            // 👉 Diferenciamos por código de estado
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return RedirectToAction("Login");

            if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // El backend te devuelve esto cuando ya hay MFA habilitado
                var txt = await resp.Content.ReadAsStringAsync();
                TempData["Ok"] = string.IsNullOrWhiteSpace(txt)
                    ? "Ya cuentas con MFA configurado."
                    : txt; // se muestra en VERDE
                return View(model: null);
            }

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo iniciar la habilitación de MFA.";
                return View(model: null);
            }

            var json = await resp.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<EnableMfaResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null)
            {
                TempData["Error"] = "Respuesta inválida del servidor.";
                return View(model: null);
            }

            // Generar QR
            string base64Png;
            using var qrGen = new QRCoder.QRCodeGenerator();
            using var qrData = qrGen.CreateQrCode(data.OtpauthUri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrData);
            base64Png = Convert.ToBase64String(qrCode.GetGraphic(10));

            ViewBag.QrBase64 = $"data:image/png;base64,{base64Png}";
            ViewBag.ManualKey = data.ManualKey;
            return View(); // EnableMfa.cshtml
        }

        // ---------- VERIFICAR MFA (requiere token) ----------
        [HttpPost]
        public async Task<IActionResult> VerifyMfa(string otpCode)
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var otp = (otpCode ?? "").Trim().Replace(" ", "");
            if (otp.Length != 6 || !otp.All(char.IsDigit))
            {
                TempData["Error"] = "El código debe tener 6 dígitos.";
                return RedirectToAction("EnableMfa");
            }

            var body = JsonSerializer.Serialize(otp); // "123456"

            var resp = await _http.PostAsync("api/auth/verify-mfa",
                new StringContent(body, Encoding.UTF8, "application/json"));

            var raw = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                TempData["Ok"] = "MFA habilitado correctamente.";
            else
                TempData["Error"] = string.IsNullOrWhiteSpace(raw) ? "Código inválido" : raw;

            return RedirectToAction("EnableMfa");
        }

    }
}
