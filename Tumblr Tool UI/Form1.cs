﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Tumblr_Tool.Common_Helpers;
using Tumblr_Tool.Enums;
using Tumblr_Tool.Image_Ripper;
using Tumblr_Tool.Managers;
using Tumblr_Tool.Tumblr_Objects;
using Tumblr_Tool.Tumblr_Stats;

namespace Tumblr_Tool
{
    public partial class mainForm : Form
    {
        private static Tumblr tumblrBlog;
        private AboutForm aboutForm;
        private bool downloadDone = false;
        private List<string> downloadedList = new List<string>();
        private List<int> downloadedSizesList = new List<int>();
        private bool fileDownloadDone = false;
        private FileManager fileManager;
        private string filename;
        private List<string> notDownloadedList = new List<string>();
        private ToolOptions options = new ToolOptions();
        private OptionsForm optionsForm;
        private bool readyForDownload = true;
        private ImageRipper ripper;
        private SaveFile saveFile, logFile;
        private Stopwatch stopWatch = new Stopwatch();
        private TumblrStats tumblrStats;
        private string version = "Beta 0.14.1127";

        private TimeSpan ts;

        public mainForm()
        {
            InitializeComponent();
            txt_WorkStatus.Visible = false;
            lbl_Timer.Text = "";

            bar_Progress.Visible = false;
            fileManager = new FileManager();
            this.Text += " (" + version + ")";
            optionsForm = new OptionsForm();
            optionsForm.mainForm = this;
            aboutForm = new AboutForm();
            aboutForm.mainForm = this;
            aboutForm.version = "Version: " + version;

            this.select_Mode.SelectedIndex = 1;
            optionsForm.apiMode = apiModeEnum.JSON.ToString();

            loadOptions();
            lbl_Size.Text = "";
            lbl_PostCount.Text = "";
        }

