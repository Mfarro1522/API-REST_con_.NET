var builder = WebApplication.CreateBuilder(args);

// 1. Registrar HttpClient (obligatorio)
builder.Services.AddHttpClient();

// Add services to the container.
builder.Services.AddCors(options => {
    options.AddPolicy("Libre", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//Primero  los cors -> luego los Controllers
app.UseCors("Libre");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();



app.Run();
