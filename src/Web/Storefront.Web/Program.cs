using Storefront.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(8);
    opt.Cookie.HttpOnly = true;
});

builder.Services.AddHttpClient<ToeicApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GatewayUrl"] ?? "http://localhost:5080");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
