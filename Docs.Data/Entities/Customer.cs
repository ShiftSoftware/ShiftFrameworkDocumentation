using ShiftSoftware.ShiftEntity.Core;
using ShiftSoftware.ShiftEntity.Model;

namespace Docs.Data.Entities;

[ShiftEntityKeyAndName(nameof(ID), nameof(Name))]
public class Customer : ShiftEntity<Customer>
{
    public string Name { get; set; } = default!;
    public string AvatarIcon { get; set; } = default!;
    public string AvatarColor { get; set; } = "0";
}