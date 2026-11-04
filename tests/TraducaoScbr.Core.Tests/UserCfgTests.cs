using TraducaoScbr.Core.Translation;

namespace TraducaoScbr.Core.Tests;

public class UserCfgTests
{
    private const string Pt = "portuguese_(brazil)";

    [Fact]
    public void AnEmptyFileGetsTheLine()
    {
        Assert.Equal("g_language = portuguese_(brazil)\r\n", UserCfg.SetLanguage("", Pt));
    }

    [Fact]
    public void AppendsAfterTheOtherLinesKeepingThem()
    {
        // O User.cfg do LIVE do Eduardo: uma linha, sem quebra no fim.
        Assert.Equal(
            "r_DepthOfField = 0\r\ng_language = portuguese_(brazil)\r\n",
            UserCfg.SetLanguage("r_DepthOfField = 0", Pt));
        Assert.Equal(
            "r_DepthOfField = 0\ng_language = portuguese_(brazil)\n",
            UserCfg.SetLanguage("r_DepthOfField = 0\n", Pt));
        Assert.Equal(
            "a = 1\r\n\r\nb = 2\r\ng_language = portuguese_(brazil)\r\n",
            UserCfg.SetLanguage("a = 1\r\n\r\nb = 2\r\n", Pt));
    }

    [Fact]
    public void ReplacesTheLanguageInPlaceAndDropsRepeats()
    {
        Assert.Equal(
            "a = 1\r\ng_language = portuguese_(brazil)\r\nb = 2\r\n",
            UserCfg.SetLanguage("a = 1\r\ng_language = english\r\nb = 2\r\nG_LANGUAGE=french_(france)\r\n", Pt));
    }

    [Fact]
    public void DoesNotTouchGLanguageAudio()
    {
        // Os .cmd da equipe trocam esta linha por engano.
        const string cfg = "g_languageAudio = english\r\n";

        Assert.Equal("g_languageAudio = english\r\ng_language = portuguese_(brazil)\r\n", UserCfg.SetLanguage(cfg, Pt));
        Assert.Null(UserCfg.GetLanguage(cfg));
    }

    [Fact]
    public void KeepsALineThatAlreadyChoosesTheLanguage()
    {
        const string cfg = "  g_language=Portuguese_(Brazil)  \r\nx = 1\r\n";

        Assert.Equal(cfg, UserCfg.SetLanguage(cfg, Pt));
        Assert.Equal("Portuguese_(Brazil)", UserCfg.GetLanguage(cfg));
    }

    [Theory]
    [InlineData("g_language = portuguese_(brazil)", "portuguese_(brazil)")]
    [InlineData("g_language portuguese_(brazil)", "portuguese_(brazil)")]
    [InlineData("g_language = \"portuguese_(brazil)\"", "portuguese_(brazil)")]
    [InlineData("g_language = english\ng_language = portuguese_(brazil)", "portuguese_(brazil)")]
    [InlineData("-- g_language = portuguese_(brazil)", null)]
    [InlineData("; g_language = portuguese_(brazil)", null)]
    [InlineData("", null)]
    public void ReadsTheLanguage(string cfg, string? expected)
    {
        Assert.Equal(expected, UserCfg.GetLanguage(cfg));
    }

    [Fact]
    public void RemovesOnlyThePortugueseLine()
    {
        Assert.Equal("a = 1\r\nb = 2\r\n", UserCfg.RemoveLanguage("a = 1\r\ng_language = portuguese_(brazil)\r\nb = 2\r\n", Pt));
        Assert.Equal("g_language = english\n", UserCfg.RemoveLanguage("g_language = english\n", Pt));
        Assert.Equal("g_languageAudio = english\n", UserCfg.RemoveLanguage("g_languageAudio = english\ng_language = portuguese_(brazil)\n", Pt));
    }

    [Fact]
    public void AFileWithOnlyTheLanguageBecomesEmpty()
    {
        Assert.Equal("", UserCfg.RemoveLanguage("g_language = portuguese_(brazil)\r\n", Pt));
        Assert.Equal("", UserCfg.RemoveLanguage("\r\ng_language = portuguese_(brazil)\r\n\r\n", Pt));
    }

    [Fact]
    public void RemovingWithoutTheLineChangesNothing()
    {
        const string cfg = "a = 1\r\nb = 2";

        Assert.Same(cfg, UserCfg.RemoveLanguage(cfg, Pt));
    }
}
