-- Eliminar datos existentes
DELETE FROM "Observations";
DELETE FROM "Resources";
DELETE FROM "Projects";

-- Crear dos proyectos nuevos con fecha 10 de octubre de 2025
INSERT INTO "Projects" ("Name", "StartDate", "EndDate", "CreatorEmail") 
VALUES 
('Construcción de Escuela Rural', '2025-10-10', '2026-03-15', 'walter.bates'),
('Programa de Alimentación Infantil', '2025-10-10', '2026-02-28', 'walter.bates');

-- Agregar recursos pendientes (sin ofrecer) al primer proyecto
INSERT INTO "Resources" ("ProjectId", "Name", "State", "ContactEmail") 
SELECT "Id", 'Materiales de Construcción', 'pending', NULL 
FROM "Projects" WHERE "Name" = 'Construcción de Escuela Rural';

INSERT INTO "Resources" ("ProjectId", "Name", "State", "ContactEmail") 
SELECT "Id", 'Mano de Obra Voluntaria', 'pending', NULL 
FROM "Projects" WHERE "Name" = 'Construcción de Escuela Rural';

-- Agregar recursos pendientes (sin ofrecer) al segundo proyecto
INSERT INTO "Resources" ("ProjectId", "Name", "State", "ContactEmail") 
SELECT "Id", 'Alimentos No Perecederos', 'pending', NULL 
FROM "Projects" WHERE "Name" = 'Programa de Alimentación Infantil';

INSERT INTO "Resources" ("ProjectId", "Name", "State", "ContactEmail") 
SELECT "Id", 'Nutricionistas Voluntarios', 'pending', NULL 
FROM "Projects" WHERE "Name" = 'Programa de Alimentación Infantil';

-- Mostrar resultado
SELECT 'Proyectos creados exitosamente' as resultado;
SELECT * FROM "Projects";
SELECT * FROM "Resources";
