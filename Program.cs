using gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;

namespace gateway
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

            var auth0Domain = builder.Configuration["Auth0:Domain"];
            var auth0ManagementApiAccessToken = builder.Configuration["Auth0:ManagementApiAccessToken"];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy.WithOrigins("http://localhost:4000")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            builder.Services.AddSingleton(new Auth0ManagementService(auth0Domain, auth0ManagementApiAccessToken));

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
                    Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
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
                   .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
        }
    }
}