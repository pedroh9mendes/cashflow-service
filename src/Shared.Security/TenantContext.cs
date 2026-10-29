namespace Shared.Security;

public sealed class TenantContext
{
    public Guid ComercianteId { get; }

    public TenantContext(Guid comercianteId)
    {
        if (comercianteId == Guid.Empty)
        {
            throw new ArgumentException("ComercianteId não pode ser vazio.", nameof(comercianteId));
        }

        ComercianteId = comercianteId;
    }
}
