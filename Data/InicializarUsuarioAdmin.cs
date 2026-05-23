using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Data;

public static class InicializarUsuarioAdmin
{
    public static void CrearAdminSiNoExiste(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            // Verificar si ya existe un usuario admin
            var adminExistente = context.Usuarios
                .FirstOrDefault(u => u.NombreUsuario.ToLower() == "admin");

            var contrasenaHash = PasswordHelper.HashPassword("admin");

            if (adminExistente != null)
            {
                // Si existe, verificar y actualizar la contraseña si es necesario
                if (adminExistente.Contrasena != contrasenaHash)
                {
                    logger.LogInformation("Actualizando contraseña del usuario admin...");
                    adminExistente.Contrasena = contrasenaHash;
                    adminExistente.Activo = true;
                    adminExistente.Rol = "Administrador";
                    context.SaveChanges();
                    logger.LogInformation("Contraseña del usuario admin actualizada. Credenciales: admin/admin");
                }
                else
                {
                    logger.LogInformation("El usuario admin ya existe en la base de datos con la contraseña correcta.");
                }
                return;
            }

            // Crear usuario admin
            var admin = new Usuario
            {
                NombreUsuario = "admin",
                Contrasena = contrasenaHash,
                NombreCompleto = "Administrador del Sistema",
                Rol = "Administrador",
                Activo = true
            };

            context.Usuarios.Add(admin);
            context.SaveChanges();

            logger.LogInformation("Usuario admin creado exitosamente. Credenciales: admin/admin");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al crear el usuario admin en la base de datos.");
            throw; // Re-lanzar para que se vea el error
        }
    }
}

