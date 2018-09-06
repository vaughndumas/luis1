using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using System.Threading;
using System.Collections.Generic;
using Pathoschild.Http.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace Scorable.Dialogs
{
    [Serializable]
    [LuisModel("fc53d47a-f592-467f-ba9a-8113d71f01ea", "f578987c5c994e90adb9131836b9c533")]
    public class LuisDialog : LuisDialog<object>
    {
        private string v_langToUse;

        public string V_langToUse { get => v_langToUse; set => v_langToUse = value; }
        private string translator_ApiKey = "bdb8798b99f14ab38808f23e928402b2";
        private string translator_targetLang = "en";
        private string translator_accessToken;

        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string message = $"Sorry, I did not understand '{result.Query}'. Type 'help' if you need assistance.";

            // alternatively, you could forward to QnA Dialog if no intent is found

            await context.PostAsync(message);

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("greeting")]
        public async Task Greeting(IDialogContext context, LuisResult result)
        {
            string message = $"Hello there";

            await context.PostAsync(message);

            context.Wait(this.MessageReceived);
        }

        private ResumeAfter<object> after()
        {
            return null;
        }

        [LuisIntent("weather")]
        public async Task Middle(IDialogContext context, LuisResult result)
        {
            await this.DoWeather(context, result);

            context.Wait(this.MessageReceived);

        }

        [LuisIntent("joke")]
        public async Task Joke(IDialogContext context, LuisResult result)
        {
            // confirm we hit joke intent
            string message = $"Let's see...I know a good joke...";

            await context.PostAsync(message);

            await context.Forward(new JokeDialog(), ResumeAfterJokeDialog, context.Activity, CancellationToken.None);
        }

        [LuisIntent("academic")]
        [LuisIntent("question")]
        public async Task QnA(IDialogContext context, LuisResult result)
        {
            // confirm we hit QnA
            string message = $"Routing to QnA... ";
            await context.PostAsync(message);

            var userQuestion = (context.Activity as Activity).Text;
            // Detect the language
            ITextAnalyticsAPI client = new TextAnalyticsAPI();
            client.AzureRegion = AzureRegions.Westus;
            client.SubscriptionKey = "d220c5d87b5d486d94d1ef065bd2ba71";
            var connector = new ConnectorClient(new Uri(context.Activity.ServiceUrl));
            var v_fromlang = "en";

            // Translate if needed
            LanguageBatchResult v_result = client.DetectLanguage(
                                                     new BatchInput(
                                                         new List<Input>()
                                                         {
                                                         new Input("1", userQuestion)
                                                         }));
            foreach (var v_document in v_result.Documents)
            {
                v_fromlang = v_document.DetectedLanguages[0].Iso6391Name;
                if (v_fromlang != "en")
                {
                    var v_translatedtext = await TranslateText(userQuestion,
                                                               this.translator_targetLang,
                                                               this.translator_accessToken);
                    (context.Activity as Activity).Text = v_translatedtext;
                }
            }

            // Update the text to send
            await context.Forward(new QnaDialog(), ResumeAfterQnA, context.Activity, CancellationToken.None);
            //context.Wait(this.MessageReceived);
        }

        private async Task DoWeather(IDialogContext context, LuisResult x_results)
        {
            IList<EntityRecommendation> v_entities = x_results.Entities;
            string v_call;
            IClient client;
            string v_content;
            var v_weatherdef = new { Name = "" };
            int v_found = 0;

            foreach (EntityRecommendation v_item in v_entities)
            {
                if (v_item.Type == "City.names")
                {
                    v_found = 1;
                    client = new FluentClient("http://api.wunderground.com");
                    v_call = "/api/4aa3669ef7360f4b/conditions/q/Australia/"
                           + v_item.Entity
                           + ".json";

                    v_content = await client.GetAsync(v_call)
                                            .AsString();

                    JObject v_weather = JObject.Parse(v_content);
                    JToken v_localtemp = v_weather.SelectToken("$.current_observation['temperature_string']");
                    JToken v_local_time = v_weather.SelectToken("$.current_observation['local_time_rfc822']");
                    JToken v_cloud = v_weather.SelectToken("$.current_observation['weather']");

                    string v_message = $"Weather for " + v_item.Entity + ": \n" + v_cloud.ToString() + " Temp: " + v_localtemp.ToString()
                                     + " at " + v_local_time.ToString();
                    await context.PostAsync(v_message);

                }
            }

            if (v_found == 0)
            {
                string v_message = "Unable to determine the weather for your query.";
                await context.PostAsync(v_message);
            }
            context.Wait(this.MessageReceived);
        }

        static async Task<string> TranslateText(string inputText, string language, string accessToken)
        {
            string url = "http://api.microsofttranslator.com/v2/Http.svc/Translate";
            string query = $"?text={System.Net.WebUtility.UrlEncode(inputText)}&to={language}&contentType=text/plain";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(url + query);
                var result = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return "Hata: " + result;

                var translatedText = XElement.Parse(result).Value;
                return translatedText;
            }
        }

        static async Task<string> GetAuthenticationToken(string key)
        {
            string endpoint = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
                var response = await client.PostAsync(endpoint, null);
                var token = await response.Content.ReadAsStringAsync();
                return token;
            }
        }

        private async Task ResumeAfterQnA(IDialogContext context, IAwaitable<object> result)
        {
            context.Done<object>(null);
        }

        private async Task ResumeAfterJokeDialog(IDialogContext context, IAwaitable<object> result)
        {
            context.Done<object>(null);
        }

    }
}