using System.Text.Json;
using KuGou.Net.Abstractions.Models;
using KuGou.Net.Adapters.Common;
using KuGou.Net.Protocol.Raw;
using KuGou.Net.Protocol.Session;
using KuGou.Net.util;

namespace KuGou.Net.Clients;

public class ArtistClient(RawArtistApi rawApi, RawSearchApi rawSearchApi, KgSessionManager sessionManager)
{
    public Task<JsonElement> FollowAsync(string id)
    {
        var session = sessionManager.Session;
        return rawApi.FollowAsync(id, session.UserId, session.Token);
    }

    public Task<JsonElement> UnfollowAsync(string id)
    {
        var session = sessionManager.Session;
        return rawApi.UnfollowAsync(id, session.UserId, session.Token);
    }

    public Task<JsonElement> GetFollowNewSongsAsync(long lastAlbumId = 0, int pageSize = 30, int optSort = 1)
    {
        return rawApi.GetFollowNewSongsAsync(lastAlbumId, pageSize, optSort);
    }

    public Task<JsonElement> GetHonourAsync(string id, int page = 1, int pageSize = 30)
    {
        return rawApi.GetHonourAsync(id, page, pageSize);
    }

    public Task<JsonElement> GetListsAsync(int musician = 0, int sexType = 0, int type = 0, int hotSize = 30)
    {
        return rawApi.GetListsAsync(musician, sexType, type, hotSize);
    }

    public Task<JsonElement> GetSingerListAsync(int sexType = 0, int type = 0, int hotSize = 200)
    {
        return rawApi.GetListsAsync(0, sexType, type, hotSize);
    }

    public async Task<ArtistVideoResponse?> GetVideosAsync(string id, int page = 1, int pageSize = 30, string tag = "all")
    {
        var json = await rawApi.GetVideosAsync(id, page, pageSize, tag);
        return KgApiResponseParser.Parse<ArtistVideoResponse>(
            json, 
            AppJsonContext.Default.ArtistVideoResponse
        );
    }

    public async Task<SingerDetailResponse?> GetDetailAsync(string id)
    {
        var json = await rawSearchApi.GetSingerDetailAsync(id);
        return KgApiResponseParser.Parse<SingerDetailResponse>(
            json,
            AppJsonContext.Default.SingerDetailResponse
        );
    }

    public async Task<SingerAudioResponse?> GetAudiosAsync(string id, int page = 1, int pageSize = 30,
        string sort = "new")
    {
        var json = await rawSearchApi.GetSingerSongsAsync(sessionManager.Session.Dfid, id, page, pageSize, sort);
        return json.Deserialize(AppJsonContext.Default.SingerAudioResponse);
    }

    public async Task<ArtistAlbumResponse?> GetAlbumsAsync(string id, int page = 1, int pageSize = 30, string sort = "new")
    {
        var json = await rawApi.GetAlbumsAsync(id, page, pageSize, sort);
        return json.Deserialize(AppJsonContext.Default.ArtistAlbumResponse);
    }

    /// <summary>
    ///     获取歌手写真（对应 /kmr/v1/author/extend）。
    /// </summary>
    /// <param name="authorId">歌手 ID。</param>
    /// <returns>写真列表，无写真或请求失败时返回 null。</returns>
    public async Task<List<AuthorExtendItem>?> GetExtendImagesAsync(string authorId)
    {
        var json = await rawApi.GetExtendImagesAsync(authorId);
        if (json.ValueKind != JsonValueKind.Object)
            return null;

        if (!json.TryGetProperty("status", out var status) || status.GetInt32() != 1)
            return null;

        if (!json.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;

        return data.Deserialize(AppJsonContext.Default.ListAuthorExtendItem);
    }

    public Task<JsonElement> GetDetailRawAsync(string id)
    {
        return rawApi.GetDetailAsync(id);
    }

    public Task<JsonElement> GetAudiosRawAsync(string id, int page = 1, int pageSize = 30, string sort = "new")
    {
        return rawApi.GetAudiosAsync(id, page, pageSize, sort);
    }
}
