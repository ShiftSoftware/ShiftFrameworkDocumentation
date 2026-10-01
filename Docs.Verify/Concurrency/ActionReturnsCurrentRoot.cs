using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftSoftware.ShiftEntity.Core;
using ShiftSoftware.ShiftEntity.EFCore;
using ShiftSoftware.ShiftEntity.Model;
using ShiftSoftware.ShiftEntity.Model.Dtos;
using ShiftSoftware.ShiftEntity.Web;
using ShiftSoftware.TypeAuth.Core;
using ShiftSoftware.TypeAuth.Core.Actions;

namespace Docs.Verify.Concurrency;

// Compile-checked example for the "Concurrency in Shift Framework" guide (Incident C: a stale version stamp after a
// nested save). Mirrors the StockPlusPlus sample's SalesOrderController, whose SalesOrderStaleStampTests failed with
// the 204 answer and pass with the order answer.

public class Order : ShiftEntity<Order>
{
    public string CustomerName { get; set; } = default!;

    [Precision(18, 2)]
    public decimal Total { get; set; }

    public virtual ICollection<OrderLine> Lines { get; set; } = new HashSet<OrderLine>();

    public void RecalculateTotal() => Total = Lines.Where(x => !x.IsCancelled).Sum(x => x.Quantity * x.UnitPrice);
}

public class OrderLine : ShiftEntity<OrderLine>
{
    public long OrderID { get; set; }
    public int Quantity { get; set; }

    [Precision(18, 2)]
    public decimal UnitPrice { get; set; }

    public bool IsCancelled { get; set; }
}

public class OrderDTO : ShiftEntityViewAndUpsertDTO
{
    public override string? ID { get; set; }
    public string CustomerName { get; set; } = default!;
    public decimal Total { get; set; }
    public List<OrderLineDTO> Lines { get; set; } = new();
}

public class OrderLineDTO : ShiftEntityViewAndUpsertDTO
{
    public override string? ID { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsCancelled { get; set; }
}

public class OrderListDTO : ShiftEntityListDTO
{
    public override string? ID { get; set; }
    public string CustomerName { get; set; } = default!;
}

public class OrdersDb(DbContextOptions<OrdersDb> options) : ShiftDbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
}

public class OrderRepository(OrdersDb db)
    : ShiftRepository<OrdersDb, Order, OrderListDTO, OrderDTO>(db, o => o.IncludeRelatedEntitiesWithFindAsync(x => x.Include(e => e.Lines)));

[ActionTree("Orders", "Orders")]
public class OrdersActionTree
{
    public static readonly ReadWriteDeleteAction Order = new("Order");
}

[Route("api/[controller]")]
public class OrderController() : ShiftEntitySecureControllerAsync<OrderRepository, Order, OrderListDTO, OrderDTO>(OrdersActionTree.Order)
{
    // snippet:ConcurrencyActionReturnsCurrentRoot
    [Authorize]
    [HttpPost("{key}/lines/{lineKey}/cancel")]
    public async Task<ActionResult<ShiftEntityResponse<OrderDTO>>> CancelLine(
        string key, string lineKey,
        [FromServices] OrderRepository repository,
        [FromServices] IHashIdService hashIds,
        [FromServices] ITypeAuthService typeAuth)
    {
        if (!typeAuth.CanWrite(OrdersActionTree.Order))
            return Forbid();

        var order = await repository.FindAsync(hashIds.Decode<OrderDTO>(key));
        var line = order?.Lines.SingleOrDefault(x => x.ID == hashIds.Decode<OrderLineDTO>(lineKey));
        if (order is null || line is null)
            return NotFound();

        line.IsCancelled = true;   // the child...
        order.RecalculateTotal();  // ...and the parent: both get a new LastSaveDate on save

        await repository.SaveChangesAsync();

        // Answer with the ROOT as it now is, in the same shape as GET, so the caller's form can take the new
        // LastSaveDate. NoContent() here leaves the form with a stale stamp: its next save always gets a 409.
        return Ok(new ShiftEntityResponse<OrderDTO>(await repository.ViewAsync(order)));
    }
    // endsnippet
}
