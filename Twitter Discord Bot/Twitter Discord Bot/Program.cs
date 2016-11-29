using System;
using System.Threading;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Streaming;
using Discord;
using Discord.Commands;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;

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

            Navigate();

            _client.UsingCommands(x =>
            {
                x.PrefixChar = '!';
                x.AllowMentionPrefix = true;
                x.HelpMode = HelpMode.Public;
            });

            _client.ExecuteAndWait(async () =>
            {
                try
                {
                    string XML = Path.GetFullPath("Config.xml");
                    XmlDocument doc = new XmlDocument();
                    doc.Load(XML);
                    string DiscordToken = doc.ChildNodes.Item(1).ChildNodes.Item(1).InnerText.ToString();

                    await _client.Connect(DiscordToken, TokenType.Bot);
                } catch (FileNotFoundException)
                {
                    if (GenXml == false)
                    {
                        GenerateXML();
                    }
                }
                
            });
        }

        bool GenXml = false;

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

            IFilteredStream FilteredStream = Tweetinvi.Stream.CreateFilteredStream();
            ISampleStream SampleStream = Tweetinvi.Stream.CreateSampleStream();

            CService.CreateCommand("StreamState")
                .Description("Gets the states of the streams")
                .Do(async (e) =>
                {
                    await e.Channel.SendMessage("```Sample Stream: " + SampleStream.StreamState + Environment.NewLine + "Filtered Stream: " + FilteredStream.StreamState + "```");
                });

            CService.CreateCommand("PurgeStreams")
                .Alias("Kill", "Stop", "PS")
                .Description("Hopefully stops the stream")
                .Do(async (e) =>
               {
                   FilteredStream.StopStream();
                   SampleStream.StopStream();
                   FilteredStream.ClearTracks();
                   await e.Channel.SendMessage("```Sample Stream: " + SampleStream.StreamState + Environment.NewLine + "Filtered Stream: " + FilteredStream.StreamState + "```");
               });

            CService.CreateCommand("StopSampleStream")
                .Description("Stops all sample streams")
                .Do(async (e) =>
               {
                   SampleStream.StopStream();
                   await e.Channel.SendMessage("```Sample Stream: " + SampleStream.StreamState + Environment.NewLine + "Filtered Stream: " + FilteredStream.StreamState + "```");
               });

            CService.CreateCommand("StopFilteredStream")
                .Description("Stops all Filtered streams")
                .Do(async (e) =>
                {
                    FilteredStream.StopStream();
                    await e.Channel.SendMessage("```Sample Stream: " + SampleStream.StreamState + Environment.NewLine + "Filtered Stream: " + FilteredStream.StreamState + "```");
                });

            CService.CreateCommand("Track")
                .Description("Tracks a twitter User Either from their ID or Handle")
                .Parameter("User", ParameterType.Unparsed)
                .Description("Tracks a Twitter User")
                .Do(async (e) =>
                {
                    if (Regex.IsMatch(e.GetArg("User"), @"-?\d+(\.\d+)?"))
                    {
                        long User = Convert.ToInt64(e.GetArg("User"));
                        IUser Target = Tweetinvi.User.GetUserFromId(Convert.ToInt64(e.GetArg("User")));

                        await e.Channel.SendMessage("Tracking " + Tweetinvi.User.GetUserFromId(Convert.ToInt64(e.GetArg("User"))));

                        FilteredStream.AddFollow(User);
                        FilteredStream.MatchingTweetReceived += (sender, args) =>
                        {
                            e.Channel.SendMessage(Target + " " + args.Tweet);
                        };
                        FilteredStream.StreamStopped += (sender, args) =>
                        {
                            var Exception = args.Exception;
                            var DisconnectMessage = args.DisconnectMessage;
                            e.Channel.SendMessage("Filtered stream ended exception: " + Exception + " Disconnect message: " + DisconnectMessage);
                            
                        };
                        await FilteredStream.StartStreamMatchingAllConditionsAsync();
                    }
                    else
                    {
                        var Target = Tweetinvi.User.GetUserFromScreenName(e.GetArg("User"));

                        await e.Channel.SendMessage("Tracking " + Tweetinvi.User.GetUserFromScreenName(e.GetArg("User")));

                        FilteredStream.AddFollow(Target);
                        FilteredStream.MatchingTweetReceived += (sender, args) =>
                        {
                            Console.WriteLine("Found Tweet");
                            e.Channel.SendMessage(e.GetArg("User") + " Tweeted " + args.Tweet);
                        };
                        await FilteredStream.StartStreamMatchingAllConditionsAsync();
                    }
                });

            CService.CreateCommand("Spam")
                .Alias("Havoc", "Anton", "Cancer")
                .Description("Initializes a socalled sample stream which returns 1% of all public tweets Discord will not show them all because of RateLimits.")
                .Do(async (e) =>
                {
                    if (e.User.ServerPermissions.ManageServer == true)
                    {
                        SampleStream.TweetReceived += (sender, args) =>
                        {

                            e.Channel.SendMessage(args.Tweet.Text.ToString());

                        };
                        await SampleStream.StartStreamAsync();
                    }
                    else
                    {
                        await e.Channel.SendMessage("ERROR most likely you do not have sufficient permissions");
                    }
                });

            CService.CreateCommand("Sample")
                .Description("Initializes a socalled sample stream but instead of 1% its specified with an amount of random tweets, Neat!")
                .Parameter("Tweets", ParameterType.Unparsed)
                .Do(async (e) =>
                {
                    if (Regex.IsMatch(e.GetArg("Tweets"), @"-?\d+(\.\d+)?"))
                    {
                        int Tweets = Convert.ToInt32(e.GetArg("Tweets"));
                        await e.Channel.SendMessage("Transmitting " + e.GetArg("Tweets") + " Tweets");

                        int i = 0;
                        SampleStream.TweetReceived += (sender, args) =>
                        {
                            i++;
                            if (i < Tweets)
                            {
                                e.Channel.SendMessage(args.Tweet.Text.ToString());
                            }
                            else
                            {
                                SampleStream.StopStream();
                            }
                        };
                        FilteredStream.StreamStopped += (sender, args) =>
                        {
                            var Exception = args.Exception;
                            var DisconnectMessage = args.DisconnectMessage;
                            e.Channel.SendMessage("Filtered stream ended exception: " + Exception + " Disconnect message: " + DisconnectMessage);

                        };
                        await SampleStream.StartStreamAsync();
                    }
                    else
                    {
                        await e.Channel.SendMessage("Error Invalid input");
                    }
                });
        }

        public string Authorization()
        {
            string ConsumerKey = null;
            string ConsumerSecret = null;
            string AccessToken = null;
            string AccessSecret = null;
            try
            {
                string XML = Path.GetFullPath("Config.xml");
                XmlDocument doc = new XmlDocument();
                doc.Load(XML);
                ConsumerKey = doc.ChildNodes.Item(1).ChildNodes.Item(9).InnerText.ToString();
                ConsumerSecret = doc.ChildNodes.Item(1).ChildNodes.Item(11).InnerText.ToString();
                AccessToken = doc.ChildNodes.Item(1).ChildNodes.Item(13).InnerText.ToString();
                AccessSecret = doc.ChildNodes.Item(1).ChildNodes.Item(15).InnerText.ToString();
            }
            catch (FileNotFoundException)
            {
                if (GenXml == false)
                {
                    GenerateXML();
                }
            }

            ITwitterCredentials AppCredentials = new TwitterCredentials(ConsumerKey, ConsumerSecret, AccessToken, AccessSecret);

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
                webBrowser1.ScriptErrorsSuppressed = true;

                if (IsFirstDocument == true)
                {
                    try
                    {
                        string XML = Path.GetFullPath("Config.xml");
                        XmlDocument doc = new XmlDocument();
                        doc.Load(XML);
                        string YesNo = doc.ChildNodes.Item(1).ChildNodes.Item(3).InnerText.ToString();

                        if (YesNo == "True" | YesNo == "true")
                        {
                            webBrowser1.DocumentCompleted += ManualDocumentCompleted; //new WebBrowserDocumentCompletedEventHandler(DocumentCompleted);

                            webBrowser1.Navigate(Authorization());
                        }
                        else if (YesNo == "False" | YesNo == "false")
                        {
                            webBrowser1.DocumentCompleted += AutoDocumentCompleted;

                            webBrowser1.Navigate(Authorization());
                        }
                        else
                        {
                            Console.WriteLine("Something went wrong please make sure that you have specified which method of authentication you wish to use in the config");
                        }
                    } catch (FileNotFoundException)
                    {
                        if (GenXml == false)
                        {
                            GenerateXML();
                        }
                    }

                    

                }

                Application.Run();
            });
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
        }

        public void GenerateXML()
        {
            GenXml = true;
            Console.WriteLine("Config.xml file not found do you wish to generate a new template");
            Console.WriteLine("Y/N");
            string key = Console.ReadLine();

            // acknowlegdement with generating a new config.xml
            if (key == "y" | key == "Y")
            {

                // If no Config file exists generate a new config.xml
                if (!File.Exists(Path.GetFullPath("Config.xml")))
                {
                    XmlWriterSettings Settings = new XmlWriterSettings();
                    //Settings.Encoding = System.Text.Encoding.UTF8;
                    Settings.Indent = true;

                    // Generates a new Config file.
                    using (XmlWriter Writer = XmlWriter.Create(Path.GetFullPath("Config.xml"), Settings))
                    {

                        Writer.WriteStartDocument();
                        Writer.WriteStartElement("Config");

                        Writer.WriteComment(" Enter DiscordToken Here you can create a Bot here https://discordapp.com/developers/applications/me");
                        Writer.WriteElementString("DiscordToken", "InsertDiscordTokenHere");

                        Writer.WriteComment(" True will enable Manual Login in the CommandLine, False will enable Username and Password fields for automatic login");
                        Writer.WriteElementString("ManualAuth", "True/False");

                        Writer.WriteComment(" Enter Twitter Username Here");
                        Writer.WriteElementString("Username", "Twitter Username or email here");

                        Writer.WriteComment(" Enter Twitter Password Here");
                        Writer.WriteElementString("Password", "Twitter Password Here");

                        Writer.WriteComment(" Enter Twitter Consumerkey here you can get a Twitter App here https://apps.twitter.com/");
                        Writer.WriteElementString("Consumerkey", "Consumer key here");

                        Writer.WriteComment(" Enter Twitter ConsumerSecret here");
                        Writer.WriteElementString("ConsumerSecret", "Consumer Secret here");

                        Writer.WriteComment(" Enter Twitter AccessToken Here");
                        Writer.WriteElementString("AccessToken", "Access Token Here");

                        Writer.WriteComment(" Enter Twitter AccessSecret here \t");
                        Writer.WriteElementString("AccessSecret", "Access Secret here");

                        Writer.WriteEndElement();
                        Writer.WriteEndDocument();
                    }

                    Console.WriteLine("Config File Generated Please Restart Application");
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
                    Environment.Exit(0);

                }
                else
                {
                    Console.WriteLine("ERROR Unknown Error has been detected please restart" + Environment.NewLine + "Press any key to continue");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
            }
            else if (key == "n" | key == "N")
            {
                Console.WriteLine("Program has met a fatal error and needs to restart please generate your own Config.xml");
            }
        }

        bool IsFirstDocument = true;
        private void ManualDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
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
                                CreateCommands();
                                webBrowser1.Dispose();

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

        private void AutoDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            try
            {
                string XML = Path.GetFullPath("Config.xml");
                XmlDocument doc = new XmlDocument();
                doc.Load(XML);
                string Username = doc.ChildNodes.Item(1).ChildNodes.Item(5).InnerText.ToString();
                string Password = doc.ChildNodes.Item(1).ChildNodes.Item(7).InnerText.ToString();

                var webBrowser1 = sender as WebBrowser;

                var username = webBrowser1.Document.GetElementById("username_or_email");
                username?.SetAttribute("value", Username);

                var password = webBrowser1.Document.GetElementById(@"session[password]");
                password?.SetAttribute("value", Password);

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
                                    CreateCommands();
                                    webBrowser1.Dispose();

                                }
                            }


                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                        }
                    }
                }
            } catch (FileNotFoundException)
            {
                if (GenXml == false)
                {
                    GenerateXML();
                }
            }
        }
    }
}