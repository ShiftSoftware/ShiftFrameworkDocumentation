using Docs.Data;
using Docs.Data.DbContext;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

Action<DbContextOptionsBuilder> dbOptionBuilder = x =>
{
    x.UseInMemoryDatabase("Docs")
    .UseTemporal(true);
};

builder.Services.RegisterShiftRepositories(typeof(Docs.Data.Marker).Assembly);

builder.Services.AddDbContext<DB>(dbOptionBuilder);
builder.Services.AddHttpClient();

var mvcBuilder = builder.Services
    .AddLocalization()
    .AddHttpContextAccessor()
    .AddControllers();

builder.Services.AddShiftEntityPrint(x =>
{
    x.TokenExpirationInSeconds = 600;
    x.SASTokenKey = "One-Two-Three";
});

mvcBuilder.AddShiftEntityWeb(x =>
{
    x.AddDataAssembly(typeof(Docs.Data.Marker).Assembly);
    x.WrapValidationErrorResponseWithShiftEntityResponse(true);
    x.AddAutoMapper(typeof(Docs.Data.Marker).Assembly);
    
    //x.HashId.RegisterHashId(builder.Configuration.GetValue<bool>("Settings:HashIdSettings:AcceptUnencodedIds"));
    //x.HashId.RegisterIdentityHashId("one-two", 5);

    var azureStorageAccounts = new List<ShiftSoftware.ShiftEntity.Core.Services.AzureStorageOption>();
    builder.Configuration.Bind("AzureStorageAccounts", azureStorageAccounts);
    x.AddAzureStorage(azureStorageAccounts.ToArray());
});


//builder.Services.AddTypeAuth((o) =>
//{
//    o.AddActionTree<ShiftIdentityActions>();
//    o.AddActionTree<ShiftSoftware.ShiftEntity.Core.AzureStorageActionTree>();
//});



builder.Services.AddRazorPages();


//builder.Services.AddAzureClients(clientBuilder =>
//{
//    clientBuilder.AddBlobServiceClient(builder.Configuration["devstoreaccount1:blob"]!, preferMsi: true);
//    clientBuilder.AddQueueServiceClient(builder.Configuration["devstoreaccount1:queue"]!, preferMsi: true);
//});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DB>();
    DatabaseSeeder.Seed(db);
}

var supportedCultures = new List<CultureInfo>
{
    new CultureInfo("en-US"),
    new CultureInfo("ar-IQ"),
    new CultureInfo("ku-IQ"),
};

app.UseRequestLocalization(options =>
{
    options.SetDefaultCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider> { new AcceptLanguageHeaderRequestCultureProvider() };
    options.ApplyCurrentCultureToResponseHeaders = true;
});

app.MapControllers();

app.UseCors(x => x.WithOrigins("*").AllowAnyMethod().AllowAnyHeader());


app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapRazorPages();
app.MapFallbackToFile("index.html");


app.Run();