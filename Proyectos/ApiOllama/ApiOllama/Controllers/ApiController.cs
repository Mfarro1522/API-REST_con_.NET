using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ApiOllama.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public ChatController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet("modelos")]
        public async Task<IActionResult> GetModelos()
        {
            // 1- instanciamos ollama en nuestra terminal ejecutamo : ollama serve
            // 2- hacemos la peticion de los modelos que estan instaldos en ollama ni quieres instalar nada puedes usar modelos cloud como qwen3.5 
            var respuesta = await _httpClient.GetAsync("http://localhost:11434/api/tags");

            if (respuesta.IsSuccessStatusCode)
            {
                // 3- almacenamos el json que nos envia ollama
                var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

                //4- dentro de ese json hay una propiedad models esa es la de los nombres y esa necesitamos 
                var modelos = json.GetProperty("models")
                                  .EnumerateArray()
                                  .Select(m => m.GetProperty("name").GetString())
                                  .ToList();

                return Ok(modelos);
            }

            return StatusCode(500, "No se pudo conectar con Ollama para obtener modelos.");
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] RequestUsuario request)
        {
            //segundo post en este se maneja el envio del mensaje y le enviamos formateado en json 
            var datosParaOllama = new
            {
                model = request.Modelo, // importante que el nombre del modelo coincida con el que tenemos en ollama
                prompt = request.Mensaje,
                stream = false // esto es para pedir la respuesta completa si se pone true se va a recibir la respuesta en partes (streaming)
            };

            // 2.ollama serve corre en el puerto 11434 
            // la url lleva /api/generate que es el endpoint para generar texto con ollama
            var respuestaOllama = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", datosParaOllama);

            if (respuestaOllama.IsSuccessStatusCode)
            {
                // 3. igual que en el anterior post se recibe un json pero con la respuesta de la IA
                var resultado = await respuestaOllama.Content.ReadFromJsonAsync<JsonElement>();
                string textoIA = resultado.GetProperty("response").GetString();

                return Ok(new { respuesta = textoIA });
            }

            return StatusCode(500, "Ollama no está disponible o falló.");
        }
    }

    //este record es para recibir el modelo y el mensaje que el usuario quiere enviar a la IA desde el frontend


    /*explicacion sencilla de que es un record: es una forma de definir una clase inmutable en C#. 
     * Es decir, una vez que se crea una instancia de un record, sus propiedades no pueden cambiar. 
     * Esto es útil para representar datos que no deben modificarse después de su creación, 
     * como los datos que recibimos en una solicitud HTTP. En este caso, el record RequestUsuario 
     * tiene dos propiedades: Modelo y Mensaje, 
     * que representan el modelo de IA que queremos usar y el mensaje que queremos enviar a la IA, respectivamente.
    
    */
    public record RequestUsuario(string Modelo, string Mensaje);
}
