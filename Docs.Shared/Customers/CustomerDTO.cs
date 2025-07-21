using ShiftSoftware.ShiftEntity.Model.Dtos;
using System.ComponentModel.DataAnnotations;

namespace Docs.Shared.Customers;

public class CustomerDTO : ShiftEntityViewAndUpsertDTO
{
    public override string? ID { get; set; }

    [Required]
    public string Name { get; set; } = default!;

    [Required]
    public string AvatarIcon { get; set; } = default!;

    public int AvatarColor { get; set; } = 0;
}