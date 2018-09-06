using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using System.Collections.Generic;
using System;
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace Scorable
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        private string translator_ApiKey = "bdb8798b99f14ab38808f23e928402b2";
        private string translator_targetLang = "en";
        private string translator_accessToken; 

        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            this.translator_accessToken = await GetAuthenticationToken(translator_ApiKey);
            if (activity.Type == ActivityTypes.Message)
            {
                /*
                 * TODO:  Determine language used.
                 */
                ITextAnalyticsAPI client = new TextAnalyticsAPI();
                client.AzureRegion = AzureRegions.Westus;
                client.SubscriptionKey = "d220c5d87b5d486d94d1ef065bd2ba71";
                var connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                var v_fromlang = "en";

                try
                {
                    LanguageBatchResult v_result = client.DetectLanguage(
                                                     new BatchInput(
                                                         new List<Input>()
                                                         {
                                                         new Input("1", activity.Text)
                                                         }));
                    foreach (var v_document in v_result.Documents)
                    {

                        var v_replytext = v_document.DetectedLanguages[0].Name + " : " + v_document.DetectedLanguages[0].Iso6391Name;
                        var reply = activity.CreateReply(v_replytext);
                        v_fromlang = v_document.DetectedLanguages[0].Iso6391Name;
                        if (v_fromlang != "en")
                        {
                            var v_translatedtext = await TranslateText(activity.Text, 
                                                                       this.translator_targetLang, 
                                                                       this.translator_accessToken);
                            v_replytext = v_replytext + " : Translated to " + v_translatedtext;
                            reply = activity.CreateReply(v_replytext);
                            activity.Text = v_translatedtext;

                        }
                        await connector.Conversations.ReplyToActivityAsync(reply);
                    }
                } catch (Exception e)
                {;
                    var reply = activity.CreateReply(e.ToString());
                    await connector.Conversations.ReplyToActivityAsync(reply);
                }

                await Conversation.SendAsync(activity, () => new Dialogs.RootDialog());
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
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

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
}