using Scynapse.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddKeyedRedisClient("scynapse-redis");
builder.UseScynapseClient();
builder.Services.AddControllers();

var app = builder.Build();

var options = new DefaultFilesOptions();
options.DefaultFileNames.Clear();
options.DefaultFileNames.Add("index.html");

app.UseDefaultFiles(options);
app.UseStaticFiles();
app.MapControllers();
app.Run();
