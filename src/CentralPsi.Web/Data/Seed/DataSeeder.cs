using CentralPsi.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Data.Seed;

public static class DataSeeder
{
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRole));
        }

        var config = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = config["Seed:AdminEmail"] ?? "admin@centralpsi.cl";
        var adminPassword = config["Seed:AdminPassword"] ?? "CambiarAhora!2026";

        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Administrador CentralPsi"
            };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, AdminRole);
                logger.LogWarning(
                    "Se creó el usuario administrador {Email} con la contraseña de config Seed:AdminPassword. Cámbiala de inmediato desde el panel.",
                    adminEmail);
            }
            else
            {
                // Surface *why* it failed instead of silently leaving no admin account at all - the most
                // common cause is Seed:AdminPassword not meeting Identity's default policy (10+ chars, at
                // least one uppercase, one lowercase and one digit).
                logger.LogError(
                    "No se pudo crear el usuario administrador {Email}: {Errors}",
                    adminEmail,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
        else if (!await userManager.IsInRoleAsync(existingAdmin, AdminRole))
        {
            await userManager.AddToRoleAsync(existingAdmin, AdminRole);
        }

        if (!await db.SlideImages.AnyAsync())
        {
            db.SlideImages.AddRange(
                new SlideImage { ImagePath = "/images/seed/slide-1.svg", Title = "Tu bienestar emocional, un paso a la vez", SortOrder = 1 },
                new SlideImage { ImagePath = "/images/seed/slide-2.svg", Title = "Profesionales acreditados por el Minsal", SortOrder = 2 },
                new SlideImage { ImagePath = "/images/seed/slide-3.svg", Title = "Agenda cuando tú lo necesites", SortOrder = 3 },
                new SlideImage { ImagePath = "/images/seed/slide-4.svg", Title = "Un espacio seguro para hablar y avanzar", SortOrder = 4 }
            );
        }

        // Runs on every startup (unlike the block above, which only fires for a brand-new database) so this
        // photo also reaches the row that was seeded with the original abstract-gradient placeholder.
        var bienestarSlide = await db.SlideImages.FirstOrDefaultAsync(s => s.Title == "Tu bienestar emocional, un paso a la vez");
        if (bienestarSlide is not null && bienestarSlide.ImagePath == "/images/seed/slide-1.svg")
        {
            bienestarSlide.ImagePath = "/images/seed/slide-1-bienestar.jpg";
        }

        // Repurposes the old "Profesionales acreditados por el Minsal" slide into a family-wellbeing/systemic
        // content slide. Matches by either the old or new title so it's idempotent and only touches this once.
        const string familySlideTitle = "El bienestar de tu familia, primero";
        var familySlide = await db.SlideImages.FirstOrDefaultAsync(s =>
            s.Title == "Profesionales acreditados por el Minsal" || s.Title == familySlideTitle);
        if (familySlide is not null)
        {
            familySlide.Title = familySlideTitle;
            familySlide.Subtitle = "Padres, hijos, hermanos, abuelos o quien compone tu familia: encuentra profesionales especializados en terapia familiar y sistémica para fortalecer la comunicación y el vínculo.";
            familySlide.ButtonText = "Ver profesionales";
            familySlide.ButtonUrl = "/profesionales";
            if (familySlide.ImagePath == "/images/seed/slide-2.svg")
            {
                familySlide.ImagePath = "/images/seed/slide-3-familia.jpg";
            }
        }

        // Runs on every startup (unlike the block above, which only fires for a brand-new database) so this
        // slide also reaches databases that were seeded before it existed, matched by Title so it's never duplicated.
        const string recruitmentSlideTitle = "Súmate a CentralPsi y trabaja con estabilidad";
        const string recruitmentSlideImage = "/images/seed/slide-5-profesionales.jpg";
        var recruitmentSlide = await db.SlideImages.FirstOrDefaultAsync(s => s.Title == recruitmentSlideTitle);
        if (recruitmentSlide is null)
        {
            db.SlideImages.Add(new SlideImage
            {
                ImagePath = recruitmentSlideImage,
                Title = recruitmentSlideTitle,
                Subtitle = "Invitamos a psicólogos y psicólogas de todas las edades y géneros a inscribirse: agenda tus propias horas, genera ingresos estables y sigue ayudando a más personas.",
                ButtonText = "Inscríbete gratis",
                ButtonUrl = "/profesionales/inscripcion",
                SortOrder = 0
            });
        }
        else if (recruitmentSlide.ImagePath == "/images/seed/slide-5.svg")
        {
            // An earlier deploy seeded this row with the temporary abstract placeholder; swap it for the
            // real photo now that one exists, without touching anything an admin may have edited since.
            recruitmentSlide.ImagePath = recruitmentSlideImage;
        }

        const string bookingSlideTitle = "Encuentra al profesional ideal para ti";
        if (!await db.SlideImages.AnyAsync(s => s.Title == bookingSlideTitle))
        {
            db.SlideImages.Add(new SlideImage
            {
                ImagePath = "/images/seed/slide-6-pacientes.jpg",
                Title = bookingSlideTitle,
                Subtitle = "Agenda tu hora desde donde estés con excelentes profesionales de la psicología, validados ante la Superintendencia de Salud.",
                ButtonText = "Ver profesionales",
                ButtonUrl = "/profesionales",
                SortOrder = 1
            });
        }

        if (!await db.NewsArticles.AnyAsync())
        {
            db.NewsArticles.AddRange(
                new NewsArticle
                {
                    Title = "5 señales de que podrías beneficiarte de terapia",
                    Summary = "Reconocer el momento adecuado para pedir ayuda es un acto de autocuidado, no una debilidad.",
                    Content = "Cambios en el sueño, irritabilidad persistente, dificultad para concentrarte, aislamiento social y agotamiento emocional son señales frecuentes. Consultar con un profesional a tiempo puede marcar una gran diferencia.",
                    Category = NewsCategory.Consejo
                },
                new NewsArticle
                {
                    Title = "La OMS destaca el aumento de la ansiedad a nivel mundial",
                    Summary = "Organismos de salud reportan un alza sostenida en trastornos de ansiedad y depresión desde 2020.",
                    Content = "Diversos estudios epidemiológicos muestran que la ansiedad y la depresión se encuentran entre las principales causas de discapacidad a nivel global, reforzando la importancia del acceso a atención psicológica oportuna.",
                    Category = NewsCategory.Noticia
                },
                new NewsArticle
                {
                    Title = "Respiración 4-7-8: una técnica simple para bajar la ansiedad",
                    Summary = "Un ejercicio de respiración que puedes practicar en cualquier momento del día.",
                    Content = "Inhala por 4 segundos, sostén el aire por 7 segundos y exhala lentamente por 8 segundos. Repetir este ciclo 4 veces ayuda a activar el sistema nervioso parasimpático y reducir la sensación de estrés agudo.",
                    Category = NewsCategory.Tip
                },
                new NewsArticle
                {
                    Title = "Estudio: la terapia online es tan efectiva como la presencial",
                    Summary = "Una revisión de múltiples estudios confirma la efectividad de la psicoterapia por videollamada.",
                    Content = "Metaanálisis recientes muestran que, para gran parte de los motivos de consulta, la terapia realizada por videollamada logra resultados clínicos comparables a la atención presencial, con la ventaja de una mayor accesibilidad.",
                    Category = NewsCategory.EstudioCientifico
                }
            );
        }

        await db.SaveChangesAsync();
    }
}
