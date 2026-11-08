using Storefront.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(8);
    opt.Cookie.HttpOnly = true;
});

builder.Services.AddHttpClient<ToeicApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GatewayUrl"] ?? "http://localhost:5080");
});
builder.Services.AddHttpClient<BillingApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GatewayUrl"] ?? "http://localhost:5080");
});
builder.Services.AddHttpClient<AiApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GatewayUrl"] ?? "http://localhost:5080");
    client.Timeout = AiApiClient.ChatTimeout;
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

// Role-based Access Control Guard for Admin Portal
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
    {
        var jwt = context.Session.GetString("jwt");
        if (string.IsNullOrEmpty(jwt))
        {
            context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(path)}");
            return;
        }

        var roles = (context.Session.GetString("roles") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool isAdmin = roles.Contains("Admin");
        bool isModerator = roles.Contains("Moderator");

        if (!isAdmin && !isModerator)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!isAdmin && isModerator)
        {
            var isContentPath = path.StartsWith("/Admin/Exams", StringComparison.OrdinalIgnoreCase)
                             || path.StartsWith("/Admin/Questions", StringComparison.OrdinalIgnoreCase)
                             || path.StartsWith("/Admin/Passages", StringComparison.OrdinalIgnoreCase)
                             || path.StartsWith("/Admin/ExamStats", StringComparison.OrdinalIgnoreCase);
            if (!isContentPath)
            {
                context.Response.Redirect("/Admin/Exams");
                return;
            }
        }
    }

    await next();
});

app.UseAuthorization();
app.MapRazorPages();
app.Run();
