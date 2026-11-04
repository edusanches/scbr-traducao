using System.Net;
using System.Security.Cryptography;

namespace TraducaoScbr.Core.Translation;

/// <summary>Um erro de rede ou da API, com a mensagem pronta para a tela.</summary>
public sealed class ApiException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>O cliente da API do scbr.space: a tradução publicada e o download do arquivo, conferido pelo sha256.</summary>
public sealed class TranslationApi
{
    public static readonly Uri DefaultBase = new("https://scbr.space/");

    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    private readonly HttpClient _http;

    public TranslationApi(HttpClient http, Uri baseUri)
    {
        _http = http;
        BaseUri = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
    }

    public Uri BaseUri { get; }

    public Uri Endpoint => new(BaseUri, "api/traducao/atual");

    /// <summary>Um HttpClient com o User-Agent do app. Não manda nada da pessoa.</summary>
    public static HttpClient CreateHttpClient(string appVersion)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"TraducaoSCBR/{appVersion}");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("(+https://scbr.space/traducao)");
        return http;
    }

    /// <summary>A tradução publicada agora, ou null quando ainda não há nenhuma (404).</summary>
    public async Task<Publication?> GetCurrentAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ApiTimeout);
        try
        {
            using var response = await _http.GetAsync(Endpoint, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException($"O scbr.space respondeu com o erro {(int)response.StatusCode}. O app tenta de novo mais tarde.");
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token);
            try
            {
                return Publication.Parse(json, Endpoint);
            }
            catch (FormatException e)
            {
                throw new ApiException("O scbr.space mandou uma resposta que o app não entende. Pode ser preciso atualizar o app pela Microsoft Store.", e);
            }
        }
        catch (HttpRequestException e)
        {
            throw new ApiException("Não deu para falar com o scbr.space. Confira a internet.", e);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("O scbr.space demorou demais para responder. O app tenta de novo mais tarde.", e);
        }
    }

    /// <summary>
    /// Baixa o global.ini publicado para <paramref name="destination"/>. Só grava o arquivo se o tamanho e o sha256
    /// baterem com os da publicação.
    /// </summary>
    public async Task DownloadAsync(Publication publication, string destination, IProgress<double>? progress, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(DownloadTimeout);
        var partial = destination + ".parcial";
        try
        {
            using var response = await _http.GetAsync(publication.File, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                // Saiu uma publicação mais nova entre a conferência e o download.
                throw new ApiException("Essa tradução saiu do ar no scbr.space. Confira de novo para baixar a atual.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException($"O download da tradução falhou (erro {(int)response.StatusCode}). O app tenta de novo mais tarde.");
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    total += read;
                    if (total > publication.Size)
                    {
                        throw new ApiException("O arquivo da tradução veio maior que o publicado. Nada foi instalado.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    progress?.Report((double)total / publication.Size);
                }
            }

            if (total != publication.Size)
            {
                throw new ApiException("O arquivo da tradução veio incompleto. Nada foi instalado.");
            }

            var sha = Convert.ToHexStringLower(hash.GetHashAndReset());
            if (!string.Equals(sha, publication.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new ApiException("O arquivo baixado não confere com o publicado (sha256). Nada foi instalado.");
            }

            File.Move(partial, destination, overwrite: true);
        }
        catch (HttpRequestException e)
        {
            throw new ApiException("O download da tradução caiu. Confira a internet.", e);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("O download da tradução demorou demais. O app tenta de novo mais tarde.", e);
        }
        catch (IOException e)
        {
            throw new ApiException("O download da tradução não terminou (rede ou disco). O app tenta de novo mais tarde.", e);
        }
        finally
        {
            TextFile.TryDelete(partial);
        }
    }
}
