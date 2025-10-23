using Frontend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Frontend.Controllers
{
    public class DashboardController : Controller
    {
        private readonly HttpClient _http;

        public DashboardController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _http = httpClientFactory.CreateClient();
            _http.BaseAddress = new Uri(config["BackendApi:BaseUrl"]);
        }

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

        public async Task<IActionResult> Index()
        {
            var viewModel = new DashboardViewModel();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var summaryResponse = await _http.GetAsync("api/dashboard/summary");
            if (summaryResponse.IsSuccessStatusCode)
            {
                var json = await summaryResponse.Content.ReadAsStringAsync();
                viewModel.Summary = JsonSerializer.Deserialize<DashboardSummary>(json, options);
            }

            var detectionResponse = await _http.GetAsync("api/dashboard/anomalies");
            if (detectionResponse.IsSuccessStatusCode)
            {
                var json = await detectionResponse.Content.ReadAsStringAsync();
                viewModel.Detection = JsonSerializer.Deserialize<MlDetectionDashboard>(json, options);
            }

            return View(viewModel);
        }
    }
}
