using CradleSoft.DMS.Data;
using CradleSoft.DMS.Models;
using CradleSoft.DMS.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Steeltoe.Discovery.Eureka;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
        || dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
        || dbProvider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase))
    {
        var npgsqlAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(x => x.GetName().Name == "Npgsql.EntityFrameworkCore.PostgreSQL")
            ?? Assembly.Load("Npgsql.EntityFrameworkCore.PostgreSQL");

        var extensionsType = npgsqlAssembly.GetType("Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions")
            ?? throw new InvalidOperationException("Npgsql provider extensions were not found.");

        var useNpgsqlMethod = extensionsType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
            {
                if (m.Name != "UseNpgsql") return false;
                var parameters = m.GetParameters();
                return parameters.Length >= 2
                       && parameters[0].ParameterType == typeof(DbContextOptionsBuilder)
                       && parameters[1].ParameterType == typeof(string);
            })
            ?? throw new InvalidOperationException("Compatible UseNpgsql overload was not found.");

        useNpgsqlMethod.Invoke(null, new object?[] { options, defaultConnection });
        return;
    }

    if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
        || dbProvider.Equals("MSSQL", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(defaultConnection);
        return;
    }

    throw new InvalidOperationException($"Unsupported DatabaseProvider '{dbProvider}'. Use SqlServer or PostgreSql.");
});

builder.Services.AddHealthChecks();

// Configure JWT Authentication
var secretKey = builder.Configuration["JwtSettings:SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey is missing from configuration.");
var tokenValidationParameters = new TokenValidationParameters()
{
    ValidateIssuer = true,
    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],

    ValidateAudience = true,
    ValidAudience = builder.Configuration["JwtSettings:Audience"],

    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),

    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero
};
builder.Services.AddSingleton(tokenValidationParameters);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = tokenValidationParameters;
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Cookies["access_token"];
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddScoped<IStorageService, LocalDiskStorageService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("ETag");
    });
});


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddEurekaDiscoveryClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.MapHealthChecks("/health");

app.Run();
