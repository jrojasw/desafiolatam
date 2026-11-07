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
///
/// Rows are filtered by whichever of CreatedAtUtc, the payment's own CreatedAtUtc/TransactionDateUtc, or the
/// session's ScheduledStartUtc is the earliest plausible one (see ResolveEffectiveDate) - not CreatedAtUtc
/// alone. CreatedAtUtc is a NOT NULL column and BookingController now sets it explicitly to DateTime.UtcNow, so
/// it should never actually be missing/garbage going forward, but this keeps a row from silently falling out of
/// every date range if it ever is (a pre-existing row from before that fix, a bug, a bad migration, etc.) -
/// Admin/Finanzas and Admin/Dashboard don't have this problem because neither of them filters by
/// Appointment.CreatedAtUtc at all (Finanzas uses Payment.TransactionDateUtc, Dashboard's "upcoming" count uses
/// ScheduledStartUtc), which is also why their numbers don't line up with a CreatedAtUtc-only report.
/// </summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/ReportePagos")]
public class ReportePagosController : Controller
{
    private static readonly int[] AllowedDayRanges = { 7, 30, 90 };

    // Anything older than this is treated as "not a real timestamp" (e.g. default(DateTime)/DateTime.MinValue)
    // rather than as a genuine candidate date - CentralPsi didn't exist yet in 2020.
    private static readonly DateTime MinimumPlausibleDate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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

