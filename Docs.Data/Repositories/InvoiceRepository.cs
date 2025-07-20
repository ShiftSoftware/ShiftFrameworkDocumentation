using Docs.Data.DbContext;
using Docs.Data.Entities;
using Docs.Shared.Invoice;
using ShiftSoftware.ShiftEntity.EFCore;

namespace Docs.Data.Repositories;

public class InvoiceRepository : ShiftRepository<DB, Invoice, InvoiceListDTO, InvoiceDTO>
{
    public InvoiceRepository(DB db) : base(db)
    {
    }
}