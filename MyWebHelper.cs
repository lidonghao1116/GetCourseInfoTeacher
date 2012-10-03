using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

namespace GetCourseInfoV2
{
    public class HTTPHelper
    {
        public CookieContainer m_cookieContainer;
        int m_timeout;
        string m_userAgentStr = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; )";
        IWebProxy proxy = null;

        public HTTPHelper(int timeout)
        {
            m_cookieContainer = new CookieContainer();
            m_timeout = timeout;
            ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(CheckValidationResult);
        }

        public bool CheckValidationResult(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors)
        {
            return true;
        }

        byte[] HTTPGetResponse(HttpWebRequest request)
        {
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream stream = response.GetResponseStream();

            byte[] data = new byte[response.ContentLength];
            stream.Read(data, 0, (int)response.ContentLength);

            stream.Close();

            return data;
        }

        string HTTPGetResponseTxt(HttpWebRequest request)
        {
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream stream = response.GetResponseStream();

            Encoding encode;
            if (response.CharacterSet.ToLowerInvariant().Contains("utf-8"))
                encode = Encoding.UTF8;
            else
                encode = Encoding.Default;
            StreamReader reader = new StreamReader(stream, encode);

            string html = reader.ReadToEnd();

            reader.Close();
            stream.Close();

            return html;
        }

        public string HTTPGetTxt(string url)
        {
            HttpWebRequest request;
            string requestUrl = url;
            while (true)
            {
                request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "GET";
                request.Headers.Add("Accept-Encoding", "gzip,deflate");
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Headers.Add("Accept-Charset", "GB2312,utf-8");
                request.UserAgent = m_userAgentStr;
                request.Timeout = m_timeout;
                request.CookieContainer = m_cookieContainer;
                request.Proxy = proxy;

                if (request.RequestUri != request.Address)//转向
                    requestUrl = request.Address.AbsoluteUri;
                else
                    break;
            }
            return HTTPGetResponseTxt(request);
        }

        public byte[] HTTPGet(string url)
        {
            HttpWebRequest request;
            string requestUrl = url;
            while (true)
            {
                request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "GET";
                request.Headers.Add("Accept-Encoding", "gzip,deflate");
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.UserAgent = m_userAgentStr;
                request.Timeout = m_timeout;
                request.CookieContainer = m_cookieContainer;
                request.Proxy = proxy;

                if (request.RequestUri != request.Address)//转向
                    requestUrl = request.Address.AbsoluteUri;
                else
                    break;
            }

            return HTTPGetResponse(request);
        }

        public string HTTPPostTxt(string url, string poststring)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "POST";
            request.UserAgent = m_userAgentStr;
            request.Headers.Add("Accept-Encoding", "gzip,deflate");
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Headers.Add("Accept-Charset", "GB2312,utf-8");
            request.CookieContainer = m_cookieContainer;
            request.Timeout = m_timeout;
            request.ContentType = "application/x-www-form-urlencoded";
            request.Proxy = proxy;

            byte[] postdata = Encoding.ASCII.GetBytes(poststring);
            request.ContentLength = postdata.Length;
            Stream RequestStream = request.GetRequestStream();
            RequestStream.Write(postdata, 0, postdata.Length);
            RequestStream.Close();

            return HTTPGetResponseTxt(request);
        }
    }
}
