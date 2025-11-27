// Program.cs
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Siestur.Data;
using Siestur.Services;
using Siestur.Services.Hubs;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// === ENV ===
Env.Load();

// === DB ===
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Falta ConnectionStrings__DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// === Controllers ===
builder.Services.AddControllers();

// === SignalR ===
builder.Services.AddSignalR();

builder.Services.AddScoped<IDayResetService, DayResetService>();
builder.Services.AddHostedService<DailyResetHostedService>();

// === JWT ===
builder.Services.AddScoped<ITokenService, TokenService>();
var jwtKey = Environment.GetEnvironmentVariable("Jwt__Key")
    ?? throw new InvalidOperationException("Falta Jwt__Key");
var jwtIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer")
    ?? throw new InvalidOperationException("Falta Jwt__Issuer");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// === Swagger ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SIESTUR API", Version = "v1" });

    // Soporte a DateOnly/TimeOnly
    c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
    c.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Format = "time" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Add your JWT token",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
});

// === CORS ===
var allowedOrigins = (Environment.GetEnvironmentVariable("AllowedOrigins")
                     ?? "http://localhost:5173")
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontends", p =>
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .WithExposedHeaders("Content-Disposition")
         .AllowCredentials());
});

var app = builder.Build();

// === Pipeline ===
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/swagger/v1/swagger.json", "SIESTUR API v1");
        ui.RoutePrefix = "swagger"; // /swagger
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("Frontends");
app.UseAuthentication();
app.UseAuthorization();

// ❌ Quitar el catch-all de OPTIONS que causaba 405
// app.MapMethods("{*path}", new[] { "OPTIONS" }, () => Results.Ok());

// === Hubs ===
app.MapHub<TurnsHub>("/hubs/turns");
app.MapHub<WindowsHub>("/hubs/windows");
app.MapHub<VideosHub>("/hubs/videos");

// === Controllers ===
app.MapControllers();

app.Run();
