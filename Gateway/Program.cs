using Gateway.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog.Events;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using Yarp.ReverseProxy.Transforms;

namespace Gateway
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
            builder.Logging.AddSerilog(); // Use Serilog for logging

            ConfigureServices(builder);

            var app = builder.Build();

            ConfigureMiddleware(app);

            app.Run();
        }

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var auth0Domain = builder.Configuration["Auth0:Domain"];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy.WithOrigins("http://localhost:4000")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            ConfigureSwagger(builder);

            // Enable controllers
            builder.Services.AddControllers();

            // Authentication and authorization configuration
            ConfigureSecurity(builder);

            // Reverse proxy configuration
            ConfigureReverseProxy(builder);
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Frankensound API Gateway"));

            app.UseAuthentication();

            app.UseRouting();

            app.UseCors("CorsPolicy");

            // Enable HTTP metrics for Prometheus
            app.UseHttpMetrics();

            app.UseAuthorization();

            // Custom metrics to measure response time
            app.UseMiddleware<ResponseTimeMiddleware>();

            app.MapControllers();

            // Map metrics endpoint
            app.MapMetrics();

            app.MapReverseProxy();
        }

        private static void ConfigureSwagger(WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "API Gateway", Version = "v1" });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Enter your token, and 'Bearer' [space] will be added by default as a prefix to the header.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });
        }

        private static void ConfigureSecurity(WebApplicationBuilder builder)
        {
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, MockAuthHandler>(JwtBearerDefaults.AuthenticationScheme, options => { });
            }
            else
            {
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = $"https://{builder.Configuration.GetValue<string>("Auth0:Domain")}/";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = $"https://{builder.Configuration["Auth0:Domain"]}/",
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Auth0:Audience"],
                    };
                });
            }

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("read:songs", policy => policy.
                    RequireAuthenticatedUser().
                    RequireClaim("scope", "read:songs"));
            });
        }

        private static void ConfigureReverseProxy(WebApplicationBuilder builder)
        {
            builder.Services.AddReverseProxy()
                   .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
                   .AddTransforms(builderContext =>
                   {
                       if (!string.IsNullOrEmpty(builderContext.Route.AuthorizationPolicy))
                       {
                           builderContext.AddRequestTransform(async transformContext =>
                           {
                               var authorizationHeader = transformContext.HttpContext.Request.Headers["Authorization"].ToString();
                               if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                               {
                                   var token = authorizationHeader.Substring("Bearer ".Length).Trim();
                                   var handler = new JwtSecurityTokenHandler();
                                   var jwtToken = handler.ReadJwtToken(token);
                                   var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

                                   if (!string.IsNullOrEmpty(userId))
                                   {
                                        transformContext.ProxyRequest.Headers.Add("UserID", userId);
                                        Log.Information($"UserID header set: {userId}");
                                   }
                               }
                           });
                       }
                   });
        }
    }
}