var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();



app.MapGet("/dados/{numCaras}", (int numCaras) =>
{
    if (numCaras < 2)
    {
        return Results.BadRequest(new
        {
            Error = "Valor inválido",
            Detalle = "Un dado debe tener al menos 2 caras"
        });
    }

    
    int resultadoAlea =  Random.Shared.Next(1,numCaras+1);

    return Results.Ok(new
    {

        Resultado = resultadoAlea,
        Mensaje = $"Lanzaste un dado de {numCaras} caras",
        Fecha = DateTime.Now.ToString()
    });
})
.WithName("LanzarDado");

app.Run();
