#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false

// Um scbr.space de mentira, para testar o app sem o site. Serve a API do app com o global.ini de uma pasta:
//
//   dotnet run tools/mock-api.cs -- <pasta> [porta]
//   (o app) TraducaoSCBR.exe --api http://localhost:5199
//
// A pasta tem o global.ini publicado e o publicacao.json:
//   { "versao": "4.10.3", "canal": "PTU", "build": 13055118, "publicadaEm": "2026-11-04T15:20:00-03:00" }
// O mock relê os dois a cada pedido: troque o global.ini e o build para simular uma tradução nova. Sem o global.ini,
// a API responde 404 (nada publicado). No publicacao.json, "falha": 503 faz a API responder esse erro e
// "corromper": true serve um arquivo diferente do sha256 anunciado.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Uso: dotnet run tools/mock-api.cs -- <pasta com global.ini e publicacao.json> [porta]");
    return 1;
}

var folder = Path.GetFullPath(args[0]);
var port = args.Length > 1 ? int.Parse(args[1]) : 5199;
var builder = WebApplication.CreateBuilder();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls($"http://localhost:{port}");
var app = builder.Build();

app.MapGet("/api/traducao/atual", () =>
{
    var ini = Path.Combine(folder, "global.ini");
    var meta = Meta(folder);
    if (meta?["falha"]?.GetValue<int>() is int status)
    {
        Console.WriteLine($"GET /api/traducao/atual -> {status} (falha simulada)");
        return Results.StatusCode(status);
    }

    if (!File.Exists(ini) || meta is null)
    {
        Console.WriteLine("GET /api/traducao/atual -> 404 (nada publicado)");
        return Results.NotFound(new { erro = "Nenhuma tradução publicada." });
    }

    var bytes = File.ReadAllBytes(ini);
    var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
    Console.WriteLine($"GET /api/traducao/atual -> {meta["versao"]} {meta["canal"]} {meta["build"]} ({bytes.Length} bytes, {sha[..12]}…)");
    return Results.Json(new
    {
        versao = meta["versao"]?.GetValue<string>(),
        canal = meta["canal"]?.GetValue<string>(),
        build = meta["build"]?.GetValue<long>(),
        publicadaEm = meta["publicadaEm"]?.GetValue<string>(),
        tamanho = bytes.Length,
        sha256 = sha,
        arquivo = $"/api/traducao/arquivo/{sha}",
        pagina = $"http://localhost:{port}/traducao",
    });
});

app.MapGet("/api/traducao/arquivo/{sha}", (string sha) =>
{
    var ini = Path.Combine(folder, "global.ini");
    if (!File.Exists(ini))
    {
        return Results.NotFound();
    }

    var bytes = File.ReadAllBytes(ini);
    if (Meta(folder)?["corromper"]?.GetValue<bool>() == true)
    {
        bytes[^1] ^= 0xFF;
        Console.WriteLine("GET arquivo -> corrompido de propósito");
    }
    else
    {
        Console.WriteLine($"GET arquivo {sha[..12]}… -> {bytes.Length} bytes");
    }

    return Results.File(bytes, "text/plain; charset=utf-8", "global.ini");
});

app.MapGet("/traducao", () => Results.Text("Página da tradução (mock).", "text/plain; charset=utf-8"));

Console.WriteLine($"Mock da API em http://localhost:{port}, servindo {folder}");
app.Run();
return 0;

static JsonNode? Meta(string folder)
{
    var path = Path.Combine(folder, "publicacao.json");
    try
    {
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;
    }
    catch (JsonException)
    {
        return null;
    }
}
