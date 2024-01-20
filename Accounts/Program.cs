using Accounts.Interfaces;
using Accounts.Services;
using Accounts.Utils;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog.Events;
using Serilog;

namespace Accounts
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    path: "logs/errors.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30, // Retain logs for 30 days
                    fileSizeLimitBytes: 10_000_000, // 10 MB file size limit
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog();

            ConfigureServices(builder);

            var app = builder.Build();

            ConfigureMiddleware(app);
            try
            {
                var configuration = app.Services.GetService<IConfiguration>();
                var rabbitMQClientService = app.Services.GetService<RabbitMQClientService>();
                if (rabbitMQClientService != null)
                {
                    var queueName = configuration.GetValue<string>("RabbitMQ:QueueName:Data");
                    var cts = new CancellationTokenSource();
                    await Task.Run(() => rabbitMQClientService.Consume(queueName, cts.Token));

                    app.Lifetime.ApplicationStopping.Register(() => cts.Cancel());
                }
                else
                {
                    Console.WriteLine($"RabbitMQ service could not be found.");
                }
                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in setting up RabbitMQ consumer: {ex}");
            }
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
            builder.Services.AddSingleton<MongoDbService>();

            ConfigureSwagger(builder);

            // Enable controllers
            builder.Services.AddControllers();

            builder.Services.AddSingleton<RabbitMQClientService>();

            builder.Services.AddHttpContextAccessor();
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Accounts microservice"));

            app.UseRouting();

            // Enable HTTP metrics for Prometheus
            app.UseHttpMetrics();


            // Custom metrics to measure response time
            app.UseMiddleware<ResponseTimeMiddleware>();

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