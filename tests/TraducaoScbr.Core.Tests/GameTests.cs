using TraducaoScbr.Core.Game;

namespace TraducaoScbr.Core.Tests;

public class BuildManifestTests
{
    [Fact]
    public void ReadsTheBuildFromRequestedP4ChangeNum()
    {
        var build = BuildManifest.Parse(TempFolder.LiveManifest);

        Assert.NotNull(build);
        Assert.Equal(12913470, build.ChangeNumber);
        Assert.Equal("sc-alpha-4.10.2", build.Branch);
        Assert.Equal("4.10.197.2878", build.Version);
    }

    [Fact]
    public void TheBranchOfThePtuIsNotTheVersion()
    {
        // No PTU 4.10.3 o Branch dizia 4.10.0: o build é o que vale.
        var build = BuildManifest.Parse(TempFolder.PtuManifest);

        Assert.Equal(13093654, build!.ChangeNumber);
        Assert.Equal("sc-alpha-4.10.0", build.Branch);
    }

    [Theory]
    [InlineData("""{ "Data": { "RequestedP4ChangeNum": 13055118 } }""", 13055118)]
    [InlineData("""{ "RequestedP4ChangeNum": " 13055118 " }""", 13055118)]
    [InlineData("""{ "Data": { "RequestedP4ChangeNum": "13055118", }, }""", 13055118)]
    public void AcceptsNumbersStringsAndLooseJson(string json, long expected)
    {
        Assert.Equal(expected, BuildManifest.Parse(json)!.ChangeNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("[]")]
    [InlineData("""{ "Data": { "Branch": "sc-alpha-4.10.2" } }""")]
    [InlineData("""{ "Data": { "RequestedP4ChangeNum": "" } }""")]
    [InlineData("""{ "Data": { "RequestedP4ChangeNum": "0" } }""")]
    [InlineData("""{ "Data": { "RequestedP4ChangeNum": "abc" } }""")]
    public void InvalidManifestsHaveNoBuild(string json)
    {
        Assert.Null(BuildManifest.Parse(json));
    }

    [Fact]
    public void ReadsTheFileWithBomAndWithoutTheFile()
    {
        using var temp = new TempFolder();
        temp.Write(@"LIVE\build_manifest.id", TempFolder.LiveManifest, bom: true);

        Assert.Equal(12913470, BuildManifest.Read(temp.Combine("LIVE"))!.ChangeNumber);
        Assert.Null(BuildManifest.Read(temp.Combine("PTU")));
    }
}

public class LauncherLogTests
{
    [Fact]
    public void ReadsTheChannelFoldersFromJsonLines()
    {
        const string log = """
            { "t":"2026-11-03 19:25:04.634", "[main][info] ": "[API] request: https://robertsspaceindustries.com/api/launcher/v3/games/library"  },
            { "t":"2026-11-03 19:25:04.634", "[main][info] ": "[Launcher::launch] Launching Star Citizen LIVE from (C:\\Program Files\\Roberts Space Industries\\StarCitizen\\LIVE)"  },
            { "t":"2026-11-03 20:34:43.663", "[main][info] ": "[Launcher::launch] Launching Star Citizen PTU from (D:\\Jogos\\StarCitizen\\PTU)"  },
            { "t":"2026-11-03 23:21:34.100", "[main][info] ": "[Launcher::launch] Launching Star Citizen LIVE from (C:\\Program Files\\Roberts Space Industries\\StarCitizen\\LIVE)"  },
            """;

        var folders = LauncherLog.ParseChannelFolders(log);

        Assert.Equal(
            [@"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE", @"D:\Jogos\StarCitizen\PTU"],
            folders);
    }

    [Fact]
    public void KeepsParenthesesInsideThePath()
    {
        const string log = """{ "t":"x", "[main][info] ": "[Launcher::launch] Launching Star Citizen EPTU from (E:\\Program Files (x86)\\Roberts Space Industries\\StarCitizen\\EPTU)"  },""";

        Assert.Equal([@"E:\Program Files (x86)\Roberts Space Industries\StarCitizen\EPTU"], LauncherLog.ParseChannelFolders(log));
    }

    [Fact]
    public void AcceptsPlainLinesFromOlderLaunchers()
    {
        const string log = @"[Launcher::launch] Launching Star Citizen TECH-PREVIEW from (F:\StarCitizen\TECH-PREVIEW)";

        Assert.Equal([@"F:\StarCitizen\TECH-PREVIEW"], LauncherLog.ParseChannelFolders(log));
    }

