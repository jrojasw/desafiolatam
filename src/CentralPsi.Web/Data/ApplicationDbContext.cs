using CentralPsi.Web.Models.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Data;

// IDataProtectionKeyContext persists the ASP.NET Data Protection keys (which encrypt auth cookies, antiforgery
// tokens, etc.) to the database instead of the container's local disk - Render's free tier restarts the
// container on every deploy and after periods of inactivity, which would otherwise generate fresh keys each
// time and silently log out every admin session.
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<ProfessionalAvailability> ProfessionalAvailabilities => Set<ProfessionalAvailability>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CancellationRequest> CancellationRequests => Set<CancellationRequest>();
    public DbSet<SlideImage> SlideImages => Set<SlideImage>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<PaymentInboxMessage> PaymentInboxMessages => Set<PaymentInboxMessage>();
    public DbSet<PaymentInboxAttachment> PaymentInboxAttachments => Set<PaymentInboxAttachment>();
    public DbSet<FinanceSettings> FinanceSettings => Set<FinanceSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Professional>(e =>
        {
            e.HasIndex(p => p.Email).IsUnique();
            e.HasIndex(p => p.CertificateValidationCode);
            e.Property(p => p.Status).HasConversion<string>();
            e.HasMany(p => p.Availabilities)
                .WithOne(a => a.Professional)
                .HasForeignKey(a => a.ProfessionalId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Appointments)
                .WithOne(a => a.Professional)
                .HasForeignKey(a => a.ProfessionalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Appointment>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>();
            e.HasIndex(a => new { a.ProfessionalId, a.ScheduledStartUtc });
            e.HasIndex(a => a.CancellationToken).IsUnique();
            e.HasIndex(a => a.PatientAttendanceToken).IsUnique();
            e.HasIndex(a => a.ProfessionalAttendanceToken).IsUnique();
            e.HasOne(a => a.Payment)
                .WithOne(p => p.Appointment)
                .HasForeignKey<Payment>(p => p.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.CancellationRequest)
                .WithOne(c => c.Appointment)
                .HasForeignKey<CancellationRequest>(c => c.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>();
            e.Property(p => p.Amount).HasPrecision(10, 2);
        });

        builder.Entity<Appointment>().Property(a => a.Amount).HasPrecision(10, 2);

        builder.Entity<CancellationRequest>(e =>
        {
            e.Property(c => c.RefundTier).HasConversion<string>();
            e.Property(c => c.Status).HasConversion<string>();
            e.Property(c => c.RefundAmount).HasPrecision(10, 2);
        });

        builder.Entity<NewsArticle>(e =>
        {
            e.Property(n => n.Category).HasConversion<string>();
        });

        builder.Entity<PaymentInboxMessage>(e =>
        {
            e.HasIndex(m => m.ImapUid).IsUnique();
            e.HasMany(m => m.Attachments)
                .WithOne(a => a.Message)
                .HasForeignKey(a => a.PaymentInboxMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FinanceSettings>(e =>
        {
            e.Property(f => f.TaxRatePercent).HasPrecision(5, 2);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.OccurredAtUtc);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
        });

        builder.Entity<ErrorLog>(e =>
        {
            e.HasIndex(x => x.OccurredAtUtc);
        });
    }
}
