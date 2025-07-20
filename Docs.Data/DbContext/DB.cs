using Docs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using ShiftSoftware.ShiftEntity.EFCore;

namespace Docs.Data.DbContext;

public class DB : ShiftDbContext
{
    public DB(DbContextOptions<DB> option) : base(option)
    {
        
    }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
}