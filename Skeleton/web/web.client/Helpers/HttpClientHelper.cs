using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using model;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;

namespace web.client.Helpers
{
    [Serializable]
    public class HttpClientHelper
    {
        private readonly string _webServiceUrl;
        private readonly string _accessToken;
        private readonly string _authenticationHeader;

        private AzureAdOptions AdConfiguration { get; }

        public HttpClientHelper(string webServiceUrl, AzureAdOptions configuration)
        {
            _webServiceUrl = webServiceUrl;
            AdConfiguration = configuration;
            _accessToken = this.GetAccessToken().GetAwaiter().GetResult();
            _authenticationHeader = "Bearer";
        }

        private async Task<string> GetAccessToken()
        {
            try
            {
                var authority = string.Format(CultureInfo.InvariantCulture,
                    AdConfiguration.Instance, AdConfiguration.Tenant);
                var authContext = new AuthenticationContext(authority, TokenCache.DefaultShared);
                var credentials = new ClientCredential(AdConfiguration.ClientId, AdConfiguration.ClientSecret);
                var authResult = await authContext.AcquireTokenAsync(AdConfiguration.Audience, credentials);
                return authResult?.AccessToken;
            }
            catch (Exception e)
            {
                //No token is available
                throw e;
            }
        }




        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public async Task<T> GetItemAsync<T>(string query)
        {
            var uri = String.IsNullOrEmpty(query)? _webServiceUrl : $"{_webServiceUrl}?{query}";

            var hndlr = new HttpClientHandler { UseDefaultCredentials = true };

            using (var client = new HttpClient(hndlr))
            {
                client.BaseAddress = new Uri(uri);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Set access token
                SetAccessToken(client.DefaultRequestHeaders);

                HttpResponseMessage response = await client.GetAsync(uri).ConfigureAwait(false);

                if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest
                    && response.StatusCode != HttpStatusCode.BadGateway &&
                    response.StatusCode != HttpStatusCode.NoContent)
                {
                    using (HttpContent content = response.Content)
                    {
                        string contents = await content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(contents))
                        {
                            return JsonConvert.DeserializeObject<T>(contents);
                        }
                    }
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    throw new AuthenticationException($"Endpoint {uri} returned status code: {response.StatusCode}");
                }
            }

            return default(T);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <returns></returns>
        public async Task<T> PostItemAsync<T>(object payload)
        {

            var hndlr = new HttpClientHandler { UseDefaultCredentials = true };

            using (var client = new HttpClient(hndlr))
            {
                client.BaseAddress = new Uri(_webServiceUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Set access token
                SetAccessToken(client.DefaultRequestHeaders);

                HttpResponseMessage response = await client.PostAsync(client.BaseAddress, new JsonContent(payload)).ConfigureAwait(false);

                if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest
                    && response.StatusCode != HttpStatusCode.BadGateway &&
                    response.StatusCode != HttpStatusCode.NoContent)
                {
                    using (HttpContent content = response.Content)
                    {
                        string contents = await content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(contents))
                        {
                            return JsonConvert.DeserializeObject<T>(contents);
                        }
                    }
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    throw new AuthenticationException($"Endpoint {_webServiceUrl} returned status code: {response.StatusCode}");
                }
            }

            return default(T);
        }



        public async Task<T> PutItemAsync<T>(string uri, object payload)
        {
            var hndlr = new HttpClientHandler { UseDefaultCredentials = true };

            using (var client = new HttpClient(hndlr))
            {
                client.BaseAddress = new Uri(uri);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Set access token
                SetAccessToken(client.DefaultRequestHeaders);

                HttpResponseMessage response = await client.PostAsync(client.BaseAddress, new JsonContent(payload)).ConfigureAwait(false);

                if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest
                    && response.StatusCode != HttpStatusCode.BadGateway &&
                    response.StatusCode != HttpStatusCode.NoContent)
                {
                    using (HttpContent content = response.Content)
                    {
                        string contents = await content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(contents))
                        {
                            return JsonConvert.DeserializeObject<T>(contents);
                        }
                    }
                }
                else if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    throw new AuthenticationException($"Endpoint {uri} returned status code: {response.StatusCode}");
                }
            }

            return default(T);
        }


        private void SetAccessToken(HttpRequestHeaders clientHeader)
        {
            if (!string.IsNullOrEmpty(_accessToken))
            {
                clientHeader.Authorization = new AuthenticationHeaderValue(_authenticationHeader, _accessToken);
            }
        }
    }



    internal class JsonContent : StringContent
    {
        public JsonContent(object obj)
            : base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
        {
        }
    }
}
