using Consolidado.Domain;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure;

public sealed class ConsolidadoDbContext : DbContext
{
    public DbSet<SaldoDiario> SaldosDiarios => Set<SaldoDiario>();
    public DbSet<EventoProcessado> EventosProcessados => Set<EventoProcessado>();

    public ConsolidadoDbContext(DbContextOptions<ConsolidadoDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SaldoDiario>(builder =>
        {
            builder.ToTable("SaldosDiarios");
            builder.HasKey(s => new { s.ComercianteId, s.Data });
            builder.Property(s => s.TotalCreditos).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(s => s.TotalDebitos).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(s => s.AtualizadoEm).IsRequired();
            builder.Property(s => s.RowVersion).IsRowVersion();
            builder.Ignore(s => s.SaldoFinal);
        });

        modelBuilder.Entity<EventoProcessado>(builder =>
        {
            builder.ToTable("EventosProcessados");
            builder.HasKey(e => e.LancamentoId);
            builder.Property(e => e.ProcessadoEm).IsRequired();
        });
    }
}
