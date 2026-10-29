using Lancamentos.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lancamentos.Infrastructure;

public sealed class LancamentosDbContext : DbContext
{
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public LancamentosDbContext(DbContextOptions<LancamentosDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lancamento>(builder =>
        {
            builder.ToTable("Lancamentos");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.ComercianteId).IsRequired();
            builder.Property(l => l.Tipo).IsRequired();
            builder.Property(l => l.Valor).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(l => l.Descricao).HasMaxLength(500).IsRequired();
            builder.Property(l => l.Data).IsRequired();
            builder.Property(l => l.RegistradoEm).IsRequired();

            // Índice composto por ComercianteId é o padrão de acesso principal: toda consulta
            // de negócio é escopada por tenant (ver ADR-006 e docs/security.md).
            builder.HasIndex(l => new { l.ComercianteId, l.Data });
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.TipoEvento).HasMaxLength(200).IsRequired();
            builder.Property(o => o.Payload).IsRequired();
            builder.Property(o => o.CriadoEm).IsRequired();

            builder.HasIndex(o => o.PublicadoEm);
        });
    }
}
