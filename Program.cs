using ProjectPlanning.Web.Services;
using ProjectPlanning.DTOs;
using ProjectPlanning.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- MVC Controllers + Views ---
builder.Services.AddControllersWithViews();

// --- Bonita Configuration ---
builder.Services.Configure<BonitaConfig>(
    builder.Configuration.GetSection("Bonita"));
builder.Services.AddHttpClient<IBonitaApiService, BonitaApiService>();

// --- Database ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- JWT Service ---
builder.Services.AddScoped<IJwtService, JwtService>();

// --- JWT Authentication ---
var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"]!)
            )
        };
    });

// --- Build the App ---
var app = builder.Build();

// --- Error Handling ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// --- Middleware ---
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// --- Default Route ---
// Redirige automáticamente a /AuthView/Register si se entra a la raíz
app.MapGet("/", context =>
{
    context.Response.Redirect("/AuthView/Register");
    return Task.CompletedTask;
});

// --- MVC Routes ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuthView}/{action=Register}/{id?}");

// --- Run ---
app.Run();
