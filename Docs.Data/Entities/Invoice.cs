using ShiftSoftware.ShiftEntity.Core;

namespace Docs.Data.Entities;

public class Invoice : ShiftEntity<Invoice>
{
    public long CustomerID { get; set; }
    public virtual Customer Customer { get; set; } = default!;
}