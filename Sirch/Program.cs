using System;
using System.Collections.Generic;
using System.IO;
using ChatSharp;
using ChatSharp.Events;
using Nest;

namespace Sirch
{
    class Message
    {
        public String Channel { get; set; }
        public String From { get; set; }
        public String Host { get; set; }
        public String Time { get; set; }
        public String Text { get; set; }

        public override string ToString()
        {
            return String.Format("{0} {1} {2} {3} {4}", Channel, From, Host, Time, Text);
        }
    }
    class Program
    {
        private static IrcClient _client;
        private static ElasticClient _elastic;
        private static Dictionary<string, bool> _channelBuffering = new Dictionary<string, bool>(); 

        public static void RawMessage(object sender, RawMessageEventArgs eventArgs)
        {
            Console.WriteLine("Received: " + eventArgs.Message);
            var split = eventArgs.Message.Split(" ".ToCharArray());
            int code;
            if (Int32.TryParse(split[1], out code))
            {
                if (code == 464)
                {
                    _client.SendRawMessage("PASS user:pass");
                }
            }
        }

        public static void ChannelMessage(object sender, PrivateMessageEventArgs eventArgs)
        {
            var message = new Message
            {
                Channel = eventArgs.PrivateMessage.Source,
                From = eventArgs.PrivateMessage.User.Nick,
                Host = eventArgs.PrivateMessage.User.ToString(),
                Time = DateTime.UtcNow.ToString("o"),
                Text = eventArgs.PrivateMessage.Message
            };
            var text = eventArgs.PrivateMessage.Message;
            var channel = eventArgs.PrivateMessage.Source;
            if (text.Equals("Buffer Playback..."))
            {
                if (_channelBuffering.ContainsKey(channel))
                {
                    _channelBuffering[channel] = true;
                }
                else
                {
                    _channelBuffering.Add(channel, true);
                }
            } else if (text.Equals("Playback Complete."))
            {
                _channelBuffering.Remove(channel);
                return;
            }
            if(!_channelBuffering.ContainsKey(channel))
                _elastic.IndexAsync(message, i => i.Index("sirch_development"));

        }
        static void Main(string[] args)
        {
            try
            {
                _client = new IrcClient("gaspworks.net:6999", new IrcUser("Rappelle", "Rappelle"), true);
                _client.IgnoreInvalidSSL = true;

                _client.ConnectionComplete += (sender, eventArgs) => Console.WriteLine("Connected");
                _client.RawMessageRecieved += RawMessage;
                _client.RawMessageSent += (sender, eventArgs) => Console.WriteLine(eventArgs.Message);
                _client.NetworkError += (sender, eventArgs) => System.Environment.Exit(1);
                _client.ChannelMessageRecieved += ChannelMessage;

                var node = new Uri("http://localhost:9200");
                var settings = new ConnectionSettings(node);
                _elastic = new ElasticClient(settings);


                _client.ConnectAsync();
            }
            catch (IOException)
            {
                System.Environment.Exit(0);
            }
            Console.ReadLine();
        }

    }
}
