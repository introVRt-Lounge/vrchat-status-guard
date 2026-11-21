namespace VrchatPrivacy.Core.Worlds;

public sealed record WorldInfo(
    string Id,
    string Name,
    string? ThumbnailImageUrl,
    string? AuthorName);
