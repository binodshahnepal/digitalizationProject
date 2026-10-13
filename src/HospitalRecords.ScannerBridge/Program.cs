using HospitalRecords.ScannerBridge.Models;
using HospitalRecords.ScannerBridge.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<WiaScannerManager>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAngularClient");

// Health / Status Check
app.MapGet("/api/scanner/status", () => Results.Ok(new
{
    status = "online",
    agent = "BPKMCH Local Hardware Scanner Bridge",
    version = "1.0.0",
    platform = Environment.OSVersion.ToString()
}));

// List Connected USB / Network Scanners
app.MapGet("/api/scanner/devices", (WiaScannerManager manager) =>
{
    var devices = manager.GetConnectedScanners();
    return Results.Ok(devices);
});

// Trigger Scanner Acquisition
app.MapPost("/api/scanner/scan", async (ScanRequest? request, WiaScannerManager manager) =>
{
    request ??= new ScanRequest();
    var result = await manager.AcquireScanAsync(request);
    return Results.Ok(result);
});

app.Run();
