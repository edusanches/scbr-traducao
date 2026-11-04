using System.Net;
using TraducaoScbr.Core.Translation;

namespace TraducaoScbr.Core.Tests;

public class PublicationTests
{
    private static readonly Uri Api = new("https://scbr.space/api/traducao/atual");

    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void ReadsTheContract()
    {
        var json = $$"""
            { "versao": "4.10.3", "canal": "PTU", "build": 13055118, "publicadaEm": "2026-11-04T15:20:00-03:00",
              "tamanho": 11327670, "sha256": "{{Sha.ToUpperInvariant()}}",
              "arquivo": "https://scbr.space/api/traducao/arquivo/1", "pagina": "https://scbr.space/traducao" }
            """;

        var p = Publication.Parse(json, Api);

        Assert.Equal("4.10.3 PTU", p.Label);
        Assert.Equal(13055118, p.Build);
        Assert.Equal(11327670, p.Size);
        Assert.Equal(Sha, p.Sha256);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 18, 20, 0, TimeSpan.Zero), p.PublishedAt);
        Assert.Equal("https://scbr.space/api/traducao/arquivo/1", p.File.AbsoluteUri);
        Assert.Equal("https://scbr.space/traducao", p.Page!.AbsoluteUri);
    }

    [Fact]
    public void AcceptsTheBuildAsTextAndRelativeUrls()
    {
        var json = $$"""{ "versao": "4.10.3", "canal": "LIVE", "build": "13055118", "tamanho": 10, "sha256": "{{Sha}}", "arquivo": "/traducao/global.ini" }""";

        var p = Publication.Parse(json, Api);

        Assert.Equal(13055118, p.Build);
        Assert.Equal("https://scbr.space/traducao/global.ini", p.File.AbsoluteUri);
        Assert.Null(p.Page);
        Assert.Null(p.PublishedAt);
    }

    [Fact]
    public void ReadsTheExampleOfTheFinalContract()
    {
        // O exemplo mandado pela sessão do site: data com milissegundos em UTC e os canais além de LIVE e PTU.
        var json = $$"""
            { "versao": "4.10.3", "canal": "TECH-PREVIEW", "build": 13055118, "publicadaEm": "2026-11-05T12:00:00.000Z",
              "tamanho": 11328092, "sha256": "{{Sha}}", "arquivo": "https://scbr.space/api/traducao/arquivo/abc",
              "pagina": "https://scbr.space/traducao" }
            """;

        var p = Publication.Parse(json, Api);

        Assert.Equal(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), p.PublishedAt);
        Assert.Equal("4.10.3 TECH-PREVIEW (build 13055118)", p.Description);
        Assert.Equal(11328092, p.Size);
    }

    [Fact]
    public void ChannelAndBuildMayBeNull()
    {
        // O contrato final deixa o canal e o build nulos quando a rodada não tem a versão do jogo.
        var json = $$"""{ "versao": "4.10.3", "canal": null, "build": null, "tamanho": 10, "sha256": "{{Sha}}", "arquivo": "/a" }""";

        var p = Publication.Parse(json, Api);

        Assert.Null(p.Channel);
        Assert.Null(p.Build);
        Assert.Equal("4.10.3", p.Label);
        Assert.Equal("4.10.3", p.Description);
        Assert.Equal("4.10.3 PTU (build 1)", (p with { Channel = "PTU", Build = 1 }).Description);
    }

    [Theory]
    [InlineData("""{ "canal": "PTU", "build": 1, "tamanho": 10, "sha256": "SHA", "arquivo": "/a" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": "abc", "tamanho": 10, "sha256": "SHA", "arquivo": "/a" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": 1, "sha256": "SHA", "arquivo": "/a" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": 0, "tamanho": 10, "sha256": "SHA", "arquivo": "/a" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": 1, "tamanho": 0, "sha256": "SHA", "arquivo": "/a" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": 1, "tamanho": 999999999999, "sha256": "SHA", "arquivo": "/a" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": 1, "tamanho": 10, "sha256": "abc", "arquivo": "/a" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": 1, "tamanho": 10, "sha256": "SHA" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": 1, "tamanho": 10, "sha256": "SHA", "arquivo": "http://outro.site/global.ini" }""")]
    [InlineData("""{ "versao": "4.10.3", "build": 1, "tamanho": 10, "sha256": "SHA", "arquivo": "ftp://scbr.space/global.ini" }""")]
    [InlineData("[]")]
    [InlineData("<html>")]
    public void RefusesBrokenResponses(string json)
    {
        Assert.Throws<FormatException>(() => Publication.Parse(json.Replace("SHA", Sha), Api));
    }

    [Fact]
    public void AcceptsHttpOnlyOnLocalhost()
    {
        var local = new Uri("http://localhost:5199/api/traducao/atual");
        var json = $$"""{ "versao": "4.10.3", "build": 1, "tamanho": 10, "sha256": "{{Sha}}", "arquivo": "/api/traducao/arquivo/x" }""";

        Assert.Equal("http://localhost:5199/api/traducao/arquivo/x", Publication.Parse(json, local).File.AbsoluteUri);
    }
}

