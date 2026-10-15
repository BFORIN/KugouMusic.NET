using System.Text.Json.Serialization;

namespace KuGou.Net.Abstractions.Models;

/// <summary>
///     歌手写真扩展信息，对应 /kmr/v1/author/extend 的 data 数组元素。
/// </summary>
public record AuthorExtendItem
{
    /// <summary>
    ///     内部状态。
    /// </summary>
    [property: JsonPropertyName("__status")] public int Status { get; set; }

    /// <summary>
    ///     歌手基础信息。
    /// </summary>
    [property: JsonPropertyName("base")] public AuthorExtendBase? Base { get; set; }

    /// <summary>
    ///     写真图片列表。
    /// </summary>
    [property: JsonPropertyName("imgs")] public List<AuthorExtendImage> Images { get; set; } = new();
}

/// <summary>
///     歌手基础信息。
/// </summary>
public record AuthorExtendBase
{
    /// <summary>
    ///     歌手 ID。
    /// </summary>
    [property: JsonPropertyName("author_id")] public long AuthorId { get; set; }

    /// <summary>
    ///     歌手名称。
    /// </summary>
    [property: JsonPropertyName("author_name")] public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    ///     头像地址。
    /// </summary>
    [property: JsonPropertyName("avatar")] public string Avatar { get; set; } = string.Empty;
}

/// <summary>
///     单张歌手写真。
/// </summary>
public record AuthorExtendImage
{
    /// <summary>
    ///     图片 ID。
    /// </summary>
    [property: JsonPropertyName("id")] public long Id { get; set; }

    /// <summary>
    ///     写真图片地址。
    /// </summary>
    [property: JsonPropertyName("file")] public string File { get; set; } = string.Empty;

    /// <summary>
    ///     文件 Hash。
    /// </summary>
    [property: JsonPropertyName("hash")] public string Hash { get; set; } = string.Empty;

    /// <summary>
    ///     写真类型。
    /// </summary>
    [property: JsonPropertyName("type")] public int Type { get; set; }

    /// <summary>
    ///     发布时间。
    /// </summary>
    [property: JsonPropertyName("publish_time")] public string PublishTime { get; set; } = string.Empty;

    /// <summary>
    ///     来源标记。
    /// </summary>
    [property: JsonPropertyName("source")] public int Source { get; set; }

    /// <summary>
    ///     入库时间。
    /// </summary>
    [property: JsonPropertyName("addtime")] public string AddTime { get; set; } = string.Empty;
}
