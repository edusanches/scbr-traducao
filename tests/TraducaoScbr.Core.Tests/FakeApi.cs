using System.Net;
using System.Security.Cryptography;
using System.Text;
using TraducaoScbr.Core.Translation;

namespace TraducaoScbr.Core.Tests;

/// <summary>Um scbr.space de mentira, dentro do teste: responde a API e o arquivo.</summary>
internal sealed class FakeApi : HttpMessageHandler
{
    public static readonly Uri Base = new("https://scbr.space/");

    public byte[] File { get; set; } = Ini("Frontend_PU_Version=Alpha 4.10\r\n");

    public string Version { get; set; } = "4.10.3";

    public string Channel { get; set; } = "PTU";

    public long Build { get; set; } = 13055118;

    public HttpStatusCode ApiStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Muda o arquivo servido sem mudar o sha256 anunciado.</summary>
    public byte[]? Tampered { get; set; }

    public bool Offline { get; set; }

    public int ApiCalls { get; private set; }

    public int Downloads { get; private set; }

    public string Sha => Convert.ToHexStringLower(SHA256.HashData(File));

    public static byte[] Ini(string text) => [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)];

    public string Json => $$"""
        {
          "versao": "{{Version}}",
          "canal": "{{Channel}}",
          "build": {{Build}},
          "publicadaEm": "2026-11-04T18:20:00Z",
          "tamanho": {{File.Length}},
          "sha256": "{{Sha}}",
          "arquivo": "/api/traducao/arquivo/{{Sha}}",
          "pagina": "https://scbr.space/traducao"
        }
        """;

    public TranslationApi Client() => new(new HttpClient(this), Base);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            throw new HttpRequestException("sem rede");
        }

        var path = request.RequestUri!.AbsolutePath;
        if (path == "/api/traducao/atual")
        {
            ApiCalls++;
            var response = new HttpResponseMessage(ApiStatus);
            if (ApiStatus == HttpStatusCode.OK)
            {
                response.Content = new StringContent(Json, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }

        if (path.StartsWith("/api/traducao/arquivo/", StringComparison.Ordinal))
        {
            Downloads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Tampered ?? File) });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
