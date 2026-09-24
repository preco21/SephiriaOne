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
    }
    internal static int Run()
    {
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
        string remote = NameGradient.Format("Remote");
        var label = new Label { Text = remote };
        var presenter = new NameLabelPresenter(label, () => NamePresentation.Character(remote, false));
        presenter.Refresh();
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
        return count;
    }
}
