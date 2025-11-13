using Docs.Data.DbContext;
using Docs.Data.Entities;
using Docs.Shared.Customers;
using ShiftSoftware.ShiftEntity.EFCore;

namespace Docs.Data.Repositories;

public class CustomerRepository : ShiftRepository<DB, Customer, CustomerListDTO, CustomerDTO>
{
    public CustomerRepository(DB db) : base(db)
    {

    }

    public override async Task<int> SaveChangesAsync()
    {
        return await base.SaveChangesAsync();
    }
}