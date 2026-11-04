using System.Text;
using TraducaoScbr.Core.Translation;

namespace TraducaoScbr.Core.Tests;

public class InstallerTests
{
    private static string Translation(TempFolder temp, string text)
    {
        var path = temp.Write($"cache\\{Guid.NewGuid():N}.ini", text, bom: true);
        return path;
    }

    [Fact]
    public void InstallsTheFileAndChoosesTheLanguageInANewUserCfg()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        var ini = Translation(temp, "a=b\r\n");

        Installer.Install(live, ini, Installer.Sha256Of(ini));

        var installed = Path.Combine(live, "data", "Localization", "portuguese_(brazil)", "global.ini");
        Assert.Equal(File.ReadAllBytes(ini), File.ReadAllBytes(installed));
        Assert.Equal("g_language = portuguese_(brazil)\r\n", File.ReadAllText(Path.Combine(live, "user.cfg")));
        Assert.False(File.Exists(installed + ".bak"));

        var inspection = Installer.Inspect(live);
        Assert.True(inspection.Installed);
        Assert.True(inspection.LanguageActive);
        Assert.Equal(Installer.Sha256Of(ini), inspection.Sha256);
    }

    [Fact]
    public void KeepsTheOtherLinesTheNameAndABackupOfTheUserCfg()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        var cfg = Path.Combine(live, "User.cfg");
        File.WriteAllText(cfg, "r_DepthOfField = 0\r\ng_languageAudio = english\r\n");

        Installer.Install(live, Translation(temp, "x"), Installer.Sha256Of(Translation(temp, "x")));

        Assert.Equal(["User.cfg", "User.cfg.bak"], Directory.EnumerateFiles(live, "User.cfg*").Select(Path.GetFileName).Order());
        Assert.Equal(
            "r_DepthOfField = 0\r\ng_languageAudio = english\r\ng_language = portuguese_(brazil)\r\n",
            File.ReadAllText(cfg));
        Assert.Equal("r_DepthOfField = 0\r\ng_languageAudio = english\r\n", File.ReadAllText(cfg + ".bak"));
    }

    [Fact]
    public void UpdatingKeepsTheOldTranslationAsBak()
    {
        using var temp = new TempFolder();
        var ptu = temp.Channel("StarCitizen", "PTU");
        var old = Translation(temp, "velha");
        var fresh = Translation(temp, "nova");

        Installer.Install(ptu, old, Installer.Sha256Of(old));
        Installer.Install(ptu, fresh, Installer.Sha256Of(fresh));

        var installed = Installer.IniPath(ptu);
        Assert.Equal(File.ReadAllBytes(fresh), File.ReadAllBytes(installed));
        Assert.Equal(File.ReadAllBytes(old), File.ReadAllBytes(installed + ".bak"));
        Assert.Equal(Installer.Sha256Of(fresh), Installer.Inspect(ptu).Sha256);
    }

    [Fact]
    public void InstallingTheSameFileOnlyFixesTheUserCfg()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        var ini = Translation(temp, "igual");
        Installer.Install(live, ini, Installer.Sha256Of(ini));
        File.WriteAllText(Path.Combine(live, "user.cfg"), "a = 1\r\n");

        Installer.Install(live, ini, Installer.Sha256Of(ini));

        Assert.False(File.Exists(Installer.IniPath(live) + ".bak"));
        Assert.Equal("a = 1\r\ng_language = portuguese_(brazil)\r\n", File.ReadAllText(Path.Combine(live, "user.cfg")));
    }

    [Fact]
    public void KeepsTheBomOfTheUserCfg()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        temp.Write(@"StarCitizen\LIVE\user.cfg", "a = 1\n", bom: true);
        var ini = Translation(temp, "x");

        Installer.Install(live, ini, Installer.Sha256Of(ini));

        var bytes = File.ReadAllBytes(Path.Combine(live, "user.cfg"));
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("a = 1\ng_language = portuguese_(brazil)\n", Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public void RemovesTheFolderAndTheLineAndKeepsTheRest()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        var cfg = Path.Combine(live, "user.cfg");
        File.WriteAllText(cfg, "r_DepthOfField = 0\r\n");
        var ini = Translation(temp, "x");
        Installer.Install(live, ini, Installer.Sha256Of(ini));

        Installer.Remove(live);

        Assert.False(Directory.Exists(Path.Combine(live, "data")));
        Assert.Equal("r_DepthOfField = 0\r\n", File.ReadAllText(cfg));
        Assert.Equal(ChannelInspection.Empty, Installer.Inspect(live));
    }

    [Fact]
    public void RemovingDeletesAUserCfgThatOnlyHadTheLanguage()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        var ini = Translation(temp, "x");
        Installer.Install(live, ini, Installer.Sha256Of(ini));

        Installer.Remove(live);

        Assert.False(File.Exists(Path.Combine(live, "user.cfg")));
        Assert.True(File.Exists(Path.Combine(live, "user.cfg.bak")));
    }

    [Fact]
    public void RemovingKeepsOtherLanguagesAndOtherFilesInData()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        temp.Write(@"StarCitizen\LIVE\data\Localization\english\global.ini", "en");
        temp.Write(@"StarCitizen\LIVE\user.cfg", "g_language = english\r\n");
        var ini = Translation(temp, "x");
        Installer.Install(live, ini, Installer.Sha256Of(ini));
        File.WriteAllText(Path.Combine(live, "user.cfg"), "g_language = english\r\n");

        Installer.Remove(live);

        Assert.True(File.Exists(Path.Combine(live, "data", "Localization", "english", "global.ini")));
        Assert.False(Directory.Exists(Installer.LanguageFolder(live)));
        Assert.Equal("g_language = english\r\n", File.ReadAllText(Path.Combine(live, "user.cfg")));
    }

    [Fact]
    public void RemovingWhatIsNotThereIsFine()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");

        Installer.Remove(live);

        Assert.False(Installer.Inspect(live).Installed);
    }

    [Fact]
    public void AReadOnlyUserCfgStaysReadOnly()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        var cfg = temp.Write(@"StarCitizen\LIVE\user.cfg", "a = 1\r\n");
        new FileInfo(cfg).IsReadOnly = true;
        var ini = Translation(temp, "x");

        Installer.Install(live, ini, Installer.Sha256Of(ini));

        Assert.True(new FileInfo(cfg).IsReadOnly);
        Assert.Equal("a = 1\r\ng_language = portuguese_(brazil)\r\n", File.ReadAllText(cfg));

        // Remover e instalar de novo regrava o .bak, que nasce gravável.
        Installer.Remove(live);
        Installer.Install(live, ini, Installer.Sha256Of(ini));

        Assert.True(new FileInfo(cfg).IsReadOnly);
        Assert.False(new FileInfo(cfg + ".bak").IsReadOnly);
        Assert.Equal("a = 1\r\n", File.ReadAllText(cfg + ".bak"));
    }

    [Fact]
    public void AFileInUseBecomesAFriendlyError()
    {
        using var temp = new TempFolder();
        var live = temp.Channel("StarCitizen", "LIVE");
        var old = Translation(temp, "velha");
        Installer.Install(live, old, Installer.Sha256Of(old));
        var fresh = Translation(temp, "nova");

        using (new FileStream(Installer.IniPath(live), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = Assert.Throws<InstallException>(() => Installer.Install(live, fresh, Installer.Sha256Of(fresh)));
            Assert.Contains("Feche o jogo", error.Message);
        }

        Assert.Equal(Installer.Sha256Of(old), Installer.Inspect(live).Sha256);
    }

    [Fact]
    public void AMissingChannelFolderIsAnError()
    {
        using var temp = new TempFolder();
        var ini = Translation(temp, "x");

        Assert.Throws<InstallException>(() => Installer.Install(temp.Combine("sumiu"), ini, Installer.Sha256Of(ini)));
    }
}
