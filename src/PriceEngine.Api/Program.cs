using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

using Serilog;
using Serilog.Context;

using PriceEngine.Api.Configuration;
using PriceEngine.Api.Data;
using PriceEngine.Api.Models;
using PriceEngine.Api.Pricing;
using PriceEngine.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ------------------- Serilog -------------------
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration) // appsettings.json → "Serilog"
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// ------------------- Swagger -------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ------------------- Options -------------------
builder.Services.Configure<PricingRulesOptions>(
    builder.Configuration.GetSection("PricingRules"));

// ------------------- Db -------------------
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// ------------------- Repos & Rules -------------------
builder.Services.AddScoped<ICouponRepository, EfCouponRepository>();

// Kurallar ve motor ILogger ile
builder.Services.AddSingleton<OccupancyRule>(sp =>
{
    var o = sp.GetRequiredService<IOptionsMonitor<PricingRulesOptions>>().CurrentValue.Occupancy;
    var logger = sp.GetRequiredService<ILogger<OccupancyRule>>();
    return new OccupancyRule(o.LowThreshold, o.HighThreshold, o.LowDiscountPercent, o.HighIncreasePercent, logger);
});

builder.Services.AddSingleton<TimePressureRule>(sp =>
{
    var t = sp.GetRequiredService<IOptionsMonitor<PricingRulesOptions>>().CurrentValue.TimePressure;
    var logger = sp.GetRequiredService<ILogger<TimePressureRule>>();
    return new TimePressureRule(t.IncreasePercent, t.DiscountPercent, t.HoursThreshold, t.DaysThreshold, logger);
});

builder.Services.AddScoped<CouponRule>();

builder.Services.AddScoped<PricingEngine>(sp =>
{
    var rules = new IPricingRule[]
    {
        sp.GetRequiredService<OccupancyRule>(),
        sp.GetRequiredService<TimePressureRule>(),
        sp.GetRequiredService<CouponRule>()
    };
    var logger = sp.GetRequiredService<ILogger<PricingEngine>>();
    return new PricingEngine(rules, logger);
});

// ------------------- Queue & Worker -------------------
builder.Services.AddSingleton<IBackgroundTaskQueue, ChannelBackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedWorker>();

// ------------------- Auth -------------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.LogoutPath = "/api/auth/logout";
        o.Cookie.Name = "priceengine.auth";
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// ------------------- DB migrate + seed -------------------
app.MigrateAndSeed();

// ------------------- CorrelationId -------------------
app.Use(async (ctx, next) =>
{
    var cid = ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var v) && !string.IsNullOrWhiteSpace(v)
        ? v.ToString()
        : Guid.NewGuid().ToString("N");
    ctx.Response.Headers["X-Correlation-Id"] = cid;
    using (LogContext.PushProperty("CorrelationId", cid))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

// ------------------- Middleware -------------------
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// ======================================================================
// =========================== PUBLIC API ================================
// ======================================================================

// Otobüs listesi (ana sayfa)
app.MapGet("/buses",
    async (AppDbContext db) =>
        await db.Buses.AsNoTracking().OrderBy(b => b.DepartureTime).ToListAsync())
    .WithOpenApi();

// Fiyat hesaplama
app.MapPost("/price/calculate", async (PriceRequest req, PricingEngine engine, AppDbContext db) =>
{
    if (req.BasePrice <= 0) return Results.BadRequest("BasePrice > 0 olmalı.");
    if (req.Capacity <= 0)   return Results.BadRequest("Capacity > 0 olmalı.");
    if (req.SoldSeats < 0 || req.SoldSeats > req.Capacity)
        return Results.BadRequest("SoldSeats 0..Capacity olmalı.");

    // Kupon kontrolü
    if (!string.IsNullOrWhiteSpace(req.CouponCode))
    {
        var c = await db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == req.CouponCode.Trim().ToUpper());
        if (c is null)
            return Results.NotFound(new { message = "Kupon bulunamadı." });
        if (c.ExpireAt < DateTime.UtcNow)
            return Results.BadRequest(new { message = "Kuponun süresi dolmuş." });
    }

    var resp = engine.Calculate(req);
    return Results.Ok(resp);
}).WithOpenApi();


