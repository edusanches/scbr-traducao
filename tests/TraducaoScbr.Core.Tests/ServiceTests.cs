using System.Net;
using TraducaoScbr.Core.Game;
using TraducaoScbr.Core.State;
using TraducaoScbr.Core.Translation;

namespace TraducaoScbr.Core.Tests;

public class ReconcilerTests
{
    private static readonly Publication Current = new(
        "4.10.3", "PTU", 13055118, null, 10, new string('a', 64), new Uri("https://scbr.space/x"), null);

    private static GameChannel Channel(string name, long? build) =>
        new(name, $@"C:\SC\{name}", build is { } b ? new GameBuild(b, null, null, null) : null);

    [Fact]
    public void WhoAlreadyHasTheTranslationWantsIt()
    {
        var ch = Channel("LIVE", 12913470);

        Assert.True(Reconciler.Evaluate(ch, new(true, "b", Installer.Language), null, Current).Wanted);
        Assert.False(Reconciler.Evaluate(ch, new(true, "b", null), null, Current).Wanted);
        Assert.False(Reconciler.Evaluate(ch, new(true, "b", Installer.Language), new ChannelRecord { Wanted = false }, Current).Wanted);
        Assert.True(Reconciler.Evaluate(ch, ChannelInspection.Empty, new ChannelRecord { Wanted = true }, Current).Wanted);
    }

    [Theory]
    [InlineData(false, null, null, ChannelStatus.NotInstalled)]
    [InlineData(true, "aaaa", "portuguese_(brazil)", ChannelStatus.UpToDate)]
    [InlineData(true, "aaaa", null, ChannelStatus.Inactive)]
    [InlineData(true, "aaaa", "english", ChannelStatus.Inactive)]
    [InlineData(true, "bbbb", "portuguese_(brazil)", ChannelStatus.Outdated)]
    [InlineData(true, "bbbb", null, ChannelStatus.Outdated)]
    public void StatusAgainstThePublication(bool installed, string? sha, string? language, ChannelStatus expected)
    {
        var inspection = new ChannelInspection(installed, sha is null ? null : sha.Replace("aaaa", new string('a', 64)), language);

        Assert.Equal(expected, Reconciler.Evaluate(Channel("LIVE", 1), inspection, null, Current).Status);
    }

    [Fact]
    public void WithoutThePublicationTheStatusIsUnknown()
    {
        Assert.Equal(ChannelStatus.Unknown, Reconciler.Evaluate(Channel("LIVE", 1), new(true, "b", Installer.Language), null, null).Status);
        Assert.Equal(ChannelStatus.Inactive, Reconciler.Evaluate(Channel("LIVE", 1), new(true, "b", null), null, null).Status);
    }

    [Fact]
    public void TheGameIsAheadWhenItsBuildIsNewer()
    {
        Assert.True(Reconciler.Evaluate(Channel("PTU", 13093654), ChannelInspection.Empty, null, Current).GameAhead);
        Assert.False(Reconciler.Evaluate(Channel("PTU", 13055118), ChannelInspection.Empty, null, Current).GameAhead);
        Assert.False(Reconciler.Evaluate(Channel("LIVE", 12913470), ChannelInspection.Empty, null, Current).GameAhead);
        Assert.False(Reconciler.Evaluate(Channel("LIVE", null), ChannelInspection.Empty, null, Current).GameAhead);
    }

    [Fact]
    public void WithoutThePublishedBuildThereIsNoComparison()
    {
        var noBuild = Current with { Build = null, Channel = null };

        Assert.False(Reconciler.Evaluate(Channel("PTU", 13093654), ChannelInspection.Empty, null, noBuild).GameAhead);
    }

    [Fact]
    public void AutoUpdateTakesWantedOutdatedOrMissingOnly()
    {
        var full = new string('a', 64);
        var states = new[]
        {
            Reconciler.Evaluate(Channel("LIVE", 1), new(true, "velho", Installer.Language), null, Current),
            Reconciler.Evaluate(Channel("PTU", 1), ChannelInspection.Empty, new ChannelRecord { Wanted = true }, Current),
            Reconciler.Evaluate(Channel("EPTU", 1), new(true, full, null), new ChannelRecord { Wanted = true }, Current),
            Reconciler.Evaluate(Channel("HOTFIX", 1), new(true, "velho", Installer.Language), new ChannelRecord { Wanted = false }, Current),
            Reconciler.Evaluate(Channel("TECH-PREVIEW", 1), ChannelInspection.Empty, null, Current),
        };

        Assert.Equal(["LIVE", "PTU"], Reconciler.ToUpdate(states, Current, autoUpdate: true).Select(s => s.Channel.Name));
        Assert.Empty(Reconciler.ToUpdate(states, Current, autoUpdate: false));
        Assert.Empty(Reconciler.ToUpdate(states, null, autoUpdate: true));
    }

