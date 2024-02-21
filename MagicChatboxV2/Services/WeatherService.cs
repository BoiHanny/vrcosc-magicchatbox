using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MagicChatboxV2.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.openweathermap.org/data/2.5/weather?";
        private string _apiKey;

        public string ApiKey
        {
            get => _apiKey;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("API key cannot be null or whitespace.", nameof(value));

                _apiKey = value;
            }
        }

        public WeatherService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }

        public async Task<WeatherResponse> GetWeatherAsync(string location)
        {
            var requestUrl = $"{BaseUrl}q={location}&appid={_apiKey}&units=metric";
            return await GetWeatherDataAsync(requestUrl);
        }

        public async Task<WeatherResponse> GetWeatherByCoordinatesAsync(double latitude, double longitude)
        {
            var requestUrl = $"{BaseUrl}lat={latitude}&lon={longitude}&appid={_apiKey}&units=metric";
            return await GetWeatherDataAsync(requestUrl);
        }

        private async Task<WeatherResponse> GetWeatherDataAsync(string url)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WeatherResponse>(responseContent);
            }
            else
            {
                // Here you can handle various response codes differently if needed
                throw new HttpRequestException($"Error fetching weather data: {response.ReasonPhrase}");
            }
        }
    }

    public class WeatherResponse
    {
        [JsonProperty("weather")]
        public List<WeatherDescription> Weather { get; set; }

        [JsonProperty("main")]
        public MainWeatherInfo Main { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        public class WeatherDescription
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("main")]
            public string Main { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("icon")]
            public string Icon { get; set; }
        }

        public class MainWeatherInfo
        {
            [JsonProperty("temp")]
            public double Temp { get; set; }

            [JsonProperty("feels_like")]
            public double FeelsLike { get; set; }

            [JsonProperty("temp_min")]
            public double TempMin { get; set; }

            [JsonProperty("temp_max")]
            public double TempMax { get; set; }

            [JsonProperty("pressure")]
            public int Pressure { get; set; }

            [JsonProperty("humidity")]
            public int Humidity { get; set; }
        }
    }

}
