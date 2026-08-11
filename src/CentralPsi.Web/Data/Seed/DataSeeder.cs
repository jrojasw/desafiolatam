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

        // These six slides are managed entirely through this seeder now (not through admin uploads), so every
        // startup forces ImagePath back to the known-good file checked into wwwroot/images/seed. This is
        // deliberately unconditional: a slide's ImagePath can end up pointing at /uploads/slides/<guid> if it
        // was ever edited from the admin panel, and uploaded files live on Render's ephemeral disk - they
        // vanish on the next deploy/restart, leaving a broken image. Converging here every boot is what fixes
        // that permanently instead of only healing it once.
        async Task<SlideImage> UpsertManagedSlideAsync(
            string title, string imagePath, string subtitle, string? buttonText, string? buttonUrl, int sortOrder, string? matchAlsoTitle = null)
        {
            var slide = await db.SlideImages.FirstOrDefaultAsync(s => s.Title == title || s.Title == matchAlsoTitle);
            if (slide is null)
            {
                slide = new SlideImage { Title = title, SortOrder = sortOrder };
                db.SlideImages.Add(slide);
            }

            slide.Title = title;
            slide.ImagePath = imagePath;
            slide.Subtitle = subtitle;
            slide.ButtonText = buttonText;
            slide.ButtonUrl = buttonUrl;
            return slide;
        }

        await UpsertManagedSlideAsync(
            "Tu bienestar emocional, un paso a la vez",
            "/images/seed/slide-1-bienestar.jpg",
            "Sin apuros ni exigencias: avanza a tu propio ritmo con el acompañamiento de un profesional que se ajusta a tu proceso.",
            null, null, 1);

        await UpsertManagedSlideAsync(
            "Encuentra al profesional ideal para ti",
            "/images/seed/slide-6-pacientes.jpg",
            "Agenda tu hora desde donde estés con excelentes profesionales de la psicología, validados ante la Superintendencia de Salud.",
            "Ver profesionales", "/profesionales", 1);

        await UpsertManagedSlideAsync(
            "El bienestar de tu familia, primero",
            "/images/seed/slide-3-familia.jpg",
            "Padres, hijos, hermanos, abuelos o quien compone tu familia: encuentra profesionales especializados en terapia familiar y sistémica para fortalecer la comunicación y el vínculo.",
            "Ver profesionales", "/profesionales", 2,
            matchAlsoTitle: "Profesionales acreditados por el Minsal");

        await UpsertManagedSlideAsync(
            "Agenda cuando tú lo necesites",
            "/images/seed/slide-4-agenda.jpg",
            "Reserva tu hora online, a la hora del día que te acomode, sin listas de espera ni llamadas telefónicas.",
            null, null, 3);

        await UpsertManagedSlideAsync(
            "Un espacio seguro para hablar y avanzar",
            "/images/seed/slide-espacio-seguro.jpg",
            "Confidencial, sin juicios y 100% online: un lugar para expresarte con libertad y trabajar en lo que te importa.",
            null, null, 4);

        await UpsertManagedSlideAsync(
            "Súmate a CentralPsi y trabaja con estabilidad",
            "/images/seed/slide-5-profesionales.jpg",
            "Invitamos a psicólogos y psicólogas de todas las edades y géneros a inscribirse: agenda tus propias horas, genera ingresos estables y sigue ayudando a más personas.",
            "Inscríbete gratis", "/profesionales/inscripcion", 0);

        if (!await db.NewsArticles.AnyAsync())
        {
            db.NewsArticles.AddRange(
                new NewsArticle
                {
                    Title = "5 señales de que podrías beneficiarte de terapia",
                    Summary = "Reconocer el momento adecuado para pedir ayuda es un acto de autocuidado, no una debilidad.",
                    Content = "Cambios en el sueño, irritabilidad persistente, dificultad para concentrarte, aislamiento social y agotamiento emocional son señales frecuentes. Consultar con un profesional a tiempo puede marcar una gran diferencia.",
                    Category = NewsCategory.Consejo,
                    ImagePath = "/images/seed/news-senales-terapia.svg"
                },
                new NewsArticle
                {
                    Title = "La OMS destaca el aumento de la ansiedad a nivel mundial",
                    Summary = "Organismos de salud reportan un alza sostenida en trastornos de ansiedad y depresión desde 2020.",
                    Content = "Diversos estudios epidemiológicos muestran que la ansiedad y la depresión se encuentran entre las principales causas de discapacidad a nivel global, reforzando la importancia del acceso a atención psicológica oportuna.",
                    Category = NewsCategory.Noticia,
                    ImagePath = "/images/seed/news-ansiedad-mundial.svg"
                },
                new NewsArticle
                {
                    Title = "Respiración 4-7-8: una técnica simple para bajar la ansiedad",
                    Summary = "Un ejercicio de respiración que puedes practicar en cualquier momento del día.",
                    Content = "Inhala por 4 segundos, sostén el aire por 7 segundos y exhala lentamente por 8 segundos. Repetir este ciclo 4 veces ayuda a activar el sistema nervioso parasimpático y reducir la sensación de estrés agudo.",
                    Category = NewsCategory.Tip,
                    ImagePath = "/images/seed/news-respiracion-478.svg"
                },
                new NewsArticle
                {
                    Title = "Estudio: la terapia online es tan efectiva como la presencial",
                    Summary = "Una revisión de múltiples estudios confirma la efectividad de la psicoterapia por videollamada.",
                    Content = "Metaanálisis recientes muestran que, para gran parte de los motivos de consulta, la terapia realizada por videollamada logra resultados clínicos comparables a la atención presencial, con la ventaja de una mayor accesibilidad.",
                    Category = NewsCategory.EstudioCientifico,
                    ImagePath = "/images/seed/news-terapia-online-estudio.svg"
                }
            );
        }
        else
        {
            // Backfills the illustration on rows that predate this field, matched by Title. Only fires when
            // ImagePath is still empty, so it never overwrites a real photo an admin has since uploaded.
            var seedImageByTitle = new Dictionary<string, string>
            {
                ["5 señales de que podrías beneficiarte de terapia"] = "/images/seed/news-senales-terapia.svg",
                ["La OMS destaca el aumento de la ansiedad a nivel mundial"] = "/images/seed/news-ansiedad-mundial.svg",
                ["Respiración 4-7-8: una técnica simple para bajar la ansiedad"] = "/images/seed/news-respiracion-478.svg",
                ["Estudio: la terapia online es tan efectiva como la presencial"] = "/images/seed/news-terapia-online-estudio.svg"
            };
            var articlesMissingImage = await db.NewsArticles
                .Where(n => string.IsNullOrEmpty(n.ImagePath) && seedImageByTitle.Keys.Contains(n.Title))
                .ToListAsync();
            foreach (var article in articlesMissingImage)
            {
                article.ImagePath = seedImageByTitle[article.Title];
            }
        }

        await db.SaveChangesAsync();
    }
}
