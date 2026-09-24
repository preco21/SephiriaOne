#nullable enable
namespace SephiriaOne
{
    internal readonly struct NameView
    {
        public readonly string? Text;
        public readonly bool Styled;
        public readonly string? PlainText;
        public readonly bool PreserveBaseColor;
        public NameView(string? text, bool styled, string? plainText = null, bool preserveBaseColor = false)
        { Text = text; Styled = styled; PlainText = plainText; PreserveBaseColor = preserveBaseColor; }
    }

    internal static class NamePresentation
    {
        // Ownership is supplied by a stable native identity, never name equality.
        public static NameView Platform(string? nativeNickname, bool own) =>
            nativeNickname == null ? new NameView(null, false) :
            new NameView(own ? NameGradient.Format(nativeNickname) : nativeNickname, own, nativeNickname);

        public static NameView Character(string? nativeName, bool own) => nativeName == null
            ? new NameView(null, false)
            : new NameView(own ? NameGradient.Format(nativeName) : nativeName, own || NameGradient.Plain(nativeName) != nativeName,
                own ? NameGradient.Plain(nativeName) : nativeName);

        public static NameView HostSummary(string roomLabel, string room, string hostLabel, string host, string chapterLabel, string chapter, bool own)
        {
            string plain = $"{roomLabel}: {room}\n{hostLabel}: {host}\n{chapterLabel}: {chapter}";
            string shown = $"{roomLabel}: {room}\n{hostLabel}: {(own ? NameGradient.Format(host ?? "") : host)}\n{chapterLabel}: {chapter}";
            return new NameView(shown.TrimEnd(), own, plain.TrimEnd(), preserveBaseColor: true);
        }

        public static NameView Party(string? nativeName, string? nickname, string open, string close, bool own)
        {
            if (string.IsNullOrEmpty(nativeName)) return new NameView(null, false);
            NameView character = Character(nativeName, own);
            bool styled = own || NameStyleDirectory.IsStyled(nativeName);
            string suffix = string.IsNullOrEmpty(nickname) ? "" : open + nickname + close;
            string shownSuffix = string.IsNullOrEmpty(nickname) ? "" : open + (styled ? NameGradient.Format(nickname!) : nickname) + close;
            return new NameView(character.Text + shownSuffix, styled, character.PlainText + suffix, preserveBaseColor: true);
        }
    }
}
