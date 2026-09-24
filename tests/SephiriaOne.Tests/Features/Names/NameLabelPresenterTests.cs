using SephiriaOne;

internal static class NameLabelPresenterTests
{
    private sealed class Label : INameLabel
    {
        public bool IsAlive { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string Text { get; set; } = "";
        public NameLabelColor Color { get; set; } = new NameLabelColor(.2f, .4f, .6f, .8f);
        public bool RichText { get; set; }
        public bool OverrideColorTags { get; set; } = true;
        public bool EnableVertexGradient { get; set; } = true;
        public int LayoutUpdates;
        public void UpdateLayout() => LayoutUpdates++;
    }
    internal static int Run()
    {
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
        string remote = NameGradient.Format("Remote");
        var label = new Label { Text = remote };
        var presenter = new NameLabelPresenter(label, () => NamePresentation.Character(remote, false));
        presenter.Refresh();
        Check(label.LayoutUpdates == 1, "Rich-text rendering changes update measured name-label layout");
        presenter.Refresh();
        Check(label.LayoutUpdates == 1, "Unchanged label avoids redundant layout writes");
        Check(label.RichText && !label.OverrideColorTags && !label.EnableVertexGradient, "Native markup can render");
        presenter.Restore();
        Check(label.Text == remote, "Restoring settings must retain peer native markup");
        Check(!label.RichText && label.OverrideColorTags && label.EnableVertexGradient, "Native TMP settings restored");
        Check(label.Color.Equals(new NameLabelColor(.2f, .4f, .6f, .8f)), "Native color restored");

        var host = NamePresentation.HostSummary("Room", "Home", "Host", "Alias", "Chapter", "1", true);
        label = new Label { Text = host.PlainText! };
        var original = label.Color;
        presenter = new NameLabelPresenter(label, () => host);
        presenter.Refresh();
        Check(label.Color.Equals(original), "Mixed host summary must preserve room/chapter base RGB");
        Check(label.EnableVertexGradient, "Mixed host summary must preserve untagged vertex gradient");
        Check(label.Text == host.Text && !label.OverrideColorTags, "Host name tags override native base tint");
        label.Color = new NameLabelColor(.2f, .4f, .6f, .15f);
        presenter.Refresh(); presenter.Restore();
        Check(label.Text == host.PlainText, "Restore only local host markup");
        Check(label.Color.Equals(new NameLabelColor(.2f, .4f, .6f, .15f)), "Host fade alpha preserved");

        label = new Label { Text = "Own" };
        presenter = new NameLabelPresenter(label, () => NamePresentation.Character("Own", true));
        presenter.Refresh();
        Check(label.Color.Equals(new NameLabelColor(1, 1, 1, .8f)), "Name-only label can use white RGB");
        label.Color = new NameLabelColor(1, 1, 1, .25f);
        presenter.Restore();
        Check(label.Text == "Own" && label.Color.Equals(new NameLabelColor(.2f, .4f, .6f, .25f)), "Own markup and RGB restored with live alpha");
        presenter.Refresh(); label.Text = "Native changed";
        label.Color = new NameLabelColor(.9f, .1f, .3f, .4f);
        presenter.Restore();
        Check(label.Text == "Native changed" && label.Color.Equals(new NameLabelColor(.9f, .1f, .3f, .4f)), "Restoration cannot overwrite later native text or RGB");

        var registry = new PresentationRegistry();
        var styles = new NameStyleDirectory();
        var steamRow = new Label { Text = "Platform alias" };
        var otherRow = new Label { Text = "Platform alias" };
        registry.Register(steamRow, new NameLabelPresenter(steamRow, () => NamePresentation.Platform("Platform alias", styles.UseGradient(7))));
        registry.Register(otherRow, new NameLabelPresenter(otherRow, () => NamePresentation.Platform("Platform alias", styles.UseGradient(8))));
        registry.Tick();
        Check(steamRow.Text == "Platform alias", "Lobby row stays native until identity/style arrives");
        styles.Observe(7, NameGradient.Format("Character name"), false);
        registry.Tick();
        Check(steamRow.Text == NameGradient.Format("Platform alias") && otherRow.Text == "Platform alias",
            "Observed remote style decorates only the matching identity's original platform alias");
        styles.Clear(); registry.Tick();
        Check(steamRow.Text == "Platform alias" && !steamRow.RichText && steamRow.OverrideColorTags,
            "Departure removes local platform decoration and restores rendering flags");
        registry.Clear();

        string character = "Hero", open = "(", close = ")";
        var partyLabel = new Label { Text = "Hero(Alias)" };
        registry.Register(partyLabel, new NameLabelPresenter(partyLabel,
            () => NamePresentation.Party(character, "Alias", open, close, false)));
        registry.Tick();
        character = NameGradient.Format("Hero");
        registry.Tick();
        Check(partyLabel.Text == character + "(" + NameGradient.Format("Alias") + ")" && partyLabel.LayoutUpdates == 1,
            "Native character-name delivery refreshes a visible party label and its width");
        character = NameGradient.Format("Renamed");
        open = "【"; close = "】";
        registry.Tick();
        Check(partyLabel.Text == character + open + NameGradient.Format("Alias") + close && partyLabel.LayoutUpdates == 2,
            "Rename and language rebuild refresh the existing party label without replaying a game action");
        registry.Clear();
        Check(partyLabel.Text == character + open + "Alias" + close && partyLabel.LayoutUpdates == 3,
            "Unbinding keeps peer native gradient while restoring alias and measured layout");
        return count;
    }
}
