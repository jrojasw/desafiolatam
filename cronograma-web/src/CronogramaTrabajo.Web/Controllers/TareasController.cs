using CronogramaTrabajo.Web.Data;
using CronogramaTrabajo.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CronogramaTrabajo.Web.Controllers;

public class TareasController : Controller
{
    private readonly CronogramaContext _context;

    public TareasController(CronogramaContext context)
    {
        _context = context;
    }

    // GET: Tareas
    public async Task<IActionResult> Index(EstadoTarea? estado)
    {
        var query = _context.Tareas.AsQueryable();

        if (estado.HasValue)
        {
            query = query.Where(t => t.Estado == estado.Value);
        }

        ViewBag.EstadoSeleccionado = estado;

        var tareas = await query
            .OrderBy(t => t.FechaInicio)
            .ToListAsync();

        return View(tareas);
    }

    // GET: Tareas/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var tarea = await _context.Tareas.FirstOrDefaultAsync(t => t.Id == id);
        if (tarea is null)
        {
            return NotFound();
        }

        return View(tarea);
    }

    // GET: Tareas/Create
    public IActionResult Create()
    {
        return View(new Tarea());
    }

    // POST: Tareas/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Titulo,Descripcion,Responsable,FechaInicio,FechaFin,Estado,Prioridad")] Tarea tarea)
    {
        if (ModelState.IsValid)
        {
            _context.Add(tarea);
            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Tarea creada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        return View(tarea);
    }

    // GET: Tareas/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var tarea = await _context.Tareas.FindAsync(id);
        if (tarea is null)
        {
            return NotFound();
        }

        return View(tarea);
    }

    // POST: Tareas/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,Titulo,Descripcion,Responsable,FechaInicio,FechaFin,Estado,Prioridad")] Tarea tarea)
    {
        if (id != tarea.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(tarea);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "Tarea actualizada correctamente.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Tareas.AnyAsync(t => t.Id == tarea.Id))
                {
                    return NotFound();
                }
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        return View(tarea);
    }

    // GET: Tareas/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var tarea = await _context.Tareas.FirstOrDefaultAsync(t => t.Id == id);
        if (tarea is null)
        {
            return NotFound();
        }

        return View(tarea);
    }

    // POST: Tareas/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var tarea = await _context.Tareas.FindAsync(id);
        if (tarea is not null)
        {
            _context.Tareas.Remove(tarea);
            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Tarea eliminada correctamente.";
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: Tareas/Calendario
    public IActionResult Calendario()
    {
        return View();
    }

    // GET: Tareas/Eventos  (JSON para el calendario)
    public async Task<IActionResult> Eventos()
    {
        var tareas = await _context.Tareas.ToListAsync();

        var eventos = tareas.Select(t => new
        {
            id = t.Id,
            title = t.Titulo,
            start = t.FechaInicio.ToString("yyyy-MM-dd"),
            end = t.FechaFin.AddDays(1).ToString("yyyy-MM-dd"), // FullCalendar: end exclusivo
            color = ColorPorEstado(t.Estado),
            url = Url.Action(nameof(Details), new { id = t.Id }),
            extendedProps = new
            {
                estado = t.Estado.ToString(),
                responsable = t.Responsable,
                prioridad = t.Prioridad.ToString()
            }
        });

        return Json(eventos);
    }

    private static string ColorPorEstado(EstadoTarea estado) => estado switch
    {
        EstadoTarea.Pendiente => "#6c757d",
        EstadoTarea.EnProgreso => "#0d6efd",
        EstadoTarea.Completada => "#198754",
        EstadoTarea.Atrasada => "#dc3545",
        EstadoTarea.Cancelada => "#adb5bd",
        _ => "#0d6efd"
    };
}
