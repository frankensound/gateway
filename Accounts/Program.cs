using Accounts.Interfaces;
using Accounts.Services;
using Microsoft.OpenApi.Models;
using Prometheus;

namespace Accounts
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureServices(builder);

            var app = builder.Build();

            ConfigureMiddleware(app);

            app.Run();
        }

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddSingleton<IAuth0ManagementService, MockAuth0ManagementService>();
            }
            else
            {
                var auth0Domain = builder.Configuration["Auth0:Domain"];
                var auth0ManagementApiAccessToken = builder.Configuration["Auth0:ManagementApiAccessToken"];

                builder.Services.AddSingleton<IAuth0ManagementService>(new Auth0ManagementService(auth0Domain, auth0ManagementApiAccessToken));
            }

            builder.Services.AddSingleton<IMessagePublisher, RabbitMQClientService>();

            ConfigureSwagger(builder);

            // Enable controllers
            builder.Services.AddControllers();

            builder.Services.AddHttpContextAccessor();
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Accounts microservice"));

            app.UseRouting();

            // Enable HTTP metrics for Prometheus
            app.UseHttpMetrics();

            app.MapControllers();

            // Map metrics endpoint
            app.MapMetrics();
        }

        private static void ConfigureSwagger(WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Accounts", Version = "v1" });
            });
        }
    }
}