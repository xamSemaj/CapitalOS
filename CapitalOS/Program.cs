using CapitalOS.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container BEFORE builder.Build()
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IStockMarketService, YahooStockMarketService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

// NOW build the app
var app = builder.Build();

// Configure the HTTP request pipeline AFTER building
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();