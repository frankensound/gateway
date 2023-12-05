using Gateway.Models;
using Gateway.services;
using Gateway.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Swashbuckle.AspNetCore.Filters;
using System.Text;

namespace gateway
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureServices(builder);

            var app = builder.Build();

            MigrateDatabase(app);

            await InitializeRolesAsync(app);

            ConfigureMiddleware(app);

            app.Run();
        }

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            ConfigureSwagger(builder);

            ConfigureCache(builder);

            // Enable controllers
            builder.Services.AddControllers();

            ConfigureDatabase(builder);

            // Authentication and authorization configuration
            ConfigureSecurity(builder);

            // Reverse proxy configuration
            ConfigureReverseProxy(builder);

            // Register TokenBlacklistService
            builder.Services.AddScoped<TokenBlacklistService>();
        }

        private static async Task InitializeRolesAsync(WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                string[] roleNames = { "Admin", "User", "Manager" };
                foreach (var roleName in roleNames)
                {
                    var roleExist = await roleManager.RoleExistsAsync(roleName);
                    if (!roleExist)
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }
            }
        }

        private static void MigrateDatabase(WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                dbContext.Database.Migrate();
            }
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));

            app.UseAuthentication();
            app.UseRouting();
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
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

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

        private static void ConfigureCache(WebApplicationBuilder builder)
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                var redisConfig = builder.Configuration.GetSection("Redis")["ConnectionString"];
                options.Configuration = redisConfig;
            });
        }

        private static void ConfigureDatabase(WebApplicationBuilder builder)
        {
            builder.Services.AddDbContext<DataContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
        }

        private static void ConfigureSecurity(WebApplicationBuilder builder)
        {
            builder.Services.AddIdentity<User, IdentityRole>().AddEntityFrameworkStores<DataContext>();

            var secretKey = builder.Configuration.GetSection("JwtSettings")["SecretKey"];

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var tokenBlacklistService = context.HttpContext.RequestServices.GetRequiredService<TokenBlacklistService>();

                        // Extract the token from the Authorization header
                        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                        if (authHeader == null)
                        {
                            context.Fail("Authorization header missing.");
                            return;
                        }
                        var token = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader;

                        if (await tokenBlacklistService.IsTokenBlacklistedAsync(token))
                        {
                            // Reject the token if it's blacklisted
                            context.Fail("This token has been blacklisted.");
                        }
                    }
                };
            });

            builder.Services.AddAuthorization();
        }

        private static void ConfigureReverseProxy(WebApplicationBuilder builder)
        {
            builder.Services.AddReverseProxy()
                   .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
        }
    }
}
