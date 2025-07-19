using ShiftSoftware.ShiftEntity.Model;
using ShiftSoftware.ShiftEntity.Model.Dtos;

namespace Docs.Shared.Customers;

[ShiftEntityKeyAndName(nameof(ID), nameof(Name))]
public class CustomerListDTO : ShiftEntityListDTO
{
    public override string? ID { get; set; }
    public string Name { get; set; } = default!;
    public string AvatarIcon { get; set; } = default!;
    public string AvatarColor { get; set; } = "0";
}