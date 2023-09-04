using System.Text.Json;

namespace PulsoidOAuthLib
{
    public class OAuthService
    {
        private const string AuthorizeUrl = "https://pulsoid.net/oauth2/authorize";
        private const string TokenUrl = "https://pulsoid.net/oauth2/token";
        private const string ValidateUrl = "https://dev.pulsoid.net/api/v1/token/validate";

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;

        private DateTime _accessTokenExpiry;
        private string _accessToken;
        private string _refreshToken;

        public OAuthService(string clientId, string clientSecret, string redirectUri)
        {
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
            _redirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));
        }

        public string GetAuthorizationUrl(string scope, string state)
        {
            return $"{AuthorizeUrl}?response_type=code&client_id={_clientId}&redirect_uri={_redirectUri}&scope={scope}&state={state}";
        }

        public async Task<bool> IsAccessTokenValid()
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
                var response = await httpClient.GetAsync(ValidateUrl);
                return response.IsSuccessStatusCode;
            }
        }

        public async Task<string> GetValidAccessTokenAsync()
        {
            if (_accessToken == null || DateTime.UtcNow > _accessTokenExpiry)
            {
                if (_refreshToken != null)
                {
                    (_accessToken, _refreshToken) = await RefreshTokenAsync(_refreshToken);
                }
                else
                {
                    throw new Exception("Token has expired and no refresh token is available.");
                }
            }

            return _accessToken;
        }

        public async Task<(string accessToken, string refreshToken)> ExchangeCodeForTokensAsync(string code)
        {
            using (var httpClient = new HttpClient())
            {
                var requestData = new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["redirect_uri"] = _redirectUri
                };

                var response = await httpClient.PostAsync(TokenUrl, new FormUrlEncodedContent(requestData));
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get tokens. Response: {content}");
                }

                var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                _accessToken = root.GetProperty("access_token").GetString();
                _refreshToken = root.GetProperty("refresh_token").GetString();
                _accessTokenExpiry = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32());

                return (_accessToken, _refreshToken);
            }
        }

        public async Task<(string accessToken, string refreshToken)> RefreshTokenAsync(string refreshToken)
        {
            using (var httpClient = new HttpClient())
            {
                var requestData = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret
                };

                var response = await httpClient.PostAsync(TokenUrl, new FormUrlEncodedContent(requestData));
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to refresh token. Response: {content}");
                }

                var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                _accessToken = root.GetProperty("access_token").GetString();
                _refreshToken = root.GetProperty("refresh_token").GetString();
                _accessTokenExpiry = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32());

                return (_accessToken, _refreshToken);
            }
        }
    }
}
