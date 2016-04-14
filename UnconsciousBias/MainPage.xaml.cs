using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace UnconsciousBias
{
    public sealed partial class MainPage : Page
    {
        const string serviceEndpoint = "https://graph.microsoft.com/v1.0/";
        static string tenant = App.Current.Resources["ida:Domain"].ToString();

        public MainPage()
        {
            this.InitializeComponent();
        }

        // Returns the first page of the signed-in user's messages.
        public static async Task<List<string>> GetMessagesAsync(string emailAddress)
        {
            var messages = new List<string>();
            JObject jResult = null;

            try
            {
                HttpClient client = new HttpClient();
                var token = await AuthenticationHelper.GetTokenHelperAsync();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

                // Endpoint for all messages in the current user's mailbox
                Uri messagesEndpoint = new Uri(serviceEndpoint + "me/messages?$search=\"to:" + emailAddress + "\"");
                HttpResponseMessage response = await client.GetAsync(messagesEndpoint);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    jResult = JObject.Parse(responseContent);

                    foreach (JObject user in jResult["value"])
                    {
                        string subject = (string)user["subject"];
                        string body = (string)user["body"]["content"];
                        messages.Add(body);
                        Debug.WriteLine("Got message: " + subject);
                    }
                }
                else
                {
                    Debug.WriteLine("We could not get messages. The request returned this status code: " + response.StatusCode);
                    return null;
                }
                return messages;
            }

            catch (Exception e)
            {
                Debug.WriteLine("We could not get messages: " + e.Message);
                return null;
            }
        }

        private async void btnGo_Click(object sender, RoutedEventArgs e)
        {
            var messages = await GetMessagesAsync(txtEmail.Text);
            var count = messages.Count;

            if (count > 0)
            {
                System.Text.StringBuilder s = new System.Text.StringBuilder();
                System.Text.StringBuilder s2 = new System.Text.StringBuilder();
                s2.Append("{\"documents\":[");
                int i = 1;
                Debug.WriteLine("JSON is:");
                foreach (var message in messages)
                {
                    string bodyWithoutHTMLtags = WebUtility.HtmlDecode(Regex.Replace(message, "<[^>]*(>|$)", string.Empty));
                    string step2 = Regex.Replace(bodyWithoutHTMLtags, @"[\s\r\n]+", " ");
                    // TODO: this is a dirty hack!  Find a better library to do this!  
                    s.AppendLine(step2);
                    //s.AppendLine(WebUtility.HtmlDecode(message));
                    //s.AppendLine(message);
                    //Debug.WriteLine(step2);
                    //Debug.WriteLine(message);
                    s2.Append("{\"id\":\"" + i + "\",\"text\":\"" + step2 + "\"},");
                    i++;
                }
                string tempString = s2.ToString();
                if (tempString.LastIndexOf(",") == tempString.Length - 1) s2.Remove(tempString.Length - 1, 1);
                s2.Append("]}");
                //lblDump.Text = s.ToString();
                Debug.WriteLine(s2.ToString());
                // TODO: bug when there are URLs and " in the email

                //var messages = GetMessagesAsync(txtEmail.Text);
                //double sentimentScore = 0.6434543; //TextAnalyticsHelper.
                string ssResponse = await TextAnalyticsHelper.GetSentiment(s2.ToString());
                // TODO: handle bad request errors; they won't be able to convert to doubles
                //if (ssResponse.IsSuccessStatusCode)
                // TODO: Jen start here
                var jResult = JObject.Parse(ssResponse);
                double scoreSum = 0.0;
                int scoreCount = 0;
                foreach (JObject doc in jResult["documents"])
                {
                    string score = (string)doc["score"];
                    double dblScore = Convert.ToDouble(score);
                    scoreSum += dblScore;
                    scoreCount++;
                }

                double dblsentimentScore = scoreSum / scoreCount;
                int sentimentPercentage = Convert.ToInt32(dblsentimentScore * 100);
                lblDump.Text = "Your last " + count.ToString() + " emails to " + txtEmail.Text + " were " + sentimentPercentage + "% positive.";
                //lblDump.Text = messages.ToString();
            }
            else
            {
                lblDump.Text = "No messages were found in your email to " + txtEmail.Text + ".";
            }
        }
    }
}
