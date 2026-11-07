using System.Globalization;
using System.Text;
using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>
/// Read-only audit report over Appointments/Payments for the last N days - built to answer "how many bookings,
/// in what state, through which provider" without ever pulling a patient's name/email/phone into the response.
/// Every query here is AsNoTracking and SELECT-only; nothing in this controller writes to the database.
/// </summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/ReportePagos")]
public class ReportePagosController : Controller
{
    private static readonly int[] AllowedDayRanges = { 7, 30, 90 };

    private readonly ApplicationDbContext _db;

    public ReportePagosController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int days = 30)
    {
        days = AllowedDayRanges.Contains(days) ? days : 30;
        var sinceUtc = DateTime.UtcNow.AddDays(-days);

        var vm = new ReportePagosViewModel
        {
            Days = days,
            Rows = await GetRowsAsync(sinceUtc),
            Summary = await GetSummaryAsync(sinceUtc),
            TestAppointmentsCount = await GetTestAppointmentsCountAsync(sinceUtc)
        };

        return View(vm);
    }

    [HttpGet("ExportarCitasCsv")]
    public async Task<IActionResult> ExportarCitasCsv(int days = 30)
    {
        days = AllowedDayRanges.Contains(days) ? days : 30;
        var sinceUtc = DateTime.UtcNow.AddDays(-days);
        var rows = await GetRowsAsync(sinceUtc);

        var sb = new StringBuilder();
        sb.AppendLine("appointment_id,fecha_creacion_utc,estado_cita,monto_cita,pasarela,estado_pago,monto_pago,codigo_respuesta,fecha_pago_utc,respuesta_cruda_parcial");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(r.AppointmentIdShort),
                CsvEscape(r.FechaCreacionUtc.ToString("yyyy-MM-dd HH:mm:ss")),
                CsvEscape(r.EstadoCita),
                CsvEscape(r.MontoCita.ToString(CultureInfo.InvariantCulture)),
                CsvEscape(r.Pasarela ?? ""),
                CsvEscape(r.EstadoPago ?? ""),
                CsvEscape(r.MontoPago?.ToString(CultureInfo.InvariantCulture) ?? ""),
                CsvEscape(r.CodigoRespuesta?.ToString(CultureInfo.InvariantCulture) ?? ""),
                CsvEscape(r.FechaPagoUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
                CsvEscape(r.RespuestaCrudaParcial ?? "")));
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"citas-pagos-{days}d.csv");
    }

    [HttpGet("ExportarResumenCsv")]
    public async Task<IActionResult> ExportarResumenCsv(int days = 30)
    {
        days = AllowedDayRanges.Contains(days) ? days : 30;
        var sinceUtc = DateTime.UtcNow.AddDays(-days);
        var summary = await GetSummaryAsync(sinceUtc);

        var sb = new StringBuilder();
        sb.AppendLine("estado_cita,estado_pago,pasarela,total");
        foreach (var s in summary)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(s.EstadoCita),
                CsvEscape(s.EstadoPago ?? ""),
                CsvEscape(s.Pasarela ?? ""),
                CsvEscape(s.Total.ToString(CultureInfo.InvariantCulture))));
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"resumen-pagos-{days}d.csv");
    }

    private async Task<List<ReportePagosRow>> GetRowsAsync(DateTime sinceUtc)
    {
        var queried = await _db.Appointments.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= sinceUtc)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new
            {
                a.Id,
                a.CreatedAtUtc,
                AppointmentStatus = a.Status,
                AppointmentAmount = a.Amount,
                PaymentProvider = a.Payment != null ? a.Payment.Provider : null,
                PaymentStatus = a.Payment != null ? (PaymentStatus?)a.Payment.Status : null,
                PaymentAmount = a.Payment != null ? (decimal?)a.Payment.Amount : null,
                ResponseCode = a.Payment != null ? a.Payment.ResponseCode : null,
                PaymentCreatedAtUtc = a.Payment != null ? (DateTime?)a.Payment.CreatedAtUtc : null,
                RawResponseJson = a.Payment != null ? a.Payment.RawResponseJson : null
            })
            .ToListAsync();

        return queried.Select(q => new ReportePagosRow
        {
            AppointmentIdShort = q.Id.ToString("N")[..8],
            FechaCreacionUtc = q.CreatedAtUtc,
            EstadoCita = q.AppointmentStatus.ToString(),
            MontoCita = q.AppointmentAmount,
            Pasarela = q.PaymentProvider,
            EstadoPago = q.PaymentStatus?.ToString(),
            MontoPago = q.PaymentAmount,
            CodigoRespuesta = q.ResponseCode,
            FechaPagoUtc = q.PaymentCreatedAtUtc,
            RespuestaCrudaParcial = q.RawResponseJson is { Length: > 0 } raw ? raw[..Math.Min(200, raw.Length)] : null
        }).ToList();
    }

    private async Task<List<ReportePagosSummaryRow>> GetSummaryAsync(DateTime sinceUtc)
    {
        return await _db.Appointments.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= sinceUtc)
            .GroupBy(a => new
            {
                AppointmentStatus = a.Status,
                PaymentStatus = a.Payment != null ? (PaymentStatus?)a.Payment.Status : null,
                Provider = a.Payment != null ? a.Payment.Provider : null
            })
            .Select(g => new ReportePagosSummaryRow
            {
                EstadoCita = g.Key.AppointmentStatus.ToString(),
                EstadoPago = g.Key.PaymentStatus.HasValue ? g.Key.PaymentStatus.Value.ToString() : null,
                Pasarela = g.Key.Provider,
                Total = g.Count()
            })
            .OrderByDescending(s => s.Total)
            .ToListAsync();
    }

    /// <summary>Only ever returns a count - the PatientFullName/PatientEmail values themselves are filtered on
    /// inside the database query and never leave it.</summary>
    private async Task<int> GetTestAppointmentsCountAsync(DateTime sinceUtc)
    {
        return await _db.Appointments.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= sinceUtc && (
                EF.Functions.ILike(a.PatientFullName, "%prueba%") ||
                EF.Functions.ILike(a.PatientFullName, "%test%") ||
                EF.Functions.ILike(a.PatientFullName, "%qa%") ||
                EF.Functions.ILike(a.PatientEmail, "%prueba%") ||
                EF.Functions.ILike(a.PatientEmail, "%test%") ||
                EF.Functions.ILike(a.PatientEmail, "%qa@%")))
            .CountAsync();
    }

    private static string CsvEscape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