        public mainForm(string file)
        {
            InitializeComponent();
            txt_WorkStatus.Visible = false;
            lbl_Timer.Text = "";
            lbl_Size.Text = "";
            lbl_PostCount.Text = "";
            bar_Progress.Visible = false;
            fileManager = new FileManager();
            txt_SaveLocation.Text = Path.GetDirectoryName(file);
            saveFile = fileManager.readTumblrFile(file);

            if (File.Exists(Path.GetDirectoryName(file) + @"\" + Path.GetFileNameWithoutExtension(file) + ".log"))
            {
                logFile = fileManager.readTumblrFile(Path.GetDirectoryName(file) + @"\" + Path.GetFileNameWithoutExtension(file) + ".log");
            }

            txt_TumblrURL.Text = saveFile != null ? saveFile.getBlogURL() : "";
            tumblrBlog = saveFile != null ? saveFile.blog : null;

            this.Text += " (" + version + ")";
            optionsForm = new OptionsForm();
            optionsForm.mainForm = this;
            aboutForm = new AboutForm();
            aboutForm.mainForm = this;
            aboutForm.version = "Version: " + version;
            this.select_Mode.SelectedIndex = 1;
            optionsForm.apiMode = apiModeEnum.JSON.ToString();
            loadOptions();
        }

        public bool isValidURL(string urlString)
        {
            try
            {
                return (urlString.StartsWith("http://"));
            }
            catch (Exception e)
            {
                string error = e.Message;
                return false;
            }
        }

        public void loadOptions()
        {
            optionsForm.setOptions();
            loadOptions(optionsForm.options);
        }

        public void loadOptions(ToolOptions _options)
        {
            this.options = _options;
        }

        public void updateStatusText(string text)
        {
            lbl_Status.Text = text;
            lbl_Status.Invalidate();
            status_Strip.Update();
            status_Strip.Refresh();
        }

        public void updateWorkStatusText(string text)
        {
            if (!txt_WorkStatus.Text.Contains(text))
            {
                txt_WorkStatus.Text += txt_WorkStatus.Text != "" ? "\r\n" : "";
                txt_WorkStatus.Text += text;
                txt_WorkStatus.Update();
                txt_WorkStatus.Refresh();
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.aboutForm.ShowDialog();
        }

        private void btn_Browse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog ofd = new FolderBrowserDialog())
            {
                DialogResult result = ofd.ShowDialog();
                if (result == DialogResult.OK)

                    txt_SaveLocation.Text = ofd.SelectedPath;
            }
        }

        private void btn_Crawl_Click(object sender, EventArgs e)
        {
            btn_Crawl.Enabled = false;
            tab_TumblrStats.Enabled = false;
            lbl_PostCount.Visible = false;
            bar_Progress.Visible = false;
            txt_WorkStatus.Visible = true;
            txt_WorkStatus.Clear();
            lbl_Size.Text = "";
            fileManager = new FileManager();
            stopWatch.Start();
            TimeSpan ts = stopWatch.Elapsed;

            // Format and display the TimeSpan value.
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

            this.Invoke((MethodInvoker)delegate
            {
                lbl_Timer.Text = elapsedTime;
            });
            if (checkFields())
            {
                crawl_Worker.RunWorkerAsync(ripper);

                crawl_UpdateUI_Worker.RunWorkerAsync(ripper);
            }
            else
            {
                btn_Crawl.Enabled = true;
                lbl_PostCount.Visible = false;
                bar_Progress.Visible = false;
                img_DisplayImage.Visible = false;
                tab_TumblrStats.Enabled = true;
            }
        }

        private void btn_GetStats_Click(object sender, EventArgs e)
        {
            lbl_PostCount.Visible = false;
            updateStatusText("Initiliazing...");
            if (isValidURL(txt_StatsTumblrURL.Text))
            {
                if (WebHelper.CheckForInternetConnection())
                {
                    tab_ImageRipper.Enabled = false;

                    tumblrStats = new TumblrStats(new Tumblr("Blog", txt_StatsTumblrURL.Text));

                    tumblrStats.setAPIMode(options.apiMode);

                    getStats_Worker.RunWorkerAsync(tumblrStats);
                    getStatsUI_Worker.RunWorkerAsync(tumblrStats);
                }
                else
                {
                    MessageBox.Show("No internet connection detected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Please enter valid url!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btn_Options_Click(object sender, EventArgs e)
        {
            this.optionsForm.ShowDialog();
        }

        private bool checkFields()
        {
            bool saveLocationEmpty = string.IsNullOrEmpty(txt_SaveLocation.Text);
            bool urlValid = true;

            if (saveLocationEmpty)
            {
                MessageBox.Show("Save Location cannot be left empty! \r\nSelect a valid location on disk", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btn_Browse.Focus();
            }
            else
            {
                if (!isValidURL(txt_TumblrURL.Text))
                {
                    MessageBox.Show("Please enter valid url!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txt_TumblrURL.Focus();
                    urlValid = false;
                }
            }

            return (!saveLocationEmpty && urlValid);
        }

        private void colorizeProgressBar(int value)
        {
            switch (value / 10)
            {
                case 0:
                    bar_Progress.ForeColor = Color.Black;
                    break;

                case 1:
                    bar_Progress.ForeColor = Color.DarkGray;
                    break;

                case 2:
                    bar_Progress.ForeColor = Color.DarkRed;
                    break;

                case 3:
                    bar_Progress.ForeColor = Color.Firebrick;
                    break;

                case 4:
                    bar_Progress.ForeColor = Color.OrangeRed;
                    break;

                case 5:
                    bar_Progress.ForeColor = Color.Navy;
                    break;

                case 6:
                    bar_Progress.ForeColor = Color.DarkBlue;
                    break;

                case 7:
                    bar_Progress.ForeColor = Color.Blue;
                    break;

                case 8:
                    bar_Progress.ForeColor = Color.LightBlue;
                    break;

                case 9:
                    bar_Progress.ForeColor = Color.LimeGreen;
                    break;

                default:
                    bar_Progress.ForeColor = Color.YellowGreen;
                    break;
            }
        }

        private void crawlUIWorker_AfterDone(object sender, RunWorkerCompletedEventArgs e)
        {
            if (ripper != null)
            {
                if (ripper.statusCode == postProcessingCodes.Done)
                {
                    updateWorkStatusText("Indexing Blog done");

                    updateWorkStatusText("Found " + (ripper.totalImagesCount == 0 ? "no" : ripper.totalImagesCount.ToString()) + " new image(s) to download");

                    lbl_PostCount.Text = "";

                    bar_Progress.Value = 0;
                    bar_Progress.Update();

                    bar_Progress.Refresh();
                }
                else
                {
                    if (ripper.statusCode == postProcessingCodes.UnableDownload)
                    {
                        updateWorkStatusText("Error downloading the blog post XML");
                        updateStatusText("Error");
                    }
                    else if (ripper.statusCode == postProcessingCodes.invalidURL)
                    {
                        updateWorkStatusText("Invalid Tumblr URL");
                        updateStatusText("Error");
                    }
                }
            }

            tab_TumblrStats.Enabled = true;

            if (optionsForm.parseOnly)
                btn_Crawl.Enabled = true;

            updateStatusText("Done");
            lbl_PostCount.Visible = false;
            bar_Progress.Visible = false;
        }

        private void crawlUIWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (ripper == null)
            {

            }

            if (ripper != null)
            {
                this.Invoke((MethodInvoker)delegate
                        {
                            bar_Progress.Minimum = 0;
                            bar_Progress.Value = 0;
                            bar_Progress.Step = 1;
                            bar_Progress.Maximum = 100;
                            bar_Progress.Visible = true;
                            lbl_PostCount.Visible = true;
                            // lbl_Timer.Visible = true;
                            lbl_PostCount.Text = "";

                            updateWorkStatusText("Indexing Blog ...");
                        });

                postProcessingCodes prevCode;
                int percent = 0;

                while (percent < 100 && (ripper.statusCode != postProcessingCodes.invalidURL && ripper.statusCode != postProcessingCodes.Done))
                {
                    TimeSpan ts = stopWatch.Elapsed;

                    // Format and display the TimeSpan value.
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        ts.Hours, ts.Minutes, ts.Seconds,
                        ts.Milliseconds / 10);

                    this.Invoke((MethodInvoker)delegate
                    {
                        lbl_Timer.Text = elapsedTime;
                    });
                    if (ripper.statusCode == postProcessingCodes.Started)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            updateWorkStatusText("Starting crawling Tumblr @ " + txt_TumblrURL.Text + " ... ");
                            updateStatusText("Starting ...");
                        });
                    }

                    if (ripper.statusCode == postProcessingCodes.Crawling)
                    {
                        percent = ripper.percentComplete;
                        this.Invoke((MethodInvoker)delegate
                        {
                            updateWorkStatusText("There are " + ripper.totalPosts + " photo posts found.");
                        });

                        if (percent > 100)
                            percent = 100;

                        this.Invoke((MethodInvoker)delegate
                           {
                               // colorizeProgressBar(percent);
                           });

                        this.Invoke((MethodInvoker)delegate
                           {
                               updateStatusText("Crawling...");
                               // lbl_PostCount.Visible = true;
                               lbl_PercentBar.Visible = true;
                               lbl_PostCount.Visible = true;
                               lbl_PercentBar.Text = percent.ToString() + "%";
                               lbl_PostCount.Text = ripper.parsed.ToString() + "/" + ripper.totalPosts.ToString();
                               bar_Progress.Value = percent;
                           });
                    }

                    prevCode = ripper.statusCode;
                }

                if (ripper.statusCode == postProcessingCodes.invalidURL)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        updateStatusText("Error");
                        lbl_PostCount.Visible = false;
                        bar_Progress.Visible = false;
                    });
                    MessageBox.Show("Invalid Tumblr URL", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                while (ripper.statusCode != postProcessingCodes.Done)
                {
                    if (ripper.statusCode == postProcessingCodes.Parsing)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            updateStatusText("Parsing ...");
                        });
                    }
                }
            }
        }

