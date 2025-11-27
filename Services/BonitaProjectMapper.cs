// Services/BonitaProjectMapper.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ProjectPlanning.Web.Models;   
using ProjectPlanning.DTOs;       

namespace ProjectPlanning.Web.Services      
{
    public static class BonitaProjectMapper
    {
        /// <summary>
        /// Mapea un Project de la app a las variables de proceso que espera Bonita.
        /// </summary>
        public static List<BonitaVariable> MapFromProject(Project project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            // Armamos el JSON de pedidos/recursos a partir de project.Resources
            // Ajustá los campos internos según qué estés usando en los conectores de Bonita.
            var pedidosPayload = project.Resources.Select(r => new
            {
                titulo = r.Name,
                descripcion = r.Name,          // si en tu modelo tenés Descripcion, usala acá
                contacto = r.ContactEmail,
                estado = r.State
            });

            var pedidosJson = JsonSerializer.Serialize(pedidosPayload);

            // Ojo: BonitaVariable.Value suele ser string; si tu clase espera object, igual le podés pasar strings.
            return new List<BonitaVariable>
            {
                // idProyecto: entero local de tu app (para que Bonita sepa quién lo disparó)
                new() { Name = "idProyecto", Value = project.Id },

                // projectName: nombre del proyecto
                new() { Name = "projectName", Value = project.Name ?? string.Empty },

                // responsableONG: si tenés un campo tipo CreatorEmail/Responsable, usalo; sino dejalo vacío o
                // traé el mail del usuario logueado más adelante.
                new() { Name = "responsableONG", Value = project.CreatorEmail ?? string.Empty },

                // pedidosJSON: json con la lista de recursos/pedidos
                new() { Name = "pedidosJSON", Value = pedidosJson },

                // Datos básicos del pedido “principal”
                new() { Name = "tituloPedido", Value = project.Name ?? "Proyecto sin título" },
                new() {
                    Name = "descripcionPedido",
                    Value = $"Proyecto {project.Name} creado desde la aplicación .NET"
                },

                // vencimientoPedido: usamos la fecha de fin del proyecto
                new() { Name = "vencimientoPedido", Value = project.EndDate.ToString("yyyy-MM-dd") },

                // etapald: de momento un valor fijo (3), igual al valor por defecto que ya definiste en Bonita
                new() { Name = "etapald", Value = 3 }
            };
        }
    }
}
