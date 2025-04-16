using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor.Services;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Wsla.ControlPanel
{
    public class ControlPanel
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddMudServices();

            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp =>
            {
                var client = new HttpClient
                {
                    //BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
                    BaseAddress = new Uri($"http://localhost"),
                };

                client.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));

                return client;
            });

            await builder.Build().RunAsync();
        }
    }
}