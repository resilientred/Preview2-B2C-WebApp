using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Preview2_B2C_WebApp.Models;

namespace Preview2_B2C_WebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly AzureAdB2COptions _azureOptions;
        private readonly IMemoryCache _cache;

        public HomeController(IOptions<AzureAdB2COptions> azureAdB2COptions, IMemoryCache cache)
        {
            _azureOptions = azureAdB2COptions.Value;
            _cache = cache;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Authorize]
        public async Task<IActionResult> Api()
        {
            string responseString = "";
            try
            {
                var signedInUserID = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
                var accessToken = _cache.Get<string>(signedInUserID);

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, _azureOptions.ApiUrl);

                    // Add token to the Authorization header and make the request
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await client.SendAsync(request);

                    // Handle the response
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            responseString = await response.Content.ReadAsStringAsync();
                            break;
                        case HttpStatusCode.Unauthorized:
                            responseString = $"Please sign in again. {response.ReasonPhrase}";
                            break;
                        default:
                            responseString = $"Error calling API. StatusCode=${response.StatusCode}";
                            break;
                    }
                }
            }
            catch (MsalUiRequiredException ex)
            {
                responseString = $"Session has expired. Please sign in again. {ex.Message}";
            }
            catch (Exception ex)
            {
                responseString = $"Error calling API: {ex.Message}";
            }

            ViewData["Payload"] = $"{responseString}";
            return View();
        }
    }
}
