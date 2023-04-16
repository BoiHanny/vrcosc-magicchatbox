using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public static class OpenAIClient
    {
        private static HttpClient _httpClient;

        public static void LoadOpenAIClient()
        {
            _httpClient = new HttpClient();
            UpdateAuthorizationHeader();
            ViewModel.Instance.OpenAIAPIModels = new ObservableCollection<string>
                {
                    "gpt-3.5-turbo",
                    "gpt-4"
                };
            ViewModel.Instance.OpenAIAPISelectedModel = ViewModel.Instance.OpenAIAPISelectedModel ?? "gpt-3.5-turbo";
            ViewModel.Instance.OpenAIAPIUrl = "https://api.openai.com/v1/chat/completions";
            ViewModel.Instance.OpenAIModerationUrl = "https://api.openai.com/v1/moderations";

        }

        public static void UpdateAuthorizationHeader()
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ViewModel.Instance.OpenAIAPIKey);
        }

        public static async Task<bool> ModerateContentAsync(string input)
        {
            string apiUrl = ViewModel.Instance.OpenAIModerationUrl;

            var requestData = new
            {
                input = input
            };

            var jsonContent = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                    return responseObject.results[0].flagged;
                }
                else
                {
                    throw new Exception($"Request failed with status code: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle network issues (e.g., retry or log)
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                return false;
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                return false;
            }
        }


        public static int CountTokens(string input, string model = "gpt-3.5-turbo")
        {
            if (model == "gpt-3.5-turbo" || model == "gpt-4")
            {
                // Split the input string into separate messages
                string[] messages = input.Split(new string[] { "<im_start>", "<im_end>" }, StringSplitOptions.RemoveEmptyEntries);

                // Initialize the token count
                int numTokens = 0;

                // Iterate through the messages and count tokens
                foreach (string message in messages)
                {
                    numTokens += 4; // every message follows <im_start>{role/name}\n{content}<im_end>\n
                    string[] words = message.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string word in words)
                    {
                        // Add the length of the word in tokens
                        numTokens += word.Length;
                    }

                    if (message.Contains("name:")) // if there's a name, the role is omitted
                    {
                        numTokens -= 1; // role is always required and always 1 token
                    }
                }

                numTokens += 2; // every reply is primed with <im_start>assistant
                return numTokens;
            }
            else
            {
                throw new NotImplementedException($"CountTokens() is not presently implemented for model {model}.");
            }
        }


        public static async Task<string> TestAPIConnection()
        {
            try
            {
                ChatModelMsg action = ViewModel.Instance.OpenAIAPIBuiltInActions.FirstOrDefault(a => a.FriendlyName == "SYSTEM_API_CHECK");

                ChatModelMsg response = await ExecuteActionAsync(action);


                if (response.Completed == true)
                {
                    if (response.Content.Trim().Contains("Hi", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{ViewModel.Instance.OpenAIAPISelectedModel} said 'Hi' to you {ViewModel.Instance.OpenAIUsedTokens}";
                    }
                    else
                    {
                        Logging.WriteInfo($"Unexpected response from API while testing connection: {response}");
                        return $"Unexpected response while testing connection.";
                    }
                }
                else
                {
                    if(response.ex.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Please check your API key";
                    }
                    else
                        return $"{response.ex}";
                }

               
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                return $"Unexpected response while testing connection.";
            }
        }

        public static async Task<ChatModelMsg> ExecuteActionAsync(ChatModelMsg action, string? content = null)
        {
            try
            {
                if (action != null)
                {
                    List<ChatModelMsg> messages = new List<ChatModelMsg>
                    {
                        action
                    };

                    if (content != null)
                    {
                        messages.Add(new ChatModelMsg
                        {
                            Role = ChatModelMsg.RoleType.User,
                            Content = content
                        });
                    }
                    if (ViewModel.Instance.IntelliChatModeration)
                    {
                        foreach (ChatModelMsg item in messages)
                    {

                            // Check if the content violates usage policies
                            bool flagged = await ModerateContentAsync(item.Content);
                            if (flagged)
                            {

                                action.ChatModerationFlagged = true;
                                break; // Set the flag and return the action
                            }
                            else
                            {
                                action.ChatModerationFlagged = false;
                            }
                        }
                    }
                    else
                        action.ChatModerationFlagged = false;

                    if((bool)action.ChatModerationFlagged)
                    {
                        // Handle flagged content, for example, return a message saying the content is inappropriate
                        action.Content = "The generated content violates usage policies and cannot be displayed.";
                        Logging.WriteException(new Exception("The generated content violates usage policies and cannot be displayed."), makeVMDump: true, MSGBox: true);
                        action.Completed = false;
                        return action;
                    }
                    else
                    {
                        int GenerationTokens = action.maxTokens ?? 30;
                        double GenerationTemperature = action.temperature ?? 0.7;
                        // Generate completion using the action
                        ChatModelMsg result = await GenerateCompletionAsync(messages, GenerationTokens: GenerationTokens, GenerationTemperature);

                        if (result.Completed == true)
                        {
                            // Process the result here and return a string response
                            return result;
                        }
                        else
                        {
                            return result;
                        }
                    }
                    
                }
                else
                {
                    action.Completed = false;
                    action.Content = "Action not found";
                    Logging.WriteException(new Exception("Action not found"), makeVMDump: true, MSGBox: true);
                    return action;
                }
            }
            catch (Exception ex)
            {
                action.Completed = false;

                if (ex.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    action.Content = "Please check your API key";
                }
                else
                    action.Content = $"{ex}";
                action.ex = ex.Message;
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                return action;
            }
        }


        public static async Task<ChatModelMsg> GenerateCompletionAsync(List<ChatModelMsg> messages, int GenerationTokens, double temperature)
        {
            string apiUrl = ViewModel.Instance.OpenAIAPIUrl;
            string model = ViewModel.Instance.OpenAIAPISelectedModel;
            UpdateAuthorizationHeader();

            var requestData = new
            {
                model = model,
                messages = messages.Select(m => new { role = m.Role.ToString().ToLower(), content = m.Content }).ToList(),
                max_tokens = GenerationTokens,
                temperature = temperature
            };

            var jsonContent = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            ChatModelMsg result = new ChatModelMsg();
            try
            {
                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                    string generatedContent = responseObject["choices"][0]["message"]["content"];
                    int promptTokens = responseObject["usage"]["prompt_tokens"];
                    int completionTokens = responseObject["usage"]["completion_tokens"];
                    int MSGtotalTokens = responseObject["usage"]["total_tokens"];

                    result = new ChatModelMsg
                    {
                        Role = ChatModelMsg.RoleType.Assistant,
                        Content = generatedContent,
                        PromptTokens = promptTokens,
                        CompletionTokens = completionTokens,
                        TotalTokens = MSGtotalTokens
                    };


                    if(result.TotalTokens > 0)
                    ViewModel.Instance.OpenAIUsedTokens = (int)(ViewModel.Instance.OpenAIUsedTokens + result.TotalTokens);
                    result.Completed = true;
                    return result;
                }
                else
                {
                    Exception X = new Exception($"Request failed with status code: {response.StatusCode}");
                    Logging.WriteException(X, makeVMDump: true, MSGBox: false);
                    result.Completed = false;
                    result.ex = X.Message;
                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle network issues (e.g., retry or log)
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                result.Completed = false;
                result.ex = ex.Message;
                return result;
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                result.Completed = false;
                result.ex = ex.Message;
                return result;
            }
        }


    }
}
