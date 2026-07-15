namespace ImmichFrame.Core.Api
{
    public partial class ImmichApi
    {
        public ImmichApi(string url, HttpClient httpClient)
        {
            BaseUrl = url + "/api";
            _httpClient = httpClient;
            _settings = new Lazy<Newtonsoft.Json.JsonSerializerSettings>(CreateSerializerSettings);
        }

        public async Task<FileResponse> PlayAssetVideoWithRangeAsync(Guid id, string rangeHeader, CancellationToken cancellationToken = default)
        {
            var urlBuilder = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder.Append(_baseUrl);
            urlBuilder.Append("assets/");
            urlBuilder.Append(Uri.EscapeDataString(id.ToString("D", System.Globalization.CultureInfo.InvariantCulture)));
            urlBuilder.Append("/video/playback");

            using var request = new HttpRequestMessage(HttpMethod.Get, urlBuilder.ToString());
            request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/octet-stream"));
            request.Headers.TryAddWithoutValidation("Range", rangeHeader);

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            var headers = new Dictionary<string, IEnumerable<string>>();
            foreach (var item in response.Headers)
                headers[item.Key] = item.Value;
            if (response.Content?.Headers != null)
                foreach (var item in response.Content.Headers)
                    headers[item.Key] = item.Value;

            var status = (int)response.StatusCode;
            if (status == 200 || status == 206)
            {
                var stream = response.Content == null ? Stream.Null : await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return new FileResponse(status, headers, stream, null, response);
            }

            var error = response.Content == null ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response.Dispose();
            throw new ApiException($"Unexpected status code ({status}).", status, error, headers, null);
        }

        // Immich's memories "for" query parameter expects a bare calendar date (YYYY-MM-DD), not a
        // full ISO 8601 datetime. NSwag serializes the underlying DateTimeOffset with the "s" format
        // (e.g. "2026-07-15T14:30:00"), which Immich v3 rejects with a 400 ("expected ISO date
        // string (YYYY-MM-DD)"). Truncate the value down to just the date before sending.
        // Only the "for" query parameter is affected; date filters for search go through the JSON
        // body, which Newtonsoft serializes separately.
        partial void PrepareRequest(HttpClient client, HttpRequestMessage request, System.Text.StringBuilder urlBuilder)
        {
            TruncateToDate(urlBuilder, "for");
        }

        private static void TruncateToDate(System.Text.StringBuilder urlBuilder, string parameterName)
        {
            var url = urlBuilder.ToString();
            var token = parameterName + "=";

            var idx = url.IndexOf("?" + token, StringComparison.Ordinal);
            if (idx < 0) idx = url.IndexOf("&" + token, StringComparison.Ordinal);
            if (idx < 0) return;

            var valueStart = idx + 1 + token.Length;
            var valueEnd = url.IndexOf('&', valueStart);
            if (valueEnd < 0) valueEnd = url.Length;

            var encodedValue = url.Substring(valueStart, valueEnd - valueStart);
            var rawValue = Uri.UnescapeDataString(encodedValue);

            // NSwag serializes DateTimeOffset as "yyyy-MM-ddTHH:mm:ss..."; keep only the date part.
            var dateOnly = rawValue.Split('T')[0];
            if (dateOnly == rawValue) return;

            var fixedValue = Uri.EscapeDataString(dateOnly);
            if (fixedValue == encodedValue) return;

            urlBuilder.Remove(valueStart, valueEnd - valueStart);
            urlBuilder.Insert(valueStart, fixedValue);
        }
    }
}