// Kupon kontrol
app.MapGet("/coupon/{code}", async (string code, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("Kupon kodu boş olamaz.");

    var c = await db.Coupons.AsNoTracking()
        .FirstOrDefaultAsync(x => x.Code.ToUpper() == code.Trim().ToUpper());

    if (c is null)
        return Results.NotFound(new { message = "Kupon bulunamadı." });

    if (c.IsExpired(DateTime.UtcNow))
        return Results.BadRequest(new { message = "Kuponun süresi dolmuş." });

    return Results.Ok(new
    {
        c.Code,
        c.Percent,
        c.ExpireAt,
        IsExpired = false
    });
}).WithOpenApi();


// ======================================================================
// ============================ AUTH ====================================
// ======================================================================
app.MapPost("/api/auth/login", async (HttpContext ctx) =>
{
    var dto = await ctx.Request.ReadFromJsonAsync<LoginDto>();
    if (dto is null) return Results.BadRequest("Geçersiz istek.");
    if (dto.Username != "admin" || dto.Password != "admin") return Results.Unauthorized();

    var claims = new List<Claim> { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Admin") };
    var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
    return Results.Ok(new { ok = true });
}).WithOpenApi();

app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { ok = true });
}).WithOpenApi();

// ======================================================================
// =========================== ADMIN API ================================
// ======================================================================

// Buses
app.MapGet("/api/buses",
    async (AppDbContext db) => await db.Buses.AsNoTracking().OrderBy(b => b.DepartureTime).ToListAsync())
    .RequireAuthorization().WithOpenApi();

app.MapPost("/api/buses", async (CreateBusRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || req.Capacity <= 0 || req.BasePrice <= 0)
        return Results.BadRequest("İsim/kapasite/fiyat geçerli olmalı.");
    if (req.SoldSeats < 0 || req.SoldSeats > req.Capacity)
        return Results.BadRequest("SoldSeats 0..Capacity olmalı.");

    var bus = new Bus {
        Name = req.Name.Trim(),
        Capacity = req.Capacity,
        SoldSeats = req.SoldSeats,
        BasePrice = req.BasePrice,
        DepartureTime = req.DepartureTime
    };
    db.Buses.Add(bus);
    await db.SaveChangesAsync();
    return Results.Created($"/api/buses/{bus.Id}", bus);
}).RequireAuthorization().WithOpenApi();

app.MapPut("/api/buses/{id:int}/sold", async (int id, UpdateSoldSeatsRequest req, AppDbContext db) =>
{
    var bus = await db.Buses.FindAsync(id);
    if (bus is null) return Results.NotFound();
    if (req.SoldSeats < 0 || req.SoldSeats > bus.Capacity)
        return Results.BadRequest("SoldSeats 0..Capacity olmalı.");

    bus.SoldSeats = req.SoldSeats;
    await db.SaveChangesAsync();
    return Results.Ok(bus);
}).RequireAuthorization().WithOpenApi();

app.MapDelete("/api/buses/{id:int}", async (int id, AppDbContext db) =>
{
    var bus = await db.Buses.FindAsync(id);
    if (bus is null) return Results.NotFound();
    db.Buses.Remove(bus);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization().WithOpenApi();

// Coupons
app.MapGet("/api/coupons",
    async (AppDbContext db) => await db.Coupons.AsNoTracking().OrderBy(c => c.Code).ToListAsync())
    .RequireAuthorization().WithOpenApi();

app.MapPost("/api/coupons", async (CreateCouponRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Code) || req.Percent <= 0 || req.Percent > 100)
        return Results.BadRequest("Kod ve yüzde 1..100 olmalı.");
    var code = req.Code.Trim().ToUpperInvariant();
    if (await db.Coupons.AnyAsync(c => c.Code == code)) return Results.Conflict("Bu kupon kodu zaten var.");
    var c = new Coupon { Code = code, Percent = req.Percent, ExpireAt = req.ExpireAt };
    db.Coupons.Add(c);
    await db.SaveChangesAsync();
    return Results.Created($"/api/coupons/{c.Id}", c);
}).RequireAuthorization().WithOpenApi();