        private void crawlWorker_AfterDone(object sender, RunWorkerCompletedEventArgs e)
        {
            if (ripper != null)
            {
                if (ripper.statusCode == postProcessingCodes.Done)
                {
                    saveFile.blog = ripper.blog;

                    if (!optionsForm.parseOnly)
                    {
                        fileManager.totalToDownload = ripper.totalImagesCount;
                        download_Worker.RunWorkerAsync(ripper.imagesList);
                        download_UIUpdate_Worker.RunWorkerAsync(fileManager);
                    }
                    else
                    {
                        stopWatch.Stop();
                    }
                }
            }
        }

        private void crawlWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            string elapsedTime;

            this.Invoke((MethodInvoker)delegate
            {
                updateStatusText("Initializing ...");
                updateWorkStatusText("Initializing ... ");
                updateWorkStatusText("Checking for internet connection ...");
            });

            ts = stopWatch.Elapsed;

            // Format and display the TimeSpan value.
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
               ts.Hours, ts.Minutes, ts.Seconds,
               ts.Milliseconds / 10);

            this.Invoke((MethodInvoker)delegate
            {
                lbl_Timer.Text = elapsedTime;
            });

            if (WebHelper.CheckForInternetConnection())
            {
                this.Invoke((MethodInvoker)delegate
            {
                updateWorkStatusText("Internet Connection found ...");
                updateWorkStatusText("Getting Blog info ...");
            });
                tumblrBlog = new Tumblr();
                tumblrBlog.cname = txt_TumblrURL.Text;
                this.ts = stopWatch.Elapsed;

                // Format and display the TimeSpan value.
                elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                   ts.Hours, ts.Minutes, ts.Seconds,
                   ts.Milliseconds / 10);

                this.Invoke((MethodInvoker)delegate
                {
                    lbl_Timer.Text = elapsedTime;
                });

                this.ripper = new ImageRipper(tumblrBlog, txt_SaveLocation.Text, optionsForm.parsePhotoSets, optionsForm.parseJPEG, optionsForm.parsePNG, optionsForm.parseGIF, 0);

                if (this.ripper != null)
                {
                    this.ripper.setAPIMode(options.apiMode);
                    this.ripper.setLogFile(logFile);

                    if (this.ripper.setBlogInfo())
                    {
                        ts = stopWatch.Elapsed;

                        // Format and display the TimeSpan value.
                        elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                            ts.Hours, ts.Minutes, ts.Seconds,
                            ts.Milliseconds / 10);

                        this.Invoke((MethodInvoker)delegate
                        {
                            lbl_Timer.Text = elapsedTime;

                            lbl_PostCount.Text = "0 / 0";
                            lbl_PostCount.Visible = false;
                            txt_WorkStatus.Visible = true;

                            txt_WorkStatus.SelectionStart = txt_WorkStatus.Text.Length;
                        });

                        if (saveFile == null && !saveTumblrFile(this.ripper.blog.name))
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                updateWorkStatusText("Unable to save .tumblr file");
                                updateStatusText("Error");
                            });
                        }
                        else
                        {
                            if (this.ripper != null)
                            {
                                int mode = 0;
                                this.Invoke((MethodInvoker)delegate
                                {
                                    mode = select_Mode.SelectedIndex + 1;
                                });

                                tumblrBlog = this.ripper.parseBlogPosts(mode);
                            }
                        }
                    }
                    else
                    {
                        updateStatusText("Error");
                        updateWorkStatusText("Invalid Tumblr URL: " + txt_TumblrURL.Text);
                        btn_Crawl.Enabled = true;
                        lbl_PostCount.Visible = false;
                        bar_Progress.Visible = false;
                        img_DisplayImage.Visible = false;
                        tab_TumblrStats.Enabled = true;
                    }
                }
            }
            else
            {
                updateStatusText("Error");
                updateWorkStatusText("No internet connection detected!");
                btn_Crawl.Enabled = true;
                lbl_PostCount.Visible = false;
                bar_Progress.Visible = false;
                img_DisplayImage.Visible = false;
                tab_TumblrStats.Enabled = true;
            }

            Thread.Sleep(100);
        }

        private void downloadUIUpdate_AfterDone(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Invoke((MethodInvoker)delegate
                        {
                            try
                            {
                                img_DisplayImage.ImageLocation = downloadedList[downloadedList.Count - 1];
                            }
                            catch
                            {
                                //
                            }
                            // img_DisplayImage.Image = Image.FromFile(fileManager.downloadedList[fileManager.downloadedList.Count - 1]);
                            img_DisplayImage.Update();
                            img_DisplayImage.Refresh();
                        });

            if (fileManager.statusCode == downloadStatusCodes.Done)
            {
                updateWorkStatusText("Downloaded " + downloadedList.Count.ToString() + " image(s).");
            }
            updateStatusText("Done");
            lbl_PostCount.Visible = false;
            lbl_PercentBar.Visible = false;
            bar_Progress.Visible = false;
            btn_Crawl.Enabled = true;
            tab_TumblrStats.Enabled = true;
        }

        private void downloadUIUpdate_DoWork(object sender, DoWorkEventArgs e)
        {
            FileManager fileManager = (FileManager)e.Argument;

            if (fileManager.totalToDownload == 0)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    bar_Progress.Visible = false;

                    updateWorkStatusText("No new images to download");
                    updateStatusText("Done"); ;
                });
            }
            else
            {
                while (fileManager.statusCode != downloadStatusCodes.Downloading)
                {
                    if (fileManager.statusCode == downloadStatusCodes.Preparing)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            updateStatusText("Preparing..."); ;
                        });
                    }
                }

                if (fileManager.statusCode == downloadStatusCodes.Downloading)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        img_DisplayImage.Visible = true;
                        bar_Progress.Step = 1;
                        bar_Progress.Minimum = 0;
                        bar_Progress.Maximum = 100;
                        bar_Progress.Visible = true;
                        lbl_PostCount.Visible = true;
                        lbl_PercentBar.Visible = true;

                        lbl_PostCount.Visible = true;
                        updateWorkStatusText("Downloading images ...");
                        updateStatusText("Downloading..."); ;
                    });
                    lbl_PostCount.Visible = true;

                    decimal totalLength = 0;
                    while (downloadDone != true)
                    {
                        TimeSpan ts = stopWatch.Elapsed;

                        // Format and display the TimeSpan value.
                        string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                            ts.Hours, ts.Minutes, ts.Seconds,
                            ts.Milliseconds / 10);

                        this.Invoke((MethodInvoker)delegate
                            {
                                lbl_Timer.Text = elapsedTime;
                            });

                        int c = 0;
                        int f = 0;

                        if (notDownloadedList.Count != 0 && f != notDownloadedList.Count)
                        {
                            f = notDownloadedList.Count;

                            this.Invoke((MethodInvoker)delegate
                            {
                                updateWorkStatusText("Error: Unable to download " + notDownloadedList[notDownloadedList.Count - 1]);
                            });
                        }

                        if (downloadedList.Count != 0 && c != downloadedList.Count)
                        {
                            c = downloadedList.Count;

                            this.Invoke((MethodInvoker)delegate
                    {
                        try
                        {
                            img_DisplayImage.ImageLocation = downloadedList[c - 1];
                            img_DisplayImage.Load();
                            img_DisplayImage.Update();
                            img_DisplayImage.Refresh();
                        }
                        catch (Exception)
                        {
                            readyForDownload = true;
                        }

                        int downloaded = fileManager.downloadedList.Count + 1;
                        int total = fileManager.totalToDownload;

                        if (downloaded > total)
                            downloaded = total;

                        lbl_PostCount.Text = downloaded.ToString() + " / " + total.ToString();

                        try
                        {
                            totalLength = (downloadedSizesList.Sum(x => Convert.ToInt32(x)) / (decimal)1024 / (decimal)1024);
                            decimal totalLengthNum = totalLength > 1024 ? totalLength / 1024 : totalLength;
                            string suffix = totalLength > 1024 ? "GB" : "MB";

                            lbl_Size.Text = (totalLengthNum).ToString("0.00") + " " + suffix;
                        }
                        catch (Exception)
                        {
                            //
                        }

                        if (bar_Progress.Value != (int)fileManager.percentDownloaded)
                        {
                            bar_Progress.Value = (int)fileManager.percentDownloaded;
                            lbl_PercentBar.Text = fileManager.percentDownloaded.ToString() + "%";
                            bar_Progress.Update();
                            bar_Progress.Refresh();
                        }
                    });
                        }
                    }
                }
            }
        }

        private void downloadWorker_AfterDone(object sender, RunWorkerCompletedEventArgs e)
        {
            saveFile.blog.posts = null;
            saveTumblrFile(ripper.blog.name);
            downloadDone = true;
            stopWatch.Stop();
        }

        private void downloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            downloadDone = false;
            fileDownloadDone = false;
            List<string> imagesList = (List<string>)e.Argument;
            fileManager.statusCode = downloadStatusCodes.Preparing;
            if (ripper.imagesList != null && ripper.imagesList.Count != 0)
            {
                int j = 0;
                fileManager.statusCode = downloadStatusCodes.Downloading;
                readyForDownload = true;

                foreach (string photoURL in imagesList)
                {
                    while (!readyForDownload)
                    {
                        //wait till ready for download
                    }

                    fileDownloadDone = false;
                    fileManager.statusCode = downloadStatusCodes.Downloading;
                    bool downloaded = false;
                    string fullPath = "";

                    fullPath = FileHelper.getFullFilePath(Path.GetFileName(photoURL), txt_SaveLocation.Text);

                    downloaded = fileManager.downloadFile(photoURL, txt_SaveLocation.Text, string.Empty, 1);

                    if (downloaded)
                    {
                        if (ripper.commentsList.ContainsKey(Path.GetFileName(photoURL)))
                        {
                            ImageHelper.addImageDescription(fullPath, ripper.commentsList[Path.GetFileName(photoURL)]);
                        }

                        j++;
                        fileDownloadDone = true;
                        fullPath = FileHelper.fixFileName(fullPath);
                        downloadedList.Add(fullPath);

                        downloadedSizesList.Add((int)new FileInfo(fullPath).Length);
                    }
                    else if (fileManager.statusCode == downloadStatusCodes.UnableDownload)
                    {
                        // notDownloadedList.Add(photoURL);
                    }
                }
                fileManager.statusCode = downloadStatusCodes.Done;
                downloadDone = true;
            }
        }

        private void fileBW_AfterDone(object sender, RunWorkerCompletedEventArgs e)
        {
            txt_SaveLocation.Text = !string.IsNullOrEmpty(filename) ? Path.GetDirectoryName(filename) : "";
            txt_TumblrURL.Text = saveFile != null ? saveFile.blog.cname : "Error parsing savefile";
        }

        private void fileBW_DoWork(object sender, DoWorkEventArgs e)
        {
            string filename = (string)e.Argument;
            this.filename = filename;

            saveFile = !string.IsNullOrEmpty(filename) ? fileManager.readTumblrFile(filename) : null;

            if (saveFile != null)
            {
                if (File.Exists(Path.GetDirectoryName(filename) + @"\" + Path.GetFileNameWithoutExtension(filename) + ".log"))
                {
                    logFile = fileManager.readTumblrFile(Path.GetDirectoryName(filename) + @"\" + Path.GetFileNameWithoutExtension(filename) + ".log");
                }
            }
            tumblrBlog = saveFile != null ? saveFile.blog : null;

            this.Invoke((MethodInvoker)delegate
                {
                });
        }

        private void getStatsUIWorker_AfterDone(object sender, RunWorkerCompletedEventArgs e)
        {
            tab_ImageRipper.Enabled = true;
            updateStatusText("Done");
            lbl_PostCount.Visible = false;
            bar_Progress.Visible = false;
        }

        private void getStatsUIWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            TumblrStats tumblrStats = (TumblrStats)e.Argument;

            this.Invoke((MethodInvoker)delegate
                {
                    box_PostStats.Visible = false;
                    box_BlogInfo.Visible = false;
                    lbl_PostCount.Text = "";
                    lbl_PostCount.Visible = true;
                    bar_Progress.Minimum = 0;
                    bar_Progress.Value = 0;
                    bar_Progress.Maximum = 100;
                    bar_Progress.Step = 1;
                    bar_Progress.Visible = true;
                    lbl_Size.Visible = false;
                });

            while (string.IsNullOrEmpty(tumblrStats.blog.title) && tumblrStats.totalPosts <= 0)
            {
                lbl_Stats_TotalCount.Visible = false;
            }

            this.Invoke((MethodInvoker)delegate
            {
                box_PostStats.Visible = true;
                box_BlogInfo.Visible = true;
                lbl_Stats_TotalCount.Visible = true;
                lbl_Stats_BlogTitle.Text = tumblrStats.blog.title;
                lbl_Stats_TotalCount.Text = tumblrStats.totalPosts.ToString();
                txt_Stats_BlogDescription.Text = WebHelper.stripHTMLTags(tumblrStats.blog.description);
            });

            int percent = 0;
            while (percent < 100)
            {
                percent = (int)(((double)tumblrStats.parsed / (double)tumblrStats.totalPosts) * 100.00);
                if (percent < 0)
                    percent = 0;

                if (percent >= 100)
                    percent = 100;

                if (tumblrStats.statusCode == postProcessingCodes.invalidURL)
                {
                    percent = 100;

                    this.Invoke((MethodInvoker)delegate
                {
                    updateStatusText("Error");
                    lbl_PostCount.Visible = false;
                    bar_Progress.Visible = false;
                    lbl_Size.Visible = false;
                });
                    MessageBox.Show("Invalid Tumblr URL", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        updateStatusText("Getting stats ...");
                        lbl_PercentBar.Visible = true;
                        lbl_Stats_TotalCount.Text = tumblrStats.totalPosts.ToString();

                        lbl_Stats_PhotoCount.Text = tumblrStats.photoPosts.ToString();
                        lbl_Stats_TextCount.Text = tumblrStats.textPosts.ToString();
                        lbl_Stats_QuoteStats.Text = tumblrStats.quotePosts.ToString();
                        lbl_Stats_LinkCount.Text = tumblrStats.linkPosts.ToString();
                        lbl_Stats_AudioCount.Text = tumblrStats.audioPosts.ToString();
                        lbl_Stats_VideoCount.Text = tumblrStats.videoPosts.ToString();
                        lbl_Stats_ChatCount.Text = tumblrStats.chatPosts.ToString();
                        lbl_Stats_AnswerCount.Text = tumblrStats.answerPosts.ToString();
                        lbl_PercentBar.Text = percent.ToString() + "%";
                        lbl_PostCount.Visible = true;
                        lbl_PostCount.Text = tumblrStats.parsed.ToString() + "/" + tumblrStats.totalPosts.ToString();
                        bar_Progress.Value = percent;
                    });
                }
            }
        }

        private void getStatsWorker_AfterDone(object sender, RunWorkerCompletedEventArgs e)
        {
        }

        private void getStatsWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.Sleep(100);
            TumblrStats tumblrStats = (TumblrStats)e.Argument;
            tumblrStats.parsePosts();
        }

        private void imageLoaded(object sender, AsyncCompletedEventArgs e)
        {
            readyForDownload = true;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.AddExtension = true;
                ofd.DefaultExt = ".tumblr";
                ofd.RestoreDirectory = true;
                ofd.Filter = "Tumblr Tools Files (.tumblr)|*.tumblr|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    fileBackgroundWorker.RunWorkerAsync(ofd.FileName);
                }
            }
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.optionsForm.ShowDialog();
        }

        private bool saveLogFile(string name)
        {
            if (saveFile == null)
            {
                saveFile = new SaveFile(name + ".log", ripper.blog);
            }
            return fileManager.saveTumblrFile(txt_SaveLocation.Text + @"\" + saveFile.getFileName(), saveFile);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFile saveFile = new SaveFile(ripper.blog.name + ".tumblr", ripper.blog);
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.AddExtension = true;
                sfd.DefaultExt = ".tumblr";
                sfd.Filter = "Tumblr Tools Files (.tumblr)|*.tumblr";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    if (fileManager.saveTumblrFile(sfd.FileName, saveFile))
                    {
                        MessageBox.Show("Saved", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private bool saveTumblrFile(string name)
        {
            if (saveFile == null)
            {
                saveFile = new SaveFile(name + ".tumblr", ripper.blog);
            }
            return fileManager.saveTumblrFile(txt_SaveLocation.Text + @"\" + saveFile.getFileName(), saveFile);
        }

        private void statsTumblrURLUpdate(object sender, EventArgs e)
        {
            txt_StatsTumblrURL.Text = txt_TumblrURL.Text;
        }

        private void tabMainTabSelect_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (!e.TabPage.Enabled)
            {
                e.Cancel = true;
            }
        }

        private void workStatusAutoScroll(object sender, EventArgs e)
        {
            txt_WorkStatus.SelectionStart = txt_WorkStatus.TextLength;
            txt_WorkStatus.ScrollToCaret();
        }
    }
}