using SephiriaOne;

internal static class NamePresentationTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool ok) { if (!ok) throw new Exception("Name presentation identity regression"); count++; }
        var own = NamePresentation.Platform("Platform alias", true);
        Check(own.Text == NameGradient.Format("Platform alias") && own.Styled);
        var unrelated = NamePresentation.Platform("Platform alias", false);
        Check(unrelated.Text == "Platform alias" && !unrelated.Styled);
        Check(NamePresentation.Character("Remote", false).Text == "Remote");
        Check(!NamePresentation.Character("Remote", false).Styled);
        Check(NamePresentation.Character(NameGradient.Format("Remote"), false).Styled);
        var host = NamePresentation.HostSummary("Room", "Home", "Host", "Alias", "Chapter", "1", true);
        Check(host.Text == "Room: Home\nHost: " + NameGradient.Format("Alias") + "\nChapter: 1");
        Check(host.PlainText == "Room: Home\nHost: Alias\nChapter: 1");
        var otherHost = NamePresentation.HostSummary("Room", "Home", "Host", "Alias", "Chapter", "1", false);
        Check(otherHost.Text == host.PlainText && !otherHost.Styled);
        var party = NamePresentation.Party("Hero", "Platform alias", "(", ")", true);
        Check(party.Text == NameGradient.Format("Hero") + "(" + NameGradient.Format("Platform alias") + ")");
        Check(party.PlainText == "Hero(Platform alias)" && party.PreserveBaseColor);
        var remoteParty = NamePresentation.Party(NameGradient.Format("Remote"), "Alias", "【", "】", false);
        Check(remoteParty.Text == NameGradient.Format("Remote") + "【" + NameGradient.Format("Alias") + "】");
        Check(remoteParty.PlainText == NameGradient.Format("Remote") + "【Alias】");
        var ordinaryParty = NamePresentation.Party("Other", "Alias", "(", ")", false);
        Check(ordinaryParty.Text == "Other(Alias)" && !ordinaryParty.Styled);
        Check(NamePresentation.Party("Solo", null, "(", ")", true).Text == NameGradient.Format("Solo"));
        Check(NamePresentation.Party(null, "Alias", "(", ")", false).Text == null);
        return count;
    }
}