    [Fact]
    public void AMissingLogHasNoFolders()
    {
        using var temp = new TempFolder();

        Assert.Empty(LauncherLog.ReadChannelFolders(temp.Combine("log.log")));
    }
}

public class GameLocatorTests
{
    [Fact]
    public void FindsTheChannelsOfTheCommonLibraries()
    {
        using var temp = new TempFolder();
        var library = @"Program Files\Roberts Space Industries\StarCitizen";
        temp.Channel(library, "PTU", TempFolder.PtuManifest);
        temp.Channel(library, "LIVE", TempFolder.LiveManifest);
        Directory.CreateDirectory(temp.Combine(library, "Sem jogo"));

        var channels = GameLocator.Find(new LocatorSources([temp.Path], [], []));

        Assert.Equal(["LIVE", "PTU"], channels.Select(c => c.Name));
        Assert.Equal(12913470, channels[0].Build!.ChangeNumber);
        Assert.Equal(13093654, channels[1].Build!.ChangeNumber);
        Assert.Equal(temp.Combine(library, "LIVE"), channels[0].Path);
    }

    [Fact]
    public void FindsAnyChannelNameAndSortsTheKnownOnesFirst()
    {
        using var temp = new TempFolder();
        temp.Channel("StarCitizen", "TECH-PREVIEW");
        temp.Channel("StarCitizen", "HOTFIX");
        temp.Channel("StarCitizen", "eptu");
        temp.Channel("StarCitizen", "LIVE");
        temp.Channel("StarCitizen", "NOVO");

        var channels = GameLocator.Find(new LocatorSources([temp.Path], [], []));

        Assert.Equal(["LIVE", "EPTU", "HOTFIX", "TECH-PREVIEW", "NOVO"], channels.Select(c => c.Name));
    }

    [Fact]
    public void FollowsTheLauncherLogToOtherLibrariesAndTheirSiblings()
    {
        using var temp = new TempFolder();
        var live = temp.Channel(@"Jogos\RSI\StarCitizen", "LIVE", TempFolder.LiveManifest);
        temp.Channel(@"Jogos\RSI\StarCitizen", "PTU", TempFolder.PtuManifest);
        var log = temp.Write(
            "log.log",
            "{ \"t\":\"x\", \"[main][info] \": \"[Launcher::launch] Launching Star Citizen LIVE from (" + live.Replace(@"\", @"\\") + ")\"  },\n");

        var channels = GameLocator.Find(new LocatorSources([], [log], []));

        Assert.Equal(["LIVE", "PTU"], channels.Select(c => c.Name));
    }

    [Fact]
    public void IgnoresLauncherFoldersThatNoLongerHaveTheGame()
    {
        using var temp = new TempFolder();
        var log = temp.Write("log.log", @"Launching Star Citizen LIVE from (" + temp.Combine("sumiu", "LIVE") + ")");

        Assert.Empty(GameLocator.Find(new LocatorSources([], [log], [])));
    }

    [Fact]
    public void ResolvesTheFoldersAPersonMightPick()
    {
        using var temp = new TempFolder();
        var live = temp.Channel(@"RSI\StarCitizen", "LIVE");
        var ptu = temp.Channel(@"RSI\StarCitizen", "PTU");
        Directory.CreateDirectory(Path.Combine(live, "data", "Localization"));

        Assert.Equal([live], GameLocator.ResolveFolder(live));
        Assert.Equal([live], GameLocator.ResolveFolder(live + @"\"));
        Assert.Equal([live], GameLocator.ResolveFolder(Path.Combine(live, "data")));
        Assert.Equal([live], GameLocator.ResolveFolder(Path.Combine(live, "data", "Localization")));
        Assert.Equal([live, ptu], GameLocator.ResolveFolder(temp.Combine(@"RSI\StarCitizen")).Order());
        Assert.Equal([live, ptu], GameLocator.ResolveFolder(temp.Combine("RSI")).Order());
        Assert.Empty(GameLocator.ResolveFolder(temp.Combine("não existe")));
        Assert.Empty(GameLocator.ResolveFolder(temp.Path));
    }

    [Fact]
    public void ExtraFoldersCountEvenWithoutAutoDetectAndDoNotRepeat()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");

        var channels = GameLocator.Find(new LocatorSources([temp.Path], [], [live, live.ToLowerInvariant(), temp.Combine("StarCitizen")], AutoDetect: false));

        Assert.Single(channels);
        Assert.Equal("LIVE", channels[0].Name);
        Assert.Null(channels[0].Build);
    }
}
