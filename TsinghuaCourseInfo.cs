using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace GetCourseInfoV2
{
    [Serializable]
    abstract class CourseInfoItem : IEquatable<CourseInfoItem>
    {
        public CourseInfo Course;
        public string Url;
        public string Title;
        public bool IsImportant;

        public abstract string GetHashString();
        public abstract string ItemTypeStr();

        public string CourseName()
        {
            return Course.Title;
        }

        public bool Equals(CourseInfoItem other)
        {
            return GetHashString() == other.GetHashString();
        }

        public override int GetHashCode()
        {
            return GetHashString().GetHashCode();
        }

        public static string HtmlDecode(string html)
        {
            var result = html;
            result = Regex.Replace(result, "^\\s+", "");
            result = Regex.Replace(result, "\\s+$", "");
            return HttpUtility.HtmlDecode(result);
        }
    }

    [DebuggerDisplayAttribute("{Title}")]
    [Serializable]
    sealed class CourseDiscuss : CourseInfoItem
    {
        public string Publisher;
        public string ReplyCount;
        public string Date;

        public override string ItemTypeStr()
        {
            return "课程讨论";
        }

        public override string GetHashString()
        {
            return Course.Title + Title + Publisher + Date + ReplyCount;
        }

        public static CourseDiscuss Parse(CourseInfo course, HtmlNode node)
        {
            var title = HtmlDecode(node.SelectSingleNode("./td[1]/a").InnerText);
            var url = "http://learn.tsinghua.edu.cn/MultiLanguage/public/bbs/" + node.SelectSingleNode("./td[1]/a").GetAttributeValue("href", "");
            var publisher = HtmlDecode(node.SelectSingleNode("./td[2]").InnerText);
            var date = node.SelectSingleNode("./td[4]").InnerText;
            var replycount = node.SelectSingleNode("./td[3]").InnerText.Split('/')[0];
            var important = false;

            return new CourseDiscuss()
            {
                Course = course,
                Url = url,
                Title = title,
                Publisher = publisher,
                ReplyCount = replycount,
                Date = date,
                IsImportant = important
            };
        }
    }

    enum CourseType
    {
        sjkc所教课程,
        hjkc合教课程
    }

    [DebuggerDisplayAttribute("{Title}")]
    [Serializable]
    sealed class CourseInfo
    {
        [NonSerialized]
        public TsinghuaCourseInfo CourseInfoHelper;

        public string Title;
        public string ID;
        public CourseType Type;

        public CourseDiscuss[] CourseDiscussList;

        public void GetCourseInfo()
        {
            GetCourseDiscussList();
        }

        public static string HtmlTrim(string html)
        {
            var result = html;
            result = Regex.Replace(result, "^\\s+", "");
            result = Regex.Replace(result, "\\s+$", "");
            return result;
        }

        void GetCourseDiscussList()
        {
            var url = "https://learn.tsinghua.edu.cn/MultiLanguage/public/bbs/gettalkid_student.jsp?course_id=" + ID;
            string html = CourseInfoHelper.HttpHelper.HTTPGetTxt(url);
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(".//table[@id=\"info_1\"]//table//table//tr[@class]");

            if (nodes != null)
            {
                CourseDiscussList = nodes
                        .Select(node => CourseDiscuss.Parse(this, node))
                        .ToArray();

                foreach (var discuss in CourseDiscussList)
                {
                    try
                    {
                        string discussHtml = CourseInfoHelper.HttpHelper.HTTPGetTxt(discuss.Url);
                        var discussDoc = new HtmlDocument();
                        discussDoc.LoadHtml(discussHtml);
                        var discussnodes = discussDoc.DocumentNode.SelectNodes(".//table[@id=\"info_1\"]//table[@id=\"table_box\"]//tr[1]//td[4]");
                        if(discussnodes != null)
                        {
                            var newDtStr = discussnodes.Select(node => HtmlTrim(node.InnerText)).Max();
                            discuss.Date = newDtStr;
                        }
                    }
                    catch { }
                }
            }
            else
            {
                CourseDiscussList = new CourseDiscuss[0];
            }
        }

        public static CourseInfo Parse(TsinghuaCourseInfo helper, HtmlNode node, CourseType type)
        {
            var title = CourseInfoItem.HtmlDecode(node.SelectSingleNode("./td[1]/a").InnerText);
            title = Regex.Replace(title, "\\([^()]*\\)$", "");

            var id = node.SelectSingleNode("./td[1]/a").GetAttributeValue("href", "").Split('=')[1];

            return new CourseInfo()
            {
                CourseInfoHelper = helper,
                ID = id,
                Title = title,
                Type = type
            };
        }
    }

    class TsinghuaCourseInfo
    {
        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool InternetSetCookie(string lpszUrlName, string lbszCookieName, string lpszCookieData);

        [DllImport("kernel32.dll")]
        public static extern Int32 GetLastError();

        string m_userID;
        string m_userPassword;
        public HTTPHelper HttpHelper;
        public CourseInfo[] CourseList;
        public delegate void ProcessLogDelegate(string str);
        public ProcessLogDelegate ProcessLog;

        public CourseInfoItem[] OldItemList;
        public CourseInfoItem[] AllItemList;

        public Boolean ScanSJKC;
        public Boolean ScanHJKC;

        public TsinghuaCourseInfo(string userID, string userPassword)
        {
            HttpHelper = new HTTPHelper(10000);
            m_userID = userID;
            m_userPassword = userPassword;
            ScanSJKC = true;
            ScanHJKC = false;
        }

        public void Login()
        {
            string postStr = string.Format("userid={0}&userpass={1}&submit1=%B5%C7%C2%BC", m_userID, m_userPassword);
            ProcessLog("正在登录 网络学堂...");

            var html = HttpHelper.HTTPPostTxt("https://learn.tsinghua.edu.cn/MultiLanguage/lesson/teacher/loginteacher.jsp", postStr);

            html = HttpHelper.HTTPPostTxt("https://learn.tsinghua.edu.cn/MultiLanguage/lesson/teacher/mainteacher.jsp", postStr);

            if (!html.Contains("MyCourse.jsp?language=cn"))
                throw new Exception("登录错误，请重新检查用户名和密码。");

            string webSiteUrl = "https://learn.tsinghua.edu.cn/";

            var cookies = HttpHelper.m_cookieContainer.GetCookies(new Uri(webSiteUrl));
            
            foreach (var cookie in cookies)
            {
                string cookieStr = cookie.ToString();
                int index = cookieStr.IndexOf('=');
                string cookieName = cookieStr.Substring(0, index);
                string cookieData = cookieStr.Substring(index + 1);
                InternetSetCookie(webSiteUrl, cookieName, cookieData);
            }
        }

        public void GetCourseList()
        {
            ProcessLog("正在获取 课程列表");
            var url = "https://learn.tsinghua.edu.cn/MultiLanguage/lesson/teacher/MyCourse.jsp?language=cn";
            string html = HttpHelper.HTTPGetTxt(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(".//table[@id=\"info_1\"]//tr");

            if (nodes == null)
            {
                CourseList = new CourseInfo[0];
                return;
            }

            var sjkc = nodes
                .Skip(2)
                .TakeWhile(node => node.Attributes.Contains("class"))
                .Select(node => CourseInfo.Parse(this, node, CourseType.sjkc所教课程))
                .ToArray();

            var hjkc = nodes
                .Skip(2 + sjkc.Length + 2)
                .TakeWhile(node => node.Attributes.Contains("class"))
                .Select(node => CourseInfo.Parse(this, node, CourseType.hjkc合教课程))
                .ToArray();

            CourseList = CourseFilter(sjkc.Union(hjkc).ToArray());
        }

        public CourseInfo[] CourseFilter(CourseInfo[] allCourses)
        {
            IEnumerable<CourseInfo> result = allCourses;
            if (!ScanSJKC)
                result = result.Where(c => c.Type != CourseType.sjkc所教课程);

            if (!ScanHJKC)
                result = result.Where(c => c.Type != CourseType.hjkc合教课程);

            return result.ToArray();
        }

        public void GetCoursesInfo()
        {
            foreach (var course in CourseList)
            {
                ProcessLog(string.Format("正在获取 课程 [{0}] 的信息...", course.Title));
                course.GetCourseInfo();
            }
        }

        void GetAllItems()
        {
            var itemList = new List<CourseInfoItem>();

            foreach (var course in CourseList)
            {
                itemList.AddRange(course.CourseDiscussList);
            }

            AllItemList = itemList.ToArray();
        }

        public CourseInfoItem[] GetNewItems()
        {
            GetAllItems();
            SaveItemToFile();

            if (OldItemList == null)
                return AllItemList;
            else
            {
                return AllItemList.Except(OldItemList).ToArray();
                
            }
        }

        static string DataFileName = "userdata.dat";

        public static TsinghuaCourseInfo LoadFromDataFile()
        {
            TsinghuaCourseInfo item = new TsinghuaCourseInfo(null, null);
            try
            {
                using (var fs = new FileStream(DataFileName, FileMode.Open))
                {
                    var randBytesLength = fs.ReadByte();
                    randBytesLength *= randBytesLength;

                    fs.Seek(randBytesLength, SeekOrigin.Current);

                    var rand = new Random(randBytesLength);

                    using (var mstream = new MemoryStream())
                    {
                        var buffer = new byte[1024];
                        while (true)
                        {
                            var len = fs.Read(buffer, 0, buffer.Length);

                            buffer = buffer.Select(b => (byte)(b ^ (byte)rand.Next(256))).ToArray();

                            mstream.Write(buffer, 0, len);

                            if (len < buffer.Length)
                                break;
                        }

                        mstream.Seek(0, SeekOrigin.Begin);

                        using (var stream = new GZipStream(mstream, CompressionMode.Decompress))
                        {
                            BinaryFormatter formatter = new BinaryFormatter();
                            item.m_userID = (string)formatter.Deserialize(stream);
                            item.m_userPassword = (string)formatter.Deserialize(stream);
                            item.OldItemList = (CourseInfoItem[])formatter.Deserialize(stream);
                            item.ScanSJKC = Boolean.Parse(formatter.Deserialize(stream) as string);
                            item.ScanHJKC = Boolean.Parse(formatter.Deserialize(stream) as string);
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
            return item;
        }

        public void SaveItemToFile()
        {
            using (var fs = new FileStream(DataFileName, FileMode.Create))
            {
                //使用固定种子，这样前面的随机数据不再因每次运行而改变
                var rand = new Random(m_userID.GetHashCode());

                //随机长度
                var randBytesLength = rand.Next(32, 64);
                fs.WriteByte((byte)randBytesLength);
                randBytesLength *= randBytesLength;

                var array = new byte[randBytesLength];
                rand.NextBytes(array);

                fs.Write(array, 0, randBytesLength);

                rand = new Random(randBytesLength);
                using (var mstream = new MemoryStream())
                {
                    using (var stream = new GZipStream(mstream, CompressionMode.Compress))
                    {
                        var formatter = new BinaryFormatter();
                        formatter.Serialize(stream, m_userID);
                        formatter.Serialize(stream, m_userPassword);
                        formatter.Serialize(stream, AllItemList);
                        formatter.Serialize(stream, ScanSJKC.ToString());
                        formatter.Serialize(stream, ScanHJKC.ToString());
                    }

                    var transformData = mstream.ToArray()
                        .Select(b => (byte)(b ^ (byte)rand.Next(256)))
                        .ToArray();

                    fs.Write(transformData, 0, transformData.Length);
                }
            }
        }
    }


    abstract class ViewListItemData : IComparable<ViewListItemData>
    {
        public abstract string Title();
        public abstract string Url();
        public abstract int TypeIndex();
        public abstract string TypeStr();
        public abstract string Text();
        public abstract bool IsImportant();
        public abstract string TextDate();
        public abstract string CourseID();

        public int CompareTo(ViewListItemData other)
        {
            int result = Title().CompareTo(other.Title());
            if (result != 0)
                return result;

            result = TypeIndex().CompareTo(other.TypeIndex());
            if (result != 0)
                return result;

            return -(TextDate().CompareTo(other.TextDate()));
        }
    }

    sealed class ViewListItemData_Normal : ViewListItemData
    {
        public CourseInfoItem Item;

        public ViewListItemData_Normal(CourseInfoItem item)
        {
            Item = item;
        }

        public override string Title()
        {
            return Item.CourseName();
        }

        public override string Url()
        {
            return Item.Url;
        }

        public override int TypeIndex()
        {
            if (Item is CourseDiscuss)
                return 4;

            throw new NotImplementedException();
        }

        public override string TypeStr()
        {
            return Item.ItemTypeStr();
        }

        public override string Text()
        {
            return Item.Title;
        }

        public override bool IsImportant()
        {
            return Item.IsImportant;
        }

        public override string TextDate()
        {
            if (Item is CourseDiscuss)
                return (Item as CourseDiscuss).Date;

            throw new NotImplementedException();
        }

        public override string CourseID()
        {
            return Item.Course.ID;
        }
    }

}
