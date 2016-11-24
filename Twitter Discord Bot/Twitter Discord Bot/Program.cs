using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Credentials;
using Tweetinvi.Core;
using Tweetinvi.Streams;
using Discord;
using Discord.Commands;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using System.Xml;

namespace Twitter_Discord_Bot
{
    class Program
    {
        static void Main(string[] args) => new Program().Start();

        private DiscordClient _client;

        public void Start()
        {
            _client = new DiscordClient(x =>
            {
                x.AppName = "Twitter Bot";
                x.LogLevel = LogSeverity.Info;
                x.LogHandler = Log;
            });

            _client.UsingCommands(x =>
            {
                x.PrefixChar = '!';
                x.AllowMentionPrefix = true;
                x.HelpMode = HelpMode.Public;
            });

            Navigate();

            CreateCommands();

            _client.ExecuteAndWait(async () =>
            {
                string XML = Path.GetFullPath("Tokens.xml");
                XmlDocument doc = new XmlDocument();
                doc.Load(XML);
                string DiscordToken = doc.ChildNodes.Item(1).InnerText.ToString();

                try
                {
                    await _client.Connect(DiscordToken, TokenType.Bot);
                }
                catch (Exception)
                {
                    Console.WriteLine("Something went wrong most likely the token you are using is invalid. Silly Human");
                }
            });
        }

    


        public void Log(object sender, LogMessageEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.WriteLine($"[{e.Severity}] [{e.Source}] [{e.Message}] [{e.Exception}]");
            }
            else
            {
                Console.WriteLine($"[{e.Severity}] [{e.Source}] [{e.Message}]");
            }
        }

        [STAThread]
        public void CreateCommands()
        {
            var CService = _client.GetService<CommandService>();

            CService.CreateCommand("Track")
                .Parameter("User", ParameterType.Unparsed)
                .Description("Tracks a Twitter User")
                .Do(async (e) =>
                {
                    await e.Channel.SendMessage("Tracking " + e.GetArg("User"));

                    long User = Convert.ToInt64(e.GetArg("User"));
                    var Stream = Tweetinvi.Stream.CreateFilteredStream();
                    Stream.AddFollow(User);
                    Stream.MatchingTweetReceived += (sender, args) =>
                    {
                        Console.WriteLine("Found Tweet");
                        e.Channel.SendMessage(e.GetArg("User") + " " + args.Tweet);
                    };
                    await Stream.StartStreamMatchingAllConditionsAsync();

                });

            CService.CreateCommand("Sample")
                .Parameter("User", ParameterType.Unparsed)
                .Description("Tracks a Twitter User")
                .Do( (e) =>
                {

                    SampleStream(e);
                    
                });
        }

        public void SampleStream(CommandEventArgs e)
        {

            
            //var th = new Thread(() =>
            //{

                e.Channel.SendMessage("Starting Sample Stream");
                Console.WriteLine("Starting Sample Stream");

                var stream = Tweetinvi.Stream.CreateSampleStream();
                Console.WriteLine(stream.StreamState.ToString());
                stream.TweetReceived += (sender, args) =>
                {

                    Console.WriteLine(args.Tweet);

                };
                stream.StartStreamAsync();
                Console.WriteLine(stream.StreamState.ToString());

                /*
                var stream = Tweetinvi.Stream.CreateSampleStream();

                stream.TweetReceived += (sender, args) =>
            {
                Console.WriteLine("Found Tweet");
                Console.WriteLine(args.Tweet);
                Console.WriteLine(sender.ToString());
                e.Channel.SendMessage(args.Tweet.ToString());
            };
                stream.StartStream();
                */

            //});
            //th.Start();
        }


        public string Authorization()
        {
            ITwitterCredentials AppCredentials = new TwitterCredentials("YY9hXL7YVeKoXhJxky09xAmXL", "C7r3eiTOYU016F7e2w2c0M8KsL0t3Y7nBZ61IB0kCvgGKIPpzD", "2558354432-gNeNcxDRR67W3t7ZNdx131PqcFyEPKPt5tBUdm9", "3bnuojT7cTVx295nXhK5ELnPbE4CRVSNMDwg7uhdmpAQO");

            var authenticationContext = AuthFlow.InitAuthentication(AppCredentials);

            string AuthUrl = authenticationContext.AuthorizationURL;

            AuthenticationContext = authenticationContext;

            return AuthUrl;
        }

        IAuthenticationContext AuthenticationContext = null;
        private void Authorize(string AuthCode)
        {
            var UserCredentials = AuthFlow.CreateCredentialsFromVerifierCode(AuthCode, AuthenticationContext);
            Auth.SetCredentials(UserCredentials);
            var AuthenticatedUser = Tweetinvi.User.GetAuthenticatedUser();
            Console.WriteLine("Connected to Twitter");
        }

        public void Navigate()
        {
            var th = new Thread(() =>
            {
                WebBrowser webBrowser1 = new WebBrowser();

                if (IsFirstDocument == true)
                {
                    webBrowser1.DocumentCompleted += DocumentCompleted; //new WebBrowserDocumentCompletedEventHandler(DocumentCompleted);

                    webBrowser1.Navigate(Authorization());
                }

                Application.Run();
            });
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
        }

        //string Username = Console.ReadLine();
        //string Password = Console.ReadLine();
        bool IsFirstDocument = true;
        private void DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            var webBrowser1 = sender as WebBrowser;

            Console.WriteLine("Please enter your Twitter Username");
            var username = webBrowser1.Document.GetElementById("username_or_email");
            username?.SetAttribute("value", Console.ReadLine());

            Console.WriteLine("Please enter your Twitter Password");
            var password = webBrowser1.Document.GetElementById(@"session[password]");
            password?.SetAttribute("value", Console.ReadLine());
            Console.Clear();

            var validate = webBrowser1.Document.GetElementById("allow");
            validate?.InvokeMember("click");


            string URL = webBrowser1.Document.Url.ToString();
            Convert.ToString(URL);
            if (IsFirstDocument == true)
            {
                if (URL == "https://api.twitter.com/oauth/authorize")
                {
                    try
                    {
                        string AuthPin = webBrowser1.Document.GetElementById("oauth_pin").InnerText;
                        if (AuthPin != null)
                        {
                            bool isNumber = Regex.IsMatch(AuthPin, @"-?\d+(\.\d+)?");

                            if (isNumber == true)
                            {
                                IsFirstDocument = false;
                                Authorize(AuthPin);

                            }
                        }


                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
        }
    }
}

