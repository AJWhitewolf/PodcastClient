using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;
using WMPLib;

namespace PodcastClient
{
    public class PodcastSubscriptions
    {
        public XDocument SubsDoc { get; set; }
        public static List<PodcastSub> Subscriptions { get; set; }

        public PodcastSubscriptions(string subDocUrl)
        {
            Subscriptions = new List<PodcastSub>();
            if (File.Exists(subDocUrl))
            {
                SubsDoc = XDocument.Load(subDocUrl);
                //Console.WriteLine("Number of Subscriptions: {0}", SubsDoc.Element("root").Elements("sub").Count());
                foreach (XElement sub in SubsDoc.Element("root").Elements("sub"))
                {
                    //Console.WriteLine("Adding Subscription {0}", sub.Attribute("title").Value);
                    PodcastSub newSub = new PodcastSub(sub.Attribute("url").Value, sub.Attribute("title").Value);
                    //Subscriptions.Add(newSub);
                }
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

        public void AddNewSubscription(string url)
        {
            XDocument xdoc = XDocument.Load(url);
            string title = xdoc.Element("rss").Element("channel").Element("title").Value;
            //string link = xdoc.Element("rss").Element("channel").Element("link").Value;
            PodcastSub newSub = new PodcastSub(url, title);
            //Subscriptions.Add(newSub);
            SubsDoc.Element("root").Add(new XElement("sub", new XAttribute("title", title), new XAttribute("url", url)));
            SubsDoc.Save("subs.xml");
        }

        public void CheckSubscriptions()
        {
            
        }

        public void CountSubscriptions()
        {
            Console.WriteLine("Displaying Item Counts");
            foreach (PodcastSub sub in Subscriptions)
            {
                Console.WriteLine("Title: {0}, Count: {1}", sub.PodcastName, sub.GetCount());
            }
        }

        public List<PodcastSub> GetSubscriptions()
        {
            return Subscriptions;
        }

        public XDocument GetSubsDoc()
        {
            return SubsDoc;
        }
    }

    public class PodcastSub
    {
        internal string PodcastUrl { get; set; }
        internal string PodcastName { get; set; }
        internal string PodcastLink { get; set; }

        internal XDocument SubXDoc { get; set; }
        internal List<PodcastItem> ListenedList { get; set; }

        public PodcastSub(string url, string title)
        {
            PodcastUrl = url;
            PodcastName = title;
            ListenedList = new List<PodcastItem>();
            PodcastSubscriptions.Subscriptions.Add(this);
        }

        public void LoadSubXML()
        {
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.17 (KHTML, like Gecko) Chrome/24.0.1312.57 Safari/537.17";
            var stream = client.OpenRead(PodcastUrl);
            XmlReader reader = XmlReader.Create(stream);
            SubXDoc = XDocument.Load(reader);
            reader.Close();
        }

        public int GetCount()
        {
            if (SubXDoc == null)
            {
                LoadSubXML();
            }
            return SubXDoc.Element("rss").Element("channel").Elements("item").Count();
            //Console.WriteLine("Title: {0}, Count: {1}", PodcastName, SubXDoc.Element("rss").Element("channel").Elements("item").Count());
        }

        public void UpdateSub()
        {
            if (SubXDoc == null)
            {
                LoadSubXML();
            }
            string mostRecentPubDate = SubXDoc.Element("root").Element("channel").Element("item").Element("pubDate").Value;
            if (ListenedList.Count > 0)
            {
                if (mostRecentPubDate != ListenedList[0].ItemPubDate)
                {
                    var newItem2 = (
                        from item in SubXDoc.Descendants("item")
                        where DateTime.Compare(convertDateTime(item.Element("pubDate").Value), convertDateTime(ListenedList[0].ItemPubDate)) > 0
                        orderby (DateTime)convertDateTime(item.Element("pubDate").Value)
                        select item
                        );
                    foreach (XElement pitem in newItem2)
                    {
                        PodcastItem newItem = new PodcastItem(pitem);
                        ListenedList.Insert(0, newItem);
                    }
                }
            }
        }

        public void ReadAllItems()
        {
            if (SubXDoc == null)
            {
                LoadSubXML();
            }
            ListenedList.Clear();
            var items = from item in SubXDoc.Descendants("item")
                select item;
            foreach (XElement item in items)
            {
                PodcastItem newItem = new PodcastItem(item);
                XDocument xdoc = Program.GetSubsDocument();
                var listened = from ii in xdoc.Descendants("item")
                               where ii.Parent.Attribute("title").Value == PodcastName &&
                               ii.Attribute("title").Value == newItem.ItemTitle &&
                               ii.Attribute("url").Value == newItem.ItemUrl
                               select ii;
                if (listened.Any())
                {
                    newItem.Listened = true;
                }
                ListenedList.Add(newItem);
            }
        }

        internal DateTime convertDateTime(string pubDate)
        {
            string bit = pubDate.Split("+".ToCharArray())[0];
            string RFC822 = "ddd, dd MM yyyy HH:mm:ss";
            DateTime dt = DateTime.ParseExact(bit, RFC822, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None);
            return dt;
        }

        public void MarkItemListened(PodcastItem pItem)
        {
            var xdoc = Program.GetSubsDocument();
            var pNode = (from sub in xdoc.Descendants("sub")
                where sub.Attribute("title").Value == PodcastName
                select sub).FirstOrDefault();
            var check = (from item in pNode.Descendants("item")
                where item.Attribute("title").Value == pItem.ItemTitle
                select item).Count();
            if (check < 1)
            {
                XElement node = new XElement("item", new XAttribute("title", pItem.ItemTitle), new XAttribute("url", pItem.ItemUrl), new XAttribute("pubDate", pItem.ItemPubDate));
                pNode.Add(node);
            }
            pItem.Listened = true;
            xdoc.Save("subs.xml");
        }
    }

    public class PodcastItem
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
        static List<PodcastSub> subList { get; set; }
        static ConsoleKeyInfo cki { get; set; }
        static PodcastSub SelectedSub { get; set; }
        static PodcastItem SelectedItem { get; set; }
        static XDocument SubsDocument { get; set; }
        static string DownloadDirectory { get; set; }
        static WindowsMediaPlayer wplayer = new WindowsMediaPlayer();

        public static XDocument GetSubsDocument()
        {
            return SubsDocument;
        }

        static void Start()
        {
            Console.WriteLine("Subscriptions:");
            foreach (PodcastSub sub in subList)
            {
                Console.WriteLine("[{0}] {1}", subList.IndexOf(sub), sub.PodcastName);
            }
            Console.Write("Enter number to manage Subscription: ");
            int which = int.Parse(Console.ReadLine());
            PickedSub(which);
        }

        static void PickedSub(int ind)
        {
            SelectedSub = subList[ind];
            Console.Clear();
            Console.WriteLine("=========================================================");
            Console.WriteLine(SelectedSub.PodcastName);
            Console.WriteLine("=========================================================");
            Console.WriteLine();
            Console.WriteLine("Reading data...");
            Console.WriteLine();
            SelectedSub.ReadAllItems();
            foreach (PodcastItem item in SelectedSub.ListenedList)
            {
                if (SelectedSub.ListenedList.IndexOf(item) % 20 == 0 && SelectedSub.ListenedList.IndexOf(item) != 0)
                {
                    Console.WriteLine("Displaying Entries {0} to {1}, press Escape to stop, or any other key to continue.", SelectedSub.ListenedList.IndexOf(item) - 20, SelectedSub.ListenedList.IndexOf(item) - 1);
                    cki = Console.ReadKey();
                    if (cki.Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }
                Console.WriteLine("=========================================================");
                Console.WriteLine("[{0}] {1}", SelectedSub.ListenedList.IndexOf(item), item.ItemTitle);
                Console.WriteLine("Published: {0}", item.ItemPubDate);
                Console.WriteLine("=========================================================");
                Console.WriteLine();
            }
            Console.Write("Enter an Episode number to manage: ");
            int which2 = int.Parse(Console.ReadLine());
            PickedItem(which2);
        }

        static void PickedItem(int ind)
        {
            SelectedItem = SelectedSub.ListenedList[ind];
            Console.Clear();
            Console.WriteLine("=========================================================");
            Console.WriteLine(SelectedItem.ItemTitle);
            Console.WriteLine("Published: {0}", SelectedItem.ItemPubDate);
            Console.WriteLine("Page link: {0}", SelectedItem.ItemLink);
            Console.WriteLine("Download: {0}", SelectedItem.ItemUrl);
            Console.WriteLine("Listened: {0}", SelectedItem.Listened.ToString());
            Console.WriteLine("=========================================================");
            Console.Write("Mark item [L]istened, [D]ownload, [S]tream: ");
            string comm = Console.ReadLine();
            switch (comm)
            {
                case "l":
                case "L":
                    SelectedSub.MarkItemListened(SelectedItem);
                    Console.WriteLine("Marked item as Listened.");
                    var check = from item in SelectedSub.ListenedList
                                where item.Listened == true
                                select item;
                    foreach (PodcastItem cc in check)
                    {
                        Console.WriteLine("Listened: {0}", cc.ItemTitle);
                    }
                    break;

                case "d":
                case "D":
                    Uri uri = new Uri(SelectedItem.ItemUrl);
                    if (!Directory.Exists(DownloadDirectory + SelectedSub.PodcastName))
                    {
                        Directory.CreateDirectory(DownloadDirectory + SelectedSub.PodcastName);
                    }
                    DownloadPodcast(SelectedItem.ItemUrl, DownloadDirectory + SelectedSub.PodcastName + "\\" + Path.GetFileName(uri.LocalPath));
                    break;
                case "s":
                case "S":
                    wplayer.URL = SelectedItem.ItemUrl;
                    wplayer.controls.play();
                    Console.WriteLine("Playing...");
                    //System.Diagnostics.Process.Start(SelectedItem.ItemUrl);
                    break;
            }
            Console.ReadLine();
            Start();
        }

        static string GetAssociatedApp()
        {
            const string extPathTemplate = @"HKEY_CLASSES_ROOT\{0}";
            const string cmdPathTemplate = @"HKEY_CLASSES_ROOT\{0}\shell\open\command";

            // 1. Find out document type name for .jpeg files
            const string ext = ".mp3";

            var extPath = string.Format(extPathTemplate, ext);

            var docName = Registry.GetValue(extPath, string.Empty, string.Empty) as string;
            if (!string.IsNullOrEmpty(docName))
            {
                // 2. Find out which command is associated with our extension
                var associatedCmdPath = string.Format(cmdPathTemplate, docName);
                var associatedCmd =
                    Registry.GetValue(associatedCmdPath, string.Empty, string.Empty) as string;

                if (!string.IsNullOrEmpty(associatedCmd))
                {
                    return associatedCmd;
                }
                else
                {
                    return "Failed.";
                }
            }
            else
            {
                return "Failed.";
            }
        }

        static void Main(string[] args)
        {
            PodcastSubscriptions subs = new PodcastSubscriptions("subs.xml");
            subList = subs.GetSubscriptions();
            SubsDocument = subs.GetSubsDoc();
            DownloadDirectory = @"C:\Users\ADunigan\Downloads\Podcasts\";
            //Console.WriteLine(GetAssociatedApp());
            Start();
        }

        static void DownloadPodcast(string url, string location)
        {
            Console.WriteLine("Downloading...");
            using (WebClient wc = new WebClient())
            {
                wc.DownloadFileCompleted += wc_DownloadCompleted;
                wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                wc.DownloadFileAsync(new Uri(url), location);
            }
        }

        private static void wc_DownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Console.WriteLine("Download complete.");
            Console.WriteLine("Press Enter to return to Subscriptions page.");
        }

        static void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Write("\r{0}Downloaded {1} of {2} bytes. {3}% complete...", (string)e.UserState, e.BytesReceived, e.TotalBytesToReceive, e.ProgressPercentage);
        }
    }
}
