using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace metabase_exporter
{
    /// <summary>
    /// Manages the Metabase session token. Gets/renews token as needed.
    /// </summary>
    public class MetabaseSessionTokenManager
    {
        readonly MetabaseApiSettings _settings;
        readonly HttpClient _http;
        AsyncLazy<string> sessionToken;

        /// <summary>
        /// Manages the Metabase session token. Gets/renews token as needed.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="initialToken">
        /// Optional. If defined, the API clients attempts to use this token instead of creating a new one.
        /// </param>
        public MetabaseSessionTokenManager(MetabaseApiSettings settings, string initialToken)
        {
            this._settings = settings;
            var handler = new HttpClientHandler {
                ServerCertificateCustomValidationCallback = (a,b,c,d) => true,
            };
            this._http = new HttpClient(handler)
            {
                BaseAddress = settings.MetabaseApiUrl,
            };
            if (string.IsNullOrEmpty(initialToken) == false)
            {
                sessionToken = new AsyncLazy<string>(() => initialToken);
            } else
            {
                InvalidateSessionToken();
            }
        }

        public async Task<string> Send(Func<HttpRequestMessage> request)
        {
            var response = await SendWithSessionToken(request());
            return await response.Switch(
                (HttpResponse.Ok ok) => Task.FromResult(ok.Content),
                async (HttpResponse.Unauthorized u) =>
                {
                    InvalidateSessionToken();
                    var response2 = await SendWithSessionToken(request());
                    return response2.Switch(
                        (HttpResponse.Ok ok) => ok.Content,
                        (HttpResponse.Unauthorized u2) => throw new Exception("Got unauthorized response from Metabase")
                    );
                }
            );
        }

        public async Task<string> CurrentToken() => await sessionToken;

        async Task<HttpResponse> SendWithSessionToken(HttpRequestMessage request)
        {
            var token = await sessionToken;
            request.Headers.Add("X-Metabase-Session", token);
            var response = await _http.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new HttpResponse.Unauthorized();
            }
            else if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return new HttpResponse.Ok(content);
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error sending request to Metabase: {request.Method} {request.RequestUri}\nStatus code: {response.StatusCode}\nResponse: {responseContent}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        void InvalidateSessionToken()
        {
            // meh, rather use MethodImplOptions.Synchronized
            //Interlocked.CompareExchange(ref sessionToken, new AsyncLazy<string>(GetNewSessionToken), sessionToken);

            sessionToken = new AsyncLazy<string>(async () => await GetNewSessionToken());
        }


        async Task<string> GetNewSessionToken()
        {
            var endpoint = new Uri("/api/session", UriKind.Relative);
            var requestJson = JObj.Obj(new[]
            {
                JObj.Prop("username", _settings.MetabaseApiUsername),
                JObj.Prop("password", _settings.MetabaseApiPassword),
            });
            var requestContent = new StringContent(requestJson.ToString(), Encoding.UTF8, mediaType: "application/json");
            var response = await _http.PostAsync(endpoint, requestContent);
            if (response.IsSuccessStatusCode == false)
            {
                throw new Exception("Error logging in to Metabase API. Status code: " + response.StatusCode);
            }
            var rawContent = await response.Content.ReadAsStringAsync();
            try
            {
                var responseContent = JObject.Parse(rawContent);
                return (string)responseContent["id"];
            }
            catch (Exception e)
            {
                throw new Exception("Error parsing session token response from:\n" + rawContent, e);
            }
        }

        abstract class HttpResponse
        {
            private HttpResponse() { }

            public abstract T Switch<T>(Func<Ok, T> ok, Func<Unauthorized, T> unauthorized);

            public sealed class Unauthorized : HttpResponse
            {
                public override T Switch<T>(Func<Ok, T> ok, Func<Unauthorized, T> unauthorized) =>
                    unauthorized(this);
            }

            public sealed class Ok : HttpResponse
            {
                public string Content { get; }

                public Ok(string content)
                {
                    Content = content;
                }

                public override T Switch<T>(Func<Ok, T> ok, Func<Unauthorized, T> unauthorized) =>
                    ok(this);

            }
        }
    }
}
