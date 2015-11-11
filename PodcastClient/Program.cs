using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.ServiceModel.Syndication;

namespace PodcastClient
{
    class Program
    {
        static void Main(string[] args)
        {
            XmlDocument subsDoc = new XmlDocument();
            //string xText = File.ReadAllText(args[0]);
            //subsDoc.LoadXml(xText);
            subsDoc.Load(args[0]);
            XmlNodeList subs = subsDoc.SelectNodes("//sub");

            Dictionary<XmlNode, SyndicationFeed> subDictionary = new Dictionary<XmlNode, SyndicationFeed>();
            
            foreach (XmlNode subNode in subs)
            {
                string subUrl = subNode.Attributes["url"].Value;
                var client = new WebClient();
                client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.17 (KHTML, like Gecko) Chrome/24.0.1312.57 Safari/537.17";
                var stream = client.OpenRead(subUrl);
                XmlReader reader = XmlReader.Create(stream);
                SyndicationFeed feed = SyndicationFeed.Load(reader);
                reader.Close();
                subDictionary.Add(subNode, feed);
                Console.WriteLine(subNode.Attributes["title"].Value + " ("+feed.Items.Count()+")");
            }
            
        }
    }
}