        var rows = await GetRowsAsync(sinceUtc);
        var vm = new ReportePagosViewModel
        {
            Days = days,
            Rows = rows,
            Summary = BuildSummary(rows),
            TestAppointmentsCount = await GetTestAppointmentsCountAsync(sinceUtc),
            FechasSospechosasCount = rows.Count(r => r.FechaSospechosa)
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
        sb.AppendLine("appointment_id,fecha_creacion_utc,fecha_sesion_utc,estado_cita,monto_cita,pasarela,estado_pago,monto_pago,codigo_respuesta,fecha_pago_utc,respuesta_cruda_parcial,fecha_sospechosa");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(r.AppointmentIdShort),
                CsvEscape(r.FechaCreacionUtc.ToString("yyyy-MM-dd HH:mm:ss")),
                CsvEscape(r.FechaSesionUtc.ToString("yyyy-MM-dd HH:mm:ss")),
                CsvEscape(r.EstadoCita),
                CsvEscape(r.MontoCita.ToString(CultureInfo.InvariantCulture)),
                CsvEscape(r.Pasarela ?? ""),
                CsvEscape(r.EstadoPago ?? ""),
                CsvEscape(r.MontoPago?.ToString(CultureInfo.InvariantCulture) ?? ""),
                CsvEscape(r.CodigoRespuesta?.ToString(CultureInfo.InvariantCulture) ?? ""),
                CsvEscape(r.FechaPagoUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
                CsvEscape(r.RespuestaCrudaParcial ?? ""),
                CsvEscape(r.FechaSospechosa ? "si" : "no")));
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"citas-pagos-{days}d.csv");
    }

    [HttpGet("ExportarResumenCsv")]
    public async Task<IActionResult> ExportarResumenCsv(int days = 30)
    {
        days = AllowedDayRanges.Contains(days) ? days : 30;
        var sinceUtc = DateTime.UtcNow.AddDays(-days);
        var summary = BuildSummary(await GetRowsAsync(sinceUtc));

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

    /// <summary>Pulls every appointment (no date filter at the SQL level - this app's volume makes that cheap,
    /// and the whole point is that CreatedAtUtc alone can't be trusted to filter correctly), computes each row's
    /// effective date in memory, and only then applies the day-range filter. The projection still never selects
    /// PatientFullName/Email/Phone.</summary>
    private async Task<List<ReportePagosRow>> GetRowsAsync(DateTime sinceUtc)
    {
        var queried = await _db.Appointments.AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.CreatedAtUtc,
                a.ScheduledStartUtc,
                AppointmentStatus = a.Status,
                AppointmentAmount = a.Amount,
                PaymentProvider = a.Payment != null ? a.Payment.Provider : null,
                PaymentStatus = a.Payment != null ? (PaymentStatus?)a.Payment.Status : null,
                PaymentAmount = a.Payment != null ? (decimal?)a.Payment.Amount : null,
                ResponseCode = a.Payment != null ? a.Payment.ResponseCode : null,
                PaymentCreatedAtUtc = a.Payment != null ? (DateTime?)a.Payment.CreatedAtUtc : null,
                PaymentTransactionDateUtc = a.Payment != null ? a.Payment.TransactionDateUtc : null,
                RawResponseJson = a.Payment != null ? a.Payment.RawResponseJson : null
            })
            .ToListAsync();

        return queried
            .Select(q =>
            {
                var (effectiveDate, suspicious) = ResolveEffectiveDate(q.CreatedAtUtc, q.PaymentCreatedAtUtc, q.PaymentTransactionDateUtc, q.ScheduledStartUtc);
                var row = new ReportePagosRow
                {
                    AppointmentIdShort = q.Id.ToString("N")[..8],
                    FechaCreacionUtc = q.CreatedAtUtc,
                    FechaSesionUtc = q.ScheduledStartUtc,
                    EstadoCita = q.AppointmentStatus.ToString(),
                    MontoCita = q.AppointmentAmount,
                    Pasarela = q.PaymentProvider,
                    EstadoPago = q.PaymentStatus?.ToString(),
                    MontoPago = q.PaymentAmount,
                    CodigoRespuesta = q.ResponseCode,
                    FechaPagoUtc = q.PaymentCreatedAtUtc,
                    RespuestaCrudaParcial = q.RawResponseJson is { Length: > 0 } raw ? raw[..Math.Min(200, raw.Length)] : null,
                    FechaSospechosa = suspicious
                };
                return (Row: row, EffectiveDate: effectiveDate);
            })
            .Where(x => x.EffectiveDate >= sinceUtc)
            .OrderByDescending(x => x.EffectiveDate)
            .Select(x => x.Row)
            .ToList();
    }

    /// <summary>Picks the earliest plausible "this is when the row really happened" date out of the ones
    /// available, preferring Appointment.CreatedAtUtc when it looks real. Returns suspicious=true when
    /// CreatedAtUtc itself had to be discarded (too old to be real) in favor of one of the fallbacks.</summary>
    private static (DateTime EffectiveDate, bool Suspicious) ResolveEffectiveDate(
        DateTime createdAtUtc, DateTime? paymentCreatedAtUtc, DateTime? transactionDateUtc, DateTime scheduledStartUtc)
    {
        if (createdAtUtc > MinimumPlausibleDate)
        {
            return (createdAtUtc, false);
        }

        var fallback = new[] { paymentCreatedAtUtc, transactionDateUtc, scheduledStartUtc }
            .Where(d => d is DateTime dt && dt > MinimumPlausibleDate)
            .Select(d => d!.Value)
            .DefaultIfEmpty(createdAtUtc)
            .Min();
        return (fallback, true);
    }

    private static List<ReportePagosSummaryRow> BuildSummary(List<ReportePagosRow> rows) =>
        rows.GroupBy(r => new { r.EstadoCita, r.EstadoPago, r.Pasarela })
            .Select(g => new ReportePagosSummaryRow
            {
                EstadoCita = g.Key.EstadoCita,
                EstadoPago = g.Key.EstadoPago,
                Pasarela = g.Key.Pasarela,
                Total = g.Count()
            })
            .OrderByDescending(s => s.Total)
            .ToList();

    /// <summary>Only ever returns a count - the PatientFullName/PatientEmail values themselves are filtered on
    /// inside the database query and never leave it. Uses the same "any of these dates falls in range" logic as
    /// GetRowsAsync, translated directly to SQL instead of computed in memory, since this query never needs to
    /// materialize a row to begin with.</summary>
    private async Task<int> GetTestAppointmentsCountAsync(DateTime sinceUtc)
    {
        return await _db.Appointments.AsNoTracking()
            .Where(a =>
                (a.CreatedAtUtc >= sinceUtc
                    || (a.Payment != null && a.Payment.CreatedAtUtc >= sinceUtc)
                    || (a.Payment != null && a.Payment.TransactionDateUtc != null && a.Payment.TransactionDateUtc >= sinceUtc)
                    || a.ScheduledStartUtc >= sinceUtc)
                && (
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
