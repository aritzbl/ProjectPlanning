using ProjectPlanning.Web.Services;
using ProjectPlanning.Web.Data;
using Microsoft.EntityFrameworkCore;
using ProjectPlanning.DTOs;

var builder = WebApplication.CreateBuilder(args);

// --- Controladores (API y vistas) ---
builder.Services.AddControllersWithViews();

// --- Servicios HTTP para Bonita ---
builder.Services.AddHttpClient<BonitaLoginService>();
builder.Services.AddHttpClient<IBonitaApiService, BonitaApiService>();

// --- Base de datos ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Configuración Bonita ---
builder.Services.Configure<BonitaConfig>(
    builder.Configuration.GetSection("Bonita"));

// --- Build app ---
var app = builder.Build();

// --- Error handling ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// --- Middleware ---
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// --- Rutas por defecto (Login) ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuthView}/{action=Login}/{id?}");

app.MapControllers();

// --- Run app ---
app.Run();