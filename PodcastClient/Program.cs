using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.ServiceModel.Syndication;
using System.Xml.Linq;

namespace PodcastClient
{
    class PodcastSubscriptions
    {
        public XDocument SubsDoc { get; set; }
        public List<PodcastSub> Subscriptions { get; set; }

        public PodcastSubscriptions(string subDocUrl)
        {
            if (File.Exists(subDocUrl))
            {
                SubsDoc = XDocument.Load(subDocUrl);
            }
            else
            {
                XDocument xdoc = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("root")
                );
                xdoc.Save(subDocUrl);
                SubsDoc = XDocument.Load(subDocUrl);
            }
        }

        public void AddSubscription(string url)
        {
            XDocument xdoc = XDocument.Load(url);
            string title = xdoc.Element("rss").Element("channel").Element("title").Value;
            string link = xdoc.Element("rss").Element("channel").Element("link").Value;
            PodcastSub newSub = new PodcastSub(url, title, link);
            Subscriptions.Add(newSub);
            SubsDoc.Element("root").Add(new XElement("sub", new XAttribute("title", title), new XAttribute("url", url)));
        }

        public void CheckSubscriptions()
        {
            
        }
    }

    class PodcastSub
    {
        internal string PodcastUrl { get; set; }
        internal string PodcastName { get; set; }
        internal string PodcastLink { get; set; }

        internal XDocument SubXDoc { get; set; }
        internal List<PodcastItem> ListenedList { get; set; }

        public PodcastSub(string url, string title, string link)
        {
            PodcastUrl = url;
            PodcastName = title;
            PodcastLink = link;
        }

        public void UpdateSub()
        {
            XDocument xdoc = XDocument.Load(PodcastUrl);
            string mostRecentPubDate = xdoc.Element("root").Element("channel").Element("item").Element("pubDate").Value;
            if (mostRecentPubDate != ListenedList[0].ItemPubDate)
            {
                var newItem2 = (
                    from item in xdoc.Descendants("item")
                    where DateTime.Compare(convertDateTime(item.Element("pubDate").Value), convertDateTime(ListenedList[0].ItemPubDate)) > 0
                    orderby (DateTime) convertDateTime(item.Element("pubDate").Value)
                     select item
                    );
                foreach (XElement pitem in newItem2)
                {
                    PodcastItem newItem = new PodcastItem(pitem);
                    ListenedList.Insert(0, newItem);
                }
            }

        }

        public void MarkItemListened(XElement item)
        {
            
        }

        internal DateTime convertDateTime(string pubDate)
        {
            string bit = pubDate.Split("+".ToCharArray())[0];
            string RFC822 = "ddd, dd MM yyyy HH:mm:ss";
            DateTime dt = DateTime.ParseExact(bit, RFC822, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None);
            return dt;
        }
    }

    class PodcastItem
    {
        public string ItemUrl { get; set; }
        public string ItemLink { get; set; }
        public string ItemTitle { get; set; }
        public string ItemPubDate { get; set; }
        public bool Listened { get; set; }

        public PodcastItem(XElement xItem)
        {
            ItemUrl = xItem.Element("enclosure").Attribute("url").Value;
            ItemLink = xItem.Element("link").Value;
            ItemTitle = xItem.Element("title").Value;
            ItemPubDate = xItem.Element("pubDate").Value;
        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            XmlDocument subsDoc = new XmlDocument();
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