app.MapDelete("/api/coupons/{id:int}", async (int id, AppDbContext db) =>
{
    var c = await db.Coupons.FindAsync(id);
    if (c is null) return Results.NotFound();
    db.Coupons.Remove(c);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization().WithOpenApi();

// ==================== JOBS (Queue) ====================
// Toplu fiyat güncelleme job'ı kuyrukla
app.MapPost("/api/jobs/bulk-price", async (BulkPriceUpdateRequest req,
                                           IBackgroundTaskQueue queue,
                                           IServiceProvider sp,
                                           ILoggerFactory lf) =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var job = new BackgroundJob
    {
        Type = "BulkPriceUpdate",
        PayloadJson = JsonSerializer.Serialize(req),
        Status = JobStatus.Queued
    };
    db.Jobs.Add(job);
    await db.SaveChangesAsync();

    await queue.QueueAsync(async ct =>
    {
        var log = lf.CreateLogger("BulkPriceUpdate");
        using var s2 = sp.CreateScope();
        var db2 = s2.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            log.LogInformation("Job {JobId} started. Percent={Percent}", job.Id, req.Percent);
            var j = await db2.Jobs.FirstAsync(x => x.Id == job.Id, ct);
            j.Status = JobStatus.Running;
            j.StartedAt = DateTime.UtcNow;
            await db2.SaveChangesAsync(ct);

            var list = await db2.Buses.OrderBy(b => b.Id).ToListAsync(ct);
            j.Total = list.Count;
            j.Processed = 0;
            await db2.SaveChangesAsync(ct);

            foreach (var b in list)
            {
                ct.ThrowIfCancellationRequested();
                var factor = 1 + (req.Percent / 100m);
                b.BasePrice = Math.Round(b.BasePrice * factor, 2, MidpointRounding.AwayFromZero);
                await db2.SaveChangesAsync(ct);

                j.Processed = (j.Processed ?? 0) + 1;
                await db2.SaveChangesAsync(ct);
            }

            j.Status = JobStatus.Succeeded;
            j.FinishedAt = DateTime.UtcNow;
            await db2.SaveChangesAsync(ct);

            log.LogInformation("Job {JobId} done. Updated={Count}", job.Id, list.Count);
        }
        catch (Exception ex)
        {
            var j = await db2.Jobs.FirstAsync(x => x.Id == job.Id, ct);
            j.Status = JobStatus.Failed;
            j.Error = ex.Message;
            j.FinishedAt = DateTime.UtcNow;
            await db2.SaveChangesAsync(ct);

            log.LogError(ex, "Job {JobId} failed", job.Id);
        }
    });

    return Results.Accepted($"/api/jobs/{job.Id}", new { job.Id, job.Status });
}).RequireAuthorization().WithOpenApi();

// Job durumu
app.MapGet("/api/jobs/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
    return job is null ? Results.NotFound() : Results.Ok(job);
}).RequireAuthorization().WithOpenApi();


app.MapPost("/api/admin/generate-buses", (bool? reset, AppDbContext db) =>
{
    // reset=true gönderirsen tüm Buses temizlenip baştan 100 örnek yazılır.
    DbInitializer.EnsureBuses(db, target: 100, reset: reset ?? false);
    var count = db.Buses.Count();
    return Results.Ok(new { count });
})
.RequireAuthorization()
.WithOpenApi();

// ======================================================================
// =========================== PAGE ROUTES ==============================
// ======================================================================
app.MapGet("/admin", (HttpContext ctx) =>
{
    if (!(ctx.User.Identity?.IsAuthenticated ?? false)) return Results.Redirect("/login");
    return Results.Redirect("/admin.html");
}).ExcludeFromDescription();

app.MapGet("/login", () => Results.Redirect("/login.html"))
   .ExcludeFromDescription();

app.Run();
