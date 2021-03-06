﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;

namespace GumtreeScraper
{
    internal sealed class Proxy
    {
        private static volatile Proxy _instance;
        private static readonly object Sync = new Object();

        private readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Stack<string> _proxyList = new Stack<string>();
        private readonly bool _useProxy = bool.Parse(ConfigurationManager.AppSettings["UseProxy"]);

        private string _ip;
        private int? _port;

        public static Proxy Instance
        {
            get
            {
                // Implement Singleton.
                if (_instance != null) return _instance;
                lock (Sync) _instance = new Proxy();
                return _instance;
            }
        }

        private Proxy()
        {
            if (!_useProxy) return;

            // Get IP for proxy to use for calls.
            _log.Info("Retrieving proxy list from web..");

            // Get proxy IP from server.
            string proxyUrl = ConfigurationManager.AppSettings["ProxyUrl"];
            int timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(proxyUrl);
            request.UserAgent = ConfigurationManager.AppSettings["UserAgent"];
            request.Timeout = timeout;

            // Web request response will be read into this variable.
            string data;

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            data = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Fatal("Could not get proxy details.", ex);
                throw;
            }

            IList<string> proxies = new List<string>();

            MatchCollection regMatch = Regex.Matches(data, @"([0-9]+(?:\.[0-9]+){3}:[0-9]+)");
            if (regMatch.Count > 0)
            {
                IList<string> matches = regMatch.OfType<Match>().Select(x => x.Value).ToList();
                foreach (string match in matches) proxies.Add(match);
            }
            else throw new Exception("Proxy list is empty.");

            // Shuffle the proxy list.
            _proxyList = new Stack<string>(Shuffle(proxies));
        }

        public string MakeRequest(string url)
        {
            string data = null;

            if (_useProxy)
            {
                if (_proxyList.Count < 1) throw new Exception("Proxy list has been exhuasted with no successes.");

                // Keep trying different proxies till success.
                while (String.IsNullOrEmpty(data) && _proxyList.Count > 0)
                {
                    try
                    {
                        data = DownloadString(url);
                    }
                    catch (Exception)
                    {
                        _ip = null;
                        _port = null;
                    }
                }
            }
            else
            {
                data = DownloadString(url);
                if (String.IsNullOrEmpty(data)) throw new Exception("Request returned a null response, IP is possibly blocked.");
            }
            return data;
        }

        public void NextProxy()
        {
            string ipAndPort = _proxyList.Pop();
            _ip = ipAndPort.Split(':')[0];
            _port = int.Parse(ipAndPort.Split(':')[1]);
        }

        private string DownloadString(string url)
        {
            string data;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = ConfigurationManager.AppSettings["UserAgent"];
            request.Timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.ReadWriteTimeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.Method = "GET";

            if (_useProxy)
            {
                // If IP and port are not populated, then use the next in the list.
                if (String.IsNullOrEmpty(_ip) && _port == null) NextProxy();

                _log.Info($"Attempting with {_ip}:{_port}");
                Uri uri = new Uri($"http://{_ip}:{_port}");
                WebProxy proxy = new WebProxy(uri, false);
                request.Proxy = proxy;
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException("Stream returns null.")))
                    {
                        data = reader.ReadToEnd();
                    }
                }
            }
            return data;
        }

        private static IList<string> Shuffle(IList<string> list)
        {
            int n = list.Count;
            Random rnd = new Random();
            while (n > 1)
            {
                int k = rnd.Next(0, n) % n;
                n--;
                string value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            return list;
        }
    }
}
