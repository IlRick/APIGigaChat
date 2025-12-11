using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ConsoleApp1.Models;
using Newtonsoft.Json;

namespace ConsoleApp1
{
    public class Program
    {
        static string ClientId = "";
        static string AutoriazationKey = "";

        static string Yandex_IamToken = ""; 
        static string Yandex_FolderId = ""; 

        static List<Request.Message> Dialog = new List<Request.Message>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("Выберите режим:");
            Console.WriteLine("1 — Диалог только с GigaChat");
            Console.WriteLine("2 — Диалог только с YandexGPT");
            Console.WriteLine("3 — GigaChat и YandexGPT общаются между собой");
            Console.Write("Ваш выбор: ");

            string mode = Console.ReadLine();
            Console.WriteLine();

            string Token = await GetToken(ClientId, AutoriazationKey);

            if (Token == null)
            {
                Console.WriteLine("Не удалось получить токен");
                return;
            }

            while (true)
            {
                Console.Write("Сообщение: ");
                string Message = Console.ReadLine();

                Dialog.Add(new Request.Message
                {
                    role = "user",
                    content = Message
                });

                if (mode == "1")
                {
                    await Mode_Giga(Token);
                }
                else if (mode == "2")
                {
                    await Mode_Yandex();
                }
                else if (mode == "3")
                {
                    await Mode_Both(Token);
                }
            }
        }
        static async Task Mode_Giga(string Token)
        {
            ResponseMessage Answer = await GetAnswer(Token, Dialog);
            string reply = Answer.choices[0].message.content;

            Console.WriteLine("GigaChat: " + reply);

            Dialog.Add(new Request.Message
            {
                role = "assistant",
                content = reply
            });
        }
        static async Task Mode_Yandex()
        {
            string reply = await YandexGPT(Yandex_IamToken, Yandex_FolderId, Dialog);

            Console.WriteLine("YandexGPT: " + reply);

            Dialog.Add(new Request.Message
            {
                role = "assistant",
                content = reply
            });
        }
        static async Task Mode_Both(string Token)
        {
            Dialog.Add(new Request.Message
            {
                role = "user",
                content = "Здравствуйте, давайте поговорим."
            });

            while (true)
            {
                ResponseMessage gigaResponse = await GetAnswer(Token, Dialog);
                string gigaReply = gigaResponse?.choices?[0]?.message?.content ?? "";

                Console.WriteLine("\nGigaChat: " + gigaReply);

                Dialog.Add(new Request.Message
                {
                    role = "assistant",
                    content = gigaReply
                });
                string yandexReply = await YandexGPT(Yandex_IamToken, Yandex_FolderId, Dialog);

                Console.WriteLine("YandexGPT: " + yandexReply);

                Dialog.Add(new Request.Message
                {
                    role = "assistant",
                    content = yandexReply
                });
                await Task.Delay(1500);
            }
        }
        public static async Task<ResponseMessage> GetAnswer(string token, List<Request.Message> dialog)
        {
            ResponseMessage responseMessage = null;
            string Url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

                using (HttpClient client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("Authorization", $"Bearer {token}");

                    Models.Request DataRequest = new Models.Request()
                    {
                        model = "GigaChat",
                        stream = false,
                        repetition_penalty = 1,
                        messages = dialog
                    };

                    string JsonContent = JsonConvert.SerializeObject(DataRequest);
                    Request.Content = new StringContent(JsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage Response = await client.SendAsync(Request);

                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        responseMessage = JsonConvert.DeserializeObject<ResponseMessage>(ResponseContent);
                    }
                }
            }
            return responseMessage;
        }
        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            string ReturnToken = null;
            string Url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyError) => true;
                using (HttpClient client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("RqUID", rqUID);
                    Request.Headers.Add("Authorization", $"Bearer {bearer}");
                    var Data = new List<KeyValuePair<string, string>>
                    {
                       new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                    };
                    Request.Content = new FormUrlEncodedContent(Data);
                    HttpResponseMessage Response = await client.SendAsync(Request);
                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        ResponseToken Token = JsonConvert.DeserializeObject<ResponseToken>(ResponseContent);
                        ReturnToken = Token.access_token;
                    }

                }
            }
            return ReturnToken;
        }
        public static async Task<string> YandexGPT(string iamToken, string folderId, List<Request.Message> dialog)
        {
            string Url = "https://llm.api.cloud.yandex.net/foundationModels/v1/completion";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {iamToken}");
                client.DefaultRequestHeaders.Add("x-folder-id", folderId);
                var yandexMessages = dialog.Select(m => new
                {
                    role = m.role,
                    text = m.content
                }).ToList();

                var reqBody = new
                {
                    modelUri = $"gpt://{folderId}/yandexgpt/latest",
                    completionOptions = new
                    {
                        stream = false,
                        temperature = 0.3
                    },
                    messages = yandexMessages
                };

                string json = JsonConvert.SerializeObject(reqBody);
                HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(Url, content);
                string resp = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return "Ошибка YandexGPT:\n" + resp;

                dynamic obj = JsonConvert.DeserializeObject(resp);

                return obj.result.alternatives[0].message.text;
            }
        }
    }
}
