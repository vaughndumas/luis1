using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Luis.Models;
using Pathoschild.Http.Client;
using Newtonsoft.Json.Linq;

namespace Scorable.Dialogs
{
    [Serializable]
    public class WeatherDialog: IDialog<object>
    {

        public async Task StartAsync(IDialogContext context)
        {
            IList<EntityRecommendation> v_entities = x_results.Entities;
            string v_call;
            IClient client;
            string v_content;
            var v_weatherdef = new { Name = "" };
            int v_found = 0;

            foreach (EntityRecommendation v_item in v_entities)
            {
                //await context.PostAsync(v_item.Type + " : " + v_item.Entity);
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

            context.Wait(this.MessageReceivedAsync);
            context.Done<object>(null);

        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;
        }
    }
}