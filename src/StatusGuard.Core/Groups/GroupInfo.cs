namespace StatusGuard.Core.Groups;

/// <summary>VRChat group the user can represent (nameplate/banner).</summary>
public sealed record GroupInfo(
    string Id,
    string Name,
    string? ShortCode,
    string? Discriminator,
    string? IconUrl,
    string? BannerUrl,
    bool IsRepresenting)
{
    /// <summary>Display like "INTRO.1234" when short code exists.</summary>
    public string HeaderLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ShortCode))
            {
                return Name;
            }

            return string.IsNullOrWhiteSpace(Discriminator)
                ? ShortCode
                : $"{ShortCode}.{Discriminator}";
        }
    }
}