    [Fact]
    public void NoticesAreInPortugueseAndNotRepeated()
    {
        var live = Channel("LIVE", 12913470);
        var ptu = Channel("PTU", 13093654);
        var states = new[]
        {
            Reconciler.Evaluate(live, new(true, new string('a', 64), Installer.Language), null, Current),
            Reconciler.Evaluate(ptu, new(true, new string('a', 64), Installer.Language), null, Current),
        };

        var notices = Reconciler.Notices(Current, null, states, [live, ptu], [], []);

        Assert.Equal(2, notices.Count);
        Assert.Equal("Tradução atualizada", notices[0].Title);
        Assert.Equal("A tradução 4.10.3 PTU (build 13055118) foi instalada no LIVE e no PTU.", notices[0].Text);
        Assert.Equal("O PTU está mais novo que a tradução", notices[1].Title);
        Assert.Contains("build 13093654", notices[1].Text);
        Assert.Equal(NoticeKind.Warning, notices[1].Kind);

        Assert.Empty(Reconciler.Notices(Current, null, states, [live, ptu], [], notices.Select(n => n.Key).ToList()));
    }

    [Fact]
    public void ANewPublicationWithoutAutoUpdateAsksToUpdate()
    {
        var live = Channel("LIVE", 12913470);
        var states = new[] { Reconciler.Evaluate(live, new(true, "velho", Installer.Language), null, Current) };
        var previous = new PublicationRecord("4.10.2", "LIVE", 12913470, "velho", null);

        var notices = Reconciler.Notices(Current, previous, states, [], [], []);

        Assert.Equal("Saiu tradução nova", Assert.Single(notices).Title);
        Assert.Equal("A tradução 4.10.3 PTU (build 13055118) está pronta. Abra o app para atualizar o LIVE.", notices[0].Text);
        // Na primeira conferência não há "nova": a tela já mostra tudo.
        Assert.Empty(Reconciler.Notices(Current, null, states, [], [], []));
    }

    [Fact]
    public void FailuresAreNoticed()
    {
        var live = Channel("LIVE", 12913470);
        var states = new[] { Reconciler.Evaluate(live, new(true, "velho", Installer.Language), null, Current) };
        var previous = new PublicationRecord("4.10.2", "LIVE", 12913470, "velho", null);

        var notice = Assert.Single(Reconciler.Notices(Current, previous, states, [], [(live, "Feche o jogo.")], []));

        Assert.Equal("Não deu para atualizar o LIVE", notice.Title);
        Assert.Equal("Feche o jogo.", notice.Text);
    }

    [Fact]
    public void NamesReadLikePortuguese()
    {
        Assert.Equal("LIVE", Reconciler.Names([Channel("LIVE", 1)]));
        Assert.Equal("LIVE e PTU", Reconciler.Names([Channel("LIVE", 1), Channel("PTU", 1)]));
        Assert.Equal("LIVE, PTU e EPTU", Reconciler.Names([Channel("LIVE", 1), Channel("PTU", 1), Channel("EPTU", 1)]));
    }
}

public class TranslationServiceTests
{
    private static TranslationService Service(TempFolder temp, FakeApi fake, params string[] folders) =>
        new(
            fake.Client(),
            new SettingsStore(temp.Combine("dados", "settings.json")),
            extra => new LocatorSources([], [], [.. folders, .. extra], AutoDetect: false),
            temp.Combine("dados", "cache"));

    [Fact]
    public async Task InstallsUpdatesAndRemovesAChannel()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE", TempFolder.LiveManifest);
        var fake = new FakeApi();
        var service = Service(temp, fake, live);

        var first = await service.CheckAsync();
        Assert.True(service.FirstRun);
        Assert.Empty(first.Notices);
        Assert.Equal(ChannelStatus.NotInstalled, Assert.Single(service.Channels).Status);

        await service.InstallAsync(live);
        Assert.Equal(ChannelStatus.UpToDate, service.Channels[0].Status);
        Assert.Equal(fake.File, File.ReadAllBytes(Installer.IniPath(live)));
        Assert.Equal(1, fake.Downloads);

        // Sai tradução nova: o app atualiza sozinho e avisa.
        fake.File = FakeApi.Ini("Frontend_PU_Version=Alpha 4.10 (nova)\r\n");
        var second = await service.CheckAsync();
        Assert.Equal(ChannelStatus.UpToDate, service.Channels[0].Status);
        Assert.Equal(fake.File, File.ReadAllBytes(Installer.IniPath(live)));
        Assert.Equal("Tradução atualizada", Assert.Single(second.Notices).Title);

        // Nada mudou: sem download e sem aviso.
        var third = await service.CheckAsync();
        Assert.Empty(third.Notices);
        Assert.Equal(2, fake.Downloads);

        await service.RemoveAsync(live);
        Assert.Equal(ChannelStatus.NotInstalled, service.Channels[0].Status);
        Assert.False(service.Channels[0].Wanted);

