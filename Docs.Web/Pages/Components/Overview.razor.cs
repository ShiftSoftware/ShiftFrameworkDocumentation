using Docs.Web.Interfaces;

namespace Docs.Web.Pages.Components
{
    public partial class Overview
    {
        private CodeFile CustomerDTOFile = new CodeFile
        {
            Downloadable = true,
            FileName = "CustomerDTO.cs",
            PrismClass = "language-cs",
            Content = @"
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
}"
        };

        private CodeFile CustomerListDTOFile = new CodeFile
        {
            Downloadable = true,
            FileName = "CustomerListDTO.cs",
            PrismClass = "language-cs",
            Content = @"
using ShiftSoftware.ShiftEntity.Model;
using ShiftSoftware.ShiftEntity.Model.Dtos;

namespace Docs.Shared.Customers;

[ShiftEntityKeyAndName(nameof(ID), nameof(Name))]
public class CustomerListDTO : ShiftEntityListDTO
{
    public override string? ID { get; set; }
    public string Name { get; set; } = default!;
    public string AvatarIcon { get; set; } = default!;
    public string AvatarColor { get; set; } = ""0"";
}"
        };
    }
}
