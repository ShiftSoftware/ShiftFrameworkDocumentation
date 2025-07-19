namespace Docs.API.Controllers;

using Docs.Data.Repositories;
using Docs.Shared.Customers;
using Microsoft.AspNetCore.Mvc;
using ShiftSoftware.ShiftEntity.Web;

[Route("api/[controller]")]
public class CustomerController : ShiftEntityControllerAsync<CustomerRepository, Data.Entities.Customer, CustomerListDTO, CustomerDTO>
{
    public CustomerController()
    {
    }
}