        // Removida, fica removida.
        fake.File = FakeApi.Ini("Frontend_PU_Version=Alpha 4.11\r\n");
        await service.CheckAsync();
        Assert.Equal(ChannelStatus.NotInstalled, service.Channels[0].Status);
    }

    [Fact]
    public async Task AdoptsTheTeamInstallAndReplacesIt()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE", TempFolder.LiveManifest);
        // Como os .cmd da equipe deixam: a tradução da develop e o g_language.
        temp.Write(@"StarCitizen\LIVE\data\Localization\portuguese_(brazil)\global.ini", "da develop", bom: true);
        temp.Write(@"StarCitizen\LIVE\user.cfg", "g_language = portuguese_(brazil)\r\n");
        var fake = new FakeApi();
        var service = Service(temp, fake, live);

        var outcome = await service.CheckAsync();

        Assert.Equal(ChannelStatus.UpToDate, service.Channels[0].Status);
        Assert.Equal(fake.File, File.ReadAllBytes(Installer.IniPath(live)));
        Assert.Equal("Tradução atualizada", Assert.Single(outcome.Notices).Title);
    }

    [Fact]
    public async Task ReinstallsWhenTheFolderVanishes()
    {
        using var temp = new TempFolder();
        var ptu = temp.Channel("StarCitizen", "PTU", TempFolder.PtuManifest);
        var fake = new FakeApi();
        var service = Service(temp, fake, ptu);
        await service.CheckAsync();
        await service.InstallAsync(ptu);

        Directory.Delete(Path.Combine(ptu, "data"), recursive: true);
        await service.CheckAsync();

        Assert.Equal(ChannelStatus.UpToDate, service.Channels[0].Status);
        Assert.Equal(1, fake.Downloads);
    }

    [Fact]
    public async Task WarnsOnceWhenTheGameIsAheadOfTheTranslation()
    {
        using var temp = new TempFolder();
        var ptu = temp.Channel("StarCitizen", "PTU", TempFolder.PtuManifest);
        var fake = new FakeApi();
        var service = Service(temp, fake, ptu);
        await service.CheckAsync();
        await service.InstallAsync(ptu);

        var refresh = await service.RefreshChannelsAsync();
        var notice = Assert.Single(refresh.Notices);
        Assert.Equal("O PTU está mais novo que a tradução", notice.Title);
        Assert.True(service.Channels[0].GameAhead);

        Assert.Empty((await service.RefreshChannelsAsync()).Notices);
        Assert.Empty((await service.CheckAsync()).Notices);

        // O jogo atualizou de novo: outro aviso.
        File.WriteAllText(Path.Combine(ptu, "build_manifest.id"), TempFolder.PtuManifest.Replace("13093654", "13100000"));
        Assert.Single((await service.RefreshChannelsAsync()).Notices);
    }

    [Fact]
    public async Task KeepsWorkingOfflineAndSavesTheSettings()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE", TempFolder.LiveManifest);
        var fake = new FakeApi();
        var service = Service(temp, fake, live);
        await service.CheckAsync();
        await service.InstallAsync(live);

        fake.Offline = true;
        await service.CheckAsync();
        Assert.NotNull(service.ApiError);
        Assert.NotNull(service.Current);
        Assert.Equal(ChannelStatus.UpToDate, service.Channels[0].Status);

        // Outra sessão do app lê o que ficou gravado.
        var again = Service(temp, new FakeApi { Offline = true }, live);
        Assert.False(again.FirstRun);
        Assert.True(again.Settings.Channel(live)!.Wanted);
        Assert.Equal(fake.Sha, again.Settings.LastSeen!.Sha256);
        await again.CheckAsync();
        Assert.Equal(ChannelStatus.Unknown, again.Channels[0].Status);
        Assert.Equal("4.10.3 PTU", again.Channels[0].InstalledPublication!.Label);
    }

    [Fact]
    public async Task InstallWithoutAPublicationIsAFriendlyError()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        var service = Service(temp, new FakeApi { ApiStatus = HttpStatusCode.NotFound }, live);

        await service.CheckAsync();
        Assert.True(service.NothingPublished);
        var error = await Assert.ThrowsAsync<InstallException>(() => service.InstallAsync(live));

        Assert.Contains("Ainda não há tradução publicada", error.Message);
    }

    [Fact]
    public async Task AddsAFolderOnlyWhenItHasTheGame()
    {
        using var temp = new TempFolder();
        var live = temp.Channel(@"Outro disco\StarCitizen", "LIVE");
        Directory.CreateDirectory(Path.Combine(live, "data"));
        var service = Service(temp, new FakeApi());

        Assert.Equal(0, await service.AddFolderAsync(temp.Combine("vazia")));
        Assert.Equal(1, await service.AddFolderAsync(Path.Combine(live, "data")));

        Assert.Equal("LIVE", Assert.Single(service.Channels).Channel.Name);
        Assert.Equal([Path.Combine(live, "data")], service.Settings.ExtraFolders);
    }

    [Fact]
    public async Task ACorruptSettingsFileStartsOver()
    {
        using var temp = new TempFolder();
        temp.Write(@"dados\settings.json", "{ isto não é json");

        var service = Service(temp, new FakeApi());
        await service.CheckAsync();

        Assert.True(service.FirstRun);
        Assert.True(File.Exists(temp.Combine("dados", "settings.json.estragado")));
        Assert.True(File.Exists(temp.Combine("dados", "settings.json")));
    }
}
