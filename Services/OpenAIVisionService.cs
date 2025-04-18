using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SPCHR.Services
{
    public interface IOpenAIVisionService
    {
        string ApiKey { get; }
        Task<string> EnhanceText(string recognizedText, string screenshotPath);
    }

    public class OpenAIVisionService : IOpenAIVisionService
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _endpoint;

        public string ApiKey => _apiKey;

        public OpenAIVisionService(string apiKey, string model, string endpoint)
        {
            _apiKey = apiKey;
            _model = model ?? "o4-mini";
            _endpoint = endpoint ?? "https://api.openai.com/";
        }

        public async Task<string> EnhanceText(string request, string screenshotPath)
        {
            try
            {
                if (!File.Exists(screenshotPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Screenshot file not found: {screenshotPath}");
                    return request;
                }

                // Convert image to base64 string
                byte[] imageBytes = File.ReadAllBytes(screenshotPath);
                string base64Image = Convert.ToBase64String(imageBytes);
                string systemPrompt = @"You are an AI assistant that analyzes both text transcription and screenshots. 
                                                        Your task is to correct any errors in the transcription based 
                                                        on what you see in the screenshot, and provide a clean, accurate version of the text. 
                                                        Only return the corrected text without any explanation.";

                systemPrompt = @"Use the information in the attached photo and complete the REQUEST.";

                // Create the request object for OpenAI's API
                var requestData = new
                {
                    model = _model,
                    messages = new object[]
                    {
                        new { role = "system", content =  systemPrompt},
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = $"REQUEST: {request}" },
                                new
                                {
                                    type = "image_url",
                                    image_url = new
                                    {
                                        url = $"data:image/png;base64,{base64Image}"
                                    }
                                }
                            }
                        }
                    },
                    max_completion_tokens = 5000
                };
                
                // Serialize the request to JSON
                string jsonRequest = JsonConvert.SerializeObject(requestData);
                
                // Create an HTTP client and send the request to OpenAI
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{_endpoint}v1/chat/completions", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    // Parse the response using Newtonsoft.Json
                    dynamic responseObj = JsonConvert.DeserializeObject(jsonResponse);
                    string enhancedText = responseObj.choices[0].message.content.ToString().Trim();
                    
                    System.Diagnostics.Debug.WriteLine($"Original: {request}");
                    System.Diagnostics.Debug.WriteLine($"Enhanced: {enhancedText}");
                    
                    return enhancedText;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"OpenAI API error: {response.StatusCode}, {errorContent}");
                    return request;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EnhanceText: {ex.Message}");
                return request; // Return original text if there's an error
            }
        }
    }
} 