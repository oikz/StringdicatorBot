using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Victoria;

namespace Stringdicator.Util;

public static class LavaTrackExtensions {
    public static async Task<string> FetchArtworkAsync(this LavaTrack lavaTrack) {
        var url = $"https://img.youtube.com/vi/{lavaTrack.Id}/maxresdefault.jpg";

        var (httpMethod, httpCompletionOption, fallbackUrl) = lavaTrack.Url.ToLower().Contains("youtube")
            ? (HttpMethod.Head, HttpCompletionOption.ResponseHeadersRead,
                $"https://img.youtube.com/vi/{lavaTrack.Id}/hqdefault.jpg")
            : (HttpMethod.Get, HttpCompletionOption.ResponseContentRead, null);

        var responseMessage = await new HttpClient().SendAsync(new HttpRequestMessage {
            Method = httpMethod,
            RequestUri = new Uri(url)
        }, httpCompletionOption);

        if (!responseMessage.IsSuccessStatusCode) {
            return fallbackUrl ?? throw new Exception(responseMessage.ReasonPhrase);
        }

        if (lavaTrack.Url.Contains("youtube", StringComparison.CurrentCultureIgnoreCase)) {
            return url;
        }

        using var content = responseMessage.Content;
        await using var stream = await content.ReadAsStreamAsync();

        var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.TryGetProperty("thumbnail_url", out var url2)
            ? $"{url2}"
            : url;
    }
}