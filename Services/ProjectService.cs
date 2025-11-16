using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text; // Para Encoding
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectPlanning.Web.Data;
using ProjectPlanning.Web.Models;
using ProjectPlanning.DTOs; // <- Namespace de tu BonitaConfig

namespace ProjectPlanning.Web.Services
{
    public class ProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly BonitaConfig _config;

        public ProjectService(ApplicationDbContext context, IOptions<BonitaConfig> config, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
            _config = config.Value; // Obtenemos la configuración desde IOptions
        }

        // Obtener proyecto por ID
        public async Task<Project?> GetProjectByIdAsync(int id)
        {
            return await _context.Projects
                .Include(p => p.Resources)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        // Obtener proyectos activos
        public async Task<List<Project>> GetActiveProjectsAsync()
        {
            var nowUtc = DateTime.UtcNow;
            return await _context.Projects
                .Include(p => p.Resources)
                .Where(p => p.StartDate <= nowUtc && p.EndDate >= nowUtc)
                .ToListAsync();
        }

        // Iniciar un proceso en Bonita
        public async Task<string> StartBonitaProcessAsync(int processId)
        {
            var url = $"{_config.BaseUrl}/API/bpm/process/{processId}/instantiation";
            var content = new StringContent("{}", Encoding.UTF8, "application/json");

            var byteArray = Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}