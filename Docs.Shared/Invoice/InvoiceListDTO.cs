using ShiftSoftware.ShiftEntity.Model.Dtos;

namespace Docs.Shared.Invoice;

public class InvoiceListDTO : ShiftEntityListDTO
{
    public override string? ID { get; set; }
    public string? CustomerID { get; set; }
}