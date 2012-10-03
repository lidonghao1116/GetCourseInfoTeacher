using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web;

namespace GetCourseInfoV2
{

    public partial class Form1 : Form
    {
        static readonly string varStr = "V1.0 Build 1";

        public Form1()
        {
            InitializeComponent();
            webBrowser1.ScriptErrorsSuppressed = true;
            Text = "教师网络学堂更新检测器 " + varStr;
        }

        class NoLoginIn : Exception
        {
            public NoLoginIn() : base() { }
        }

        TsinghuaCourseInfo GetTsinghuaCourseInfoHelper()
        {
            var helper = TsinghuaCourseInfo.LoadFromDataFile();

            if (helper == null)
            {
                this.Invoke(new Action(() =>
                {
                    var loginForm = new LoginForm();

                    if (loginForm.ShowDialog() != DialogResult.Yes)
                    {
                        menuStrip1.Enabled = true;
                        显示网络学堂主界面ToolStripMenuItem.Enabled = false;
                        选择课程范围ToolStripMenuItem.Enabled = false;
                        throw new NoLoginIn();
                    }

                    helper = new TsinghuaCourseInfo(loginForm.textBox1.Text, loginForm.maskedTextBox1.Text);
                }));
            }

            return helper;
        }

        List<ViewListItemData> GetItemDataList(TsinghuaCourseInfo helper)
        {
            var newItems = helper.GetNewItems();
            var itemOnShow = new List<ViewListItemData>();

            foreach (var item in newItems)
            {
                itemOnShow.Add(new ViewListItemData_Normal(item));
            }

            itemOnShow.Sort();

            return itemOnShow;
        }

        void UpdateInfo_MainThread(List<ViewListItemData> itemOnShow)
        {
            显示网络学堂主界面ToolStripMenuItem.Enabled = true;
            选择课程范围ToolStripMenuItem.Enabled = true;

            var viewItemList = new List<ListViewItem>();

            foreach (var item in itemOnShow)
            {
                var listViewItem = new ListViewItem(item.Title());

                if (item.IsImportant())
                    listViewItem.ForeColor = Color.Red;

                listViewItem.SubItems.Add(item.TypeStr());
                listViewItem.SubItems.Add(item.Text());
                listViewItem.Tag = item;
                viewItemList.Add(listViewItem);
            }

            listView1.Items.Clear();

            listView1.Items.AddRange(viewItemList.ToArray());

            toolStripStatusLabel1.Text = string.Format("检测完成，共有 {0} 项更新。", listView1.Items.Count);

            menuStrip1.Enabled = true;
        }

        void UpdateInfo()
        {
            this.BeginInvoke(new Action(() =>
            {
                menuStrip1.Enabled = false;
            }));

            try
            {
                var helper = GetTsinghuaCourseInfoHelper();

                helper.ProcessLog += new TsinghuaCourseInfo.ProcessLogDelegate(s => toolStripStatusLabel1.Text = s);
                helper.Login();
                helper.GetCourseList();
                helper.GetCoursesInfo();

                ShowMainPage();

                所教课程ToolStripMenuItem.Checked = helper.ScanSJKC;
                合教课程ToolStripMenuItem.Checked = helper.ScanHJKC;

                var itemOnShow = GetItemDataList(helper);

                this.BeginInvoke(new Action(() => UpdateInfo_MainThread(itemOnShow)));
            }
            catch (NoLoginIn) { }
        }

        void ThreadUpdateInfo()
        {
            var thread = new Thread(new ParameterizedThreadStart(o =>
                {
                    try
                    {
                        UpdateInfo();
                    }
                    catch (Exception e)
                    {
                        this.BeginInvoke(new Action(() =>
                            {
                                toolStripStatusLabel1.Text = string.Format("发生错误 {0}", e.Message);
                                menuStrip1.Enabled = true;
                            }));
                    }
                }));

            thread.IsBackground = true;
            thread.Start();
        }

        void ShowMainPage()
        {
            webBrowser1.Navigate("http://learn.tsinghua.edu.cn/MultiLanguage/lesson/teacher/MyCourse.jsp?language=cn");
        }

        private void webBrowser1_NewWindow(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            try
            {
                string url = webBrowser1.Document.ActiveElement.GetAttribute("href");

                webBrowser1.Navigate(url);
            }
            catch { }
        }

        private void listView1_ItemActivate(object sender, EventArgs e)
        {
            webBrowser1.Navigate((listView1.SelectedItems[0].Tag as ViewListItemData).Url());
        }

        private void 重新检测ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ThreadUpdateInfo();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ThreadUpdateInfo();
        }

        private void 显示网络学堂主界面ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.SelectedItems.Clear();
            ShowMainPage();
        }

        public string HtmlDecode(string html)
        {
            var result = html;
            result = Regex.Replace(result, "^\\s+", "");
            result = Regex.Replace(result, "\\s+$", "");
            return HttpUtility.HtmlDecode(result);
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {

        }

        private void ToolStripMenuItemCourseFilter(object sender, EventArgs e)
        {
            var helper = TsinghuaCourseInfo.LoadFromDataFile();

            if (sender == 所教课程ToolStripMenuItem)
                helper.ScanSJKC = 所教课程ToolStripMenuItem.Checked;
            else if (sender == 合教课程ToolStripMenuItem)
                helper.ScanHJKC = 合教课程ToolStripMenuItem.Checked;

            helper.AllItemList = helper.OldItemList;

            helper.SaveItemToFile();

            MessageBox.Show("设置在下次刷新时生效。", Text);
        }
    }
}
