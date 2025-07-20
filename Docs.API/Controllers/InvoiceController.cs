using Docs.Data.Repositories;
using Docs.Shared.Invoice;
using Microsoft.AspNetCore.Mvc;
using ShiftSoftware.ShiftEntity.Web;

namespace Docs.API.Controllers;

[Route("api/[controller]")]
public class InvoiceController : ShiftEntityControllerAsync<InvoiceRepository, Data.Entities.Invoice, InvoiceListDTO, InvoiceDTO>
{
    public InvoiceController()
    {

    }
}