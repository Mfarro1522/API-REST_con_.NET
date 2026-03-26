var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

var myAllowOrigins = "myAllowOrigins";

// Configurar CORS para permitir que el navegador lea los datos
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowOrigins,
                      policy =>
                      {
                          policy.AllowAnyOrigin()
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();

// Activar CORS
app.UseCors(myAllowOrigins);

app.UseAuthorization();

app.MapControllers();

app.Run();