public class TranslationApiTests
{
    [Fact]
    public async Task GetsTheCurrentPublication()
    {
        var fake = new FakeApi();

        var p = await fake.Client().GetCurrentAsync(default);

        Assert.Equal("4.10.3 PTU", p!.Label);
        Assert.Equal(fake.Sha, p.Sha256);
        Assert.Equal($"https://scbr.space/api/traducao/arquivo/{fake.Sha}", p.File.AbsoluteUri);
    }

    [Fact]
    public async Task NotFoundMeansNothingPublishedYet()
    {
        var fake = new FakeApi { ApiStatus = HttpStatusCode.NotFound };

        Assert.Null(await fake.Client().GetCurrentAsync(default));
    }

    [Fact]
    public async Task ServerAndNetworkErrorsBecomeMessages()
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => new FakeApi { ApiStatus = HttpStatusCode.BadGateway }.Client().GetCurrentAsync(default));
        Assert.Contains("502", error.Message);

        var offline = await Assert.ThrowsAsync<ApiException>(() => new FakeApi { Offline = true }.Client().GetCurrentAsync(default));
        Assert.Contains("internet", offline.Message);
    }

    [Fact]
    public async Task DownloadsAndChecksTheFile()
    {
        using var temp = new TempFolder();
        var fake = new FakeApi();
        var api = fake.Client();
        var p = (await api.GetCurrentAsync(default))!;
        var target = temp.Combine("global.ini");

        await api.DownloadAsync(p, target, null, default);

        Assert.Equal(fake.File, File.ReadAllBytes(target));
        Assert.False(File.Exists(target + ".parcial"));
    }

    [Fact]
    public async Task AFileThatLeftIsAFriendlyError()
    {
        using var temp = new TempFolder();
        var fake = new FakeApi();
        var api = fake.Client();
        var p = (await api.GetCurrentAsync(default))!;

        var gone = await Assert.ThrowsAsync<ApiException>(() => api.DownloadAsync(p with { File = new Uri("https://scbr.space/sumiu") }, temp.Combine("x.ini"), null, default));

        Assert.Contains("saiu do ar", gone.Message);
    }

    [Fact]
    public async Task ATamperedFileIsNotKept()
    {
        using var temp = new TempFolder();
        var fake = new FakeApi();
        var api = fake.Client();
        var p = (await api.GetCurrentAsync(default))!;
        var target = temp.Combine("global.ini");

        fake.Tampered = FakeApi.Ini("Frontend_PU_Version=Alpha 4.1X\r\n");
        var wrongHash = await Assert.ThrowsAsync<ApiException>(() => api.DownloadAsync(p, target, null, default));
        Assert.Contains("sha256", wrongHash.Message);

        fake.Tampered = [.. fake.File, .. fake.File];
        await Assert.ThrowsAsync<ApiException>(() => api.DownloadAsync(p, target, null, default));

        fake.Tampered = fake.File[..5];
        await Assert.ThrowsAsync<ApiException>(() => api.DownloadAsync(p, target, null, default));

        Assert.False(File.Exists(target));
        Assert.False(File.Exists(target + ".parcial"));
    }
}
