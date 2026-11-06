
using EventRegistrationAPI.Data;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// CAMPUSPULSE - LOCAL OFFLINE CONFIGURATION
// ==========================================

// Local data storage
var dataFile = Path.Combine(
    builder.Environment.ContentRootPath,
    "data.json"
);

Directory.CreateDirectory(
    Path.GetDirectoryName(Path.GetFullPath(dataFile))!
);

// Register services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(new DataStore(dataFile));

var app = builder.Build();

// ==========================================
// FRONTEND CONFIGURATION
// ==========================================

var frontendPath = Path.GetFullPath(
    Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "FrontEnd"
    )
);

if (!Directory.Exists(frontendPath))
{
    throw new DirectoryNotFoundException(
        $"Frontend folder not found: {frontendPath}"
    );
}

// Serve index.html as the default page
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(frontendPath)
});

// Serve CSS, JavaScript and other frontend files
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(frontendPath)
});

// ==========================================
// SCALAR API DOCUMENTATION
// ==========================================

// Generate the OpenAPI JSON document for Scalar
app.UseSwagger();

// Display interactive Scalar documentation
app.MapScalarApiReference(options =>
{
    options.WithTitle("CampusPulse API Documentation");
    options.WithOpenApiRoutePattern("/swagger/v1/swagger.json");
});

// ==========================================
// API ROUTES
// ==========================================

// Register all existing controllers
app.MapControllers();

// Health check
app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "Online",
        application = "CampusPulse",
        mode = "Offline"
    })
);

// ==========================================
// START APPLICATION
// ==========================================

app.Run();
