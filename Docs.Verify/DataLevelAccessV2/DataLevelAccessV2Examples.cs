using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShiftSoftware.ShiftEntity.Core;
using ShiftSoftware.ShiftEntity.Core.DataLevelAccess;
using ShiftSoftware.ShiftEntity.Core.Flags;
using ShiftSoftware.ShiftEntity.EFCore;
using ShiftSoftware.ShiftEntity.Model.Dtos;
using ShiftSoftware.ShiftEntity.Model.Flags;
using ShiftSoftware.ShiftIdentity.Core;
using ShiftSoftware.ShiftIdentity.Core.DTOs.Company;
using ShiftSoftware.TypeAuth.Core;
using ShiftSoftware.TypeAuth.Core.Actions;

namespace Docs.Verify.DataLevelAccessV2;

public static class HostRegistration
{
    public static void AddDataLevelAccess(IServiceCollection services)
    {
        // snippet:DataLevelAccessHostOptIn
        services.AddShiftEntityDataLevelAccess();
        // endsnippet
    }
}

// snippet:DataLevelAccessStandardMarker
[TemporalShiftEntity]
public sealed class Shipment : ShiftEntity<Shipment>, IEntityHasCompany<Shipment>
{
    public string Reference { get; set; } = default!;
    public long? CompanyID { get; set; }
    public long? IntermediaryCompanyID { get; set; }
}
// endsnippet

public sealed class ShipmentListDTO : ShiftEntityListDTO
{
    public override string? ID { get; set; }
    public string Reference { get; set; } = default!;
}

public sealed class ShipmentDTO : ShiftEntityViewAndUpsertDTO
{
    public override string? ID { get; set; }
    public string Reference { get; set; } = default!;
}

public sealed class VerificationDbContext(DbContextOptions<VerificationDbContext> options) : ShiftDbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<DepartmentRecord> DepartmentRecords => Set<DepartmentRecord>();
}

public sealed class ShipmentRepository :
    ShiftRepository<VerificationDbContext, Shipment, ShipmentListDTO, ShipmentDTO>
{
    private readonly VerificationDbContext verificationDb;
    private readonly DataLevelAccessContext dataLevelAccessContext;

    public ShipmentRepository(
        VerificationDbContext db,
        DataLevelAccessContext dataLevelAccessContext) : base(db, options =>
    {
        // snippet:DataLevelAccessSameActionOverride
        options.DataLevelAccess(access =>
        {
            access.On(ShiftIdentityActions.DataLevelAccess.Companies)
                .Keys(x => x.CompanyID, x => x.IntermediaryCompanyID)
                .HashId<CompanyDTO>()
                .Self(ShiftSoftware.ShiftEntity.Core.Constants.CompanyIdClaim);
        });
        // endsnippet
    })
    {
        verificationDb = db;
        this.dataLevelAccessContext = dataLevelAccessContext;
    }

    // snippet:DataLevelAccessManualQuery
    public IQueryable<Shipment> GetPendingShipments()
    {
        var query = verificationDb.Shipments.Where(x => x.Reference.StartsWith("PENDING-"));
        var policy = DataLevelAccessPolicy
            ?? throw new InvalidOperationException("This query requires a v2 data-level policy.");

        return policy.ApplyQueryFilter(query, Access.Read, dataLevelAccessContext);
    }
    // endsnippet
}

// snippet:DataLevelAccessCustomAction
[ActionTree("Application actions", "Permissions owned by this application")]
public sealed class ApplicationActionTree
{
    [ActionTree("Data level access", "Row-level dimensions owned by this application")]
    public sealed class DataLevelAccess
    {
        public static readonly DynamicReadWriteDeleteAction Departments = new("Departments");
    }
}
// endsnippet

public sealed class DepartmentRecord : ShiftEntity<DepartmentRecord>
{
    public string Name { get; set; } = default!;
    public long? DepartmentID { get; set; }
}

public sealed class DepartmentRecordListDTO : ShiftEntityListDTO
{
    public override string? ID { get; set; }
    public string Name { get; set; } = default!;
}

public sealed class DepartmentRecordDTO : ShiftEntityViewAndUpsertDTO
{
    public override string? ID { get; set; }
    public string Name { get; set; } = default!;
}

public sealed class DepartmentRecordRepository :
    ShiftRepository<VerificationDbContext, DepartmentRecord, DepartmentRecordListDTO, DepartmentRecordDTO>
{
    public DepartmentRecordRepository(VerificationDbContext db) : base(db, options =>
    {
        // snippet:DataLevelAccessCustomDimension
        options.DataLevelAccess(access =>
        {
            access.On(ApplicationActionTree.DataLevelAccess.Departments)
                .Key(x => x.DepartmentID);
        });
        // endsnippet
    })
    {
    }
}

public static class DeniedBehaviorExample
{
    public static void Configure(
        ShiftRepositoryOptions<Shipment, ShipmentListDTO, ShipmentDTO> options)
    {
        // snippet:DataLevelAccessForbiddenDisclosure
        options.DataLevelAccess(access =>
        {
            access.On(ShiftIdentityActions.DataLevelAccess.Companies)
                .Key(x => x.CompanyID)
                .HashId<CompanyDTO>()
                .Self(ShiftSoftware.ShiftEntity.Core.Constants.CompanyIdClaim);

            access.WhenDenied(DataLevelDeniedBehavior.Forbidden);
        });
        // endsnippet
    }
}
