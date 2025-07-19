using ShiftSoftware.ShiftEntity.Model.Dtos;

namespace Docs.Shared.Customers;

public class CustomerDTO : ShiftEntityViewAndUpsertDTO
{
    public override string? ID { get; set; }
    public string Name { get; set; } = default!;
}