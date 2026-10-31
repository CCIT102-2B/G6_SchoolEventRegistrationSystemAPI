using EventRegistrationAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// 1) Turn on controllers (the classes inside the Controllers folder)
builder.Services.AddControllers();


builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});


builder.Services.AddSingleton(new DataStore(Path.Combine(builder.Environment.ContentRootPath, "data.json")));

var app = builder.Build();

app.UseCors();           // must come before MapControllers
app.MapControllers();    // connects the [Route] attributes in the controllers to URLs like /api/events

// A friendly message if you open http://localhost:5005 in the browser
app.MapGet("/", () => "Event Registration API is running. Try http://localhost:5005/api/events");

app.Run();
