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

        // Dropped from the lineup - kept only 3 articles, each with a real photo now (see the upserts below).
        var droppedArticle = await db.NewsArticles
            .FirstOrDefaultAsync(n => n.Title == "5 señales de que podrías beneficiarte de terapia");
        if (droppedArticle is not null)
        {
            db.NewsArticles.Remove(droppedArticle);
        }

        async Task UpsertNewsArticleAsync(string title, string summary, string content, NewsCategory category, string imagePath)
        {
            var article = await db.NewsArticles.FirstOrDefaultAsync(n => n.Title == title);
            if (article is null)
            {
                article = new NewsArticle { Title = title };
                db.NewsArticles.Add(article);
            }

            article.Summary = summary;
            article.Content = content;
            article.Category = category;
            article.ImagePath = imagePath;
        }

        await UpsertNewsArticleAsync(
            "La OMS destaca el aumento de la ansiedad a nivel mundial",
            "Organismos de salud reportan un alza sostenida en trastornos de ansiedad y depresión desde 2020.",
            "Diversos estudios epidemiológicos muestran que la ansiedad y la depresión se encuentran entre las principales causas de discapacidad a nivel global, reforzando la importancia del acceso a atención psicológica oportuna.",
            NewsCategory.Noticia,
            "/images/seed/news-ansiedad-mundial.jpg");

        await UpsertNewsArticleAsync(
            "Respiración 4-7-8: una técnica simple para bajar la ansiedad",
            "Un ejercicio de respiración que puedes practicar en cualquier momento del día.",
            "Inhala por 4 segundos, sostén el aire por 7 segundos y exhala lentamente por 8 segundos. Repetir este ciclo 4 veces ayuda a activar el sistema nervioso parasimpático y reducir la sensación de estrés agudo.",
            NewsCategory.Tip,
            "/images/seed/news-respiracion-478.jpg");

        await UpsertNewsArticleAsync(
            "Estudio: la terapia online es tan efectiva como la presencial",
            "Una revisión de múltiples estudios confirma la efectividad de la psicoterapia por videollamada.",
            "Metaanálisis recientes muestran que, para gran parte de los motivos de consulta, la terapia realizada por videollamada logra resultados clínicos comparables a la atención presencial, con la ventaja de una mayor accesibilidad.",
            NewsCategory.EstudioCientifico,
            "/images/seed/news-terapia-online-estudio.jpg");

        await db.SaveChangesAsync();
    }
}
