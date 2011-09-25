//////////////////////////////////////////////////////////////////////////
// Code Named: PG-Ripper
// Function  : Extracts Images posted on PG forums and attempts to fetch
//			   them to disk.
//
// This software is licensed under the MIT license. See license.txt for
// details.
// 
// Copyright (c) The Watcher 
// Partial Rights Reserved.
// 
//////////////////////////////////////////////////////////////////////////
// This file is part of the PG-Ripper project base.

namespace PGRipper
{
    #region

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Web;
    using System.Windows.Forms;

    using PGRipper.Objects;

    #endregion

    /// <summary>
    /// Summary description for Utility.
    /// </summary>
    public class Utility
    {
        #region Constants and Fields

        /// <summary>
        /// The app.
        /// </summary>
        static readonly Configuration Conf = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        

        /// <summary>
        /// The conf.
        /// </summary>
        static readonly AppSettingsSection App = (AppSettingsSection)Conf.Sections["appSettings"];

        #endregion

        #region Public Methods

        /// <summary>
        /// Check the FilePath for Length because if its more then 260 characters long it will crash
        /// </summary>
        /// <param name="sFilePath">
        /// Folder Path to check
        /// </param>
        /// <returns>
        /// The check path length.
        /// </returns>
        public static string CheckPathLength(string sFilePath)
        {
            if (sFilePath.Length > 260)
            {
                string sShortFilePath = sFilePath.Substring(sFilePath.LastIndexOf("\\") + 1);

                sFilePath = Path.Combine(LoadSetting("Download Folder"), sShortFilePath);
            }

            return sFilePath;
        }

        /// <summary>
        /// Delete a Setting
        /// </summary>
        /// <param name="sKey">
        /// Setting Name
        /// </param>
        public static void DeleteSetting(string sKey)
        {
            if (App.Settings[sKey] != null)
            {
                App.Settings.Remove(sKey);
            }

            App.SectionInformation.ForceSave = true;
            Conf.Save(ConfigurationSaveMode.Modified);

            ConfigurationManager.RefreshSection("appSettings");
        }

        /// <summary>
        /// Encrypts a password using MD5.
        ///   not my code in this func., but falls under public domain.
        ///   Author unknown. But Thanks to the author none the less.
        /// </summary>
        /// <param name="sOriginalPass">
        /// The s Original Pass.
        /// </param>
        /// <returns>
        /// The encode password.
        /// </returns>
        public static string EncodePassword(string sOriginalPass)
        {
            // Instantiate MD5CryptoServiceProvider, get bytes for original password and compute hash (encoded password)
            var md5 = new MD5CryptoServiceProvider();

            var originalBytes = Encoding.Default.GetBytes(sOriginalPass);
            var encodedBytes = md5.ComputeHash(originalBytes);

            // Convert encoded bytes back to a 'readable' string
            return BitConverter.ToString(encodedBytes);
        }

        // public static string sURLImg;

        /// <summary>
        /// Attempts to extract hotlinked and thumb-&gt;FullScale images.
        /// </summary>
        /// <param name="strDump">
        /// The str Dump.
        /// </param>
        /// <returns>
        /// The extract attachment images html.
        /// </returns>
        public static List<ImageInfo> ExtractAttachmentImagesHtml(string strDump)
        {
            List<ImageInfo> rtnList = new List<ImageInfo>();
            
            strDump = strDump.Replace("&amp;", "&");

            // use only message content
            var iStart = strDump.IndexOf("<!-- attachments -->");

            if (iStart < 0)
            {
                // Return Empty List
                return rtnList;
            }

            iStart += 21;

            var iEnd = strDump.IndexOf("<!-- / attachments -->");

            if (iEnd > 0)
            {
                strDump = strDump.Substring(iStart, iEnd - iStart);
            }

            ///////////////////////////////////////////////
            rtnList.AddRange(
                ItemFinder.ListAllLinks(strDump).Select(
                    link =>
                    new ImageInfo
                        {
                            ImageUrl = MainForm.userSettings.sForumUrl + ReplaceHexWithAscii(link.Href),
                            ThumbnailUrl = string.Empty
                        }).Where(
                            newPicPoolItem => !IsImageNoneSense(newPicPoolItem.ImageUrl)));

            return rtnList;
        }

        /// <summary>
        /// The extract images html.
        /// </summary>
        /// <param name="strDump">
        /// The str dump.
        /// </param>
        /// <param name="sPostId">
        /// The s post id.
        /// </param>
        /// <returns>
        /// Extracted Images List
        /// </returns>
        public static List<ImageInfo> ExtractImagesHtml(string strDump, string sPostId)
        {
            List<ImageInfo> rtnList;

            if (MainForm.userSettings.sForumUrl.Contains(@"scanlover.com"))
            {
                rtnList = ExtractAttachmentImagesHtml(strDump);
            }
            else if (MainForm.userSettings.sForumUrl.Contains(@"sexyandfunny.com"))
            {
                rtnList = ExtractImagesLinksHtml(strDump, null);

                if (rtnList.Count.Equals(0))
                {
                    rtnList = ExtractAttachmentImagesHtml(strDump);
                }
            }
            else if (MainForm.userSettings.sForumUrl.Contains(@"http://rip") ||
                     MainForm.userSettings.sForumUrl.Contains(@"http://www.rip") ||
                MainForm.userSettings.sForumUrl.Contains(@"kitty-kats.com"))
            {
                rtnList = ExtractImagesLinksHtml(strDump, sPostId);
            }
            else
            {
                rtnList = ExtractImagesLinksHtml(strDump, null);
            }

            return rtnList;
        }

        /// <summary>
        /// Attempts to extract hotlinked and thumb-&gt;FullScale images.
        /// </summary>
        /// <param name="strDump">
        /// The str Dump.
        /// </param>
        /// <param name="sPostId">
        /// The s Post Id.
        /// </param>
        /// <returns>
        /// The extract images links html.
        /// </returns>
        public static List<ImageInfo> ExtractImagesLinksHtml(string strDump, string sPostId)
        {
            if (!string.IsNullOrEmpty(sPostId) && sPostId.StartsWith("http://"))
            {
                sPostId = sPostId.Substring(sPostId.IndexOf("#post") + 5);
            }

            var rtnList = new List<ImageInfo>();

            strDump = strDump.Replace("&amp;", "&");

            // use only message content
            var sMessageStart = "<!-- message -->";
            var sMessageEnd = "<!-- / message -->";

            // If Forum uses VB 4.x or higher
            if (MainForm.userSettings.sForumUrl.Contains(@"http://rip-") ||
                MainForm.userSettings.sForumUrl.Contains(@"http://www.rip-") ||
                MainForm.userSettings.sForumUrl.Contains(@"kitty-kats.com"))
            {
                sMessageStart = string.Format("<div id=\"post_message_{0}\">", sPostId);
                sMessageEnd = "</blockquote>";
            }

            var iStart = strDump.IndexOf(sMessageStart);

            iStart += sMessageStart.Length;

            var iEnd = strDump.IndexOf(sMessageEnd, iStart);

            strDump = strDump.Substring(iStart, iEnd - iStart);

            ///////////////////////////////////////////////

            // Parse all Links <a>
            foreach (var newPicPoolItem in
                ItemFinder.ListAllLinks(strDump).Select(
                    link => new ImageInfo { ImageUrl = ReplaceHexWithAscii(link.Href), ThumbnailUrl = string.Empty }))
            {
                if (newPicPoolItem.ImageUrl.Contains(@"http://rip-productions.net/redirect-to/?redirect="))
                {
                    newPicPoolItem.ImageUrl =
                        HttpUtility.UrlDecode(
                            newPicPoolItem.ImageUrl.Replace(
                                @"http://rip-productions.net/redirect-to/?redirect=", string.Empty));
                }

                if (IsImageNoneSense(newPicPoolItem.ImageUrl))
                {
                    continue;
                }

                rtnList.Add(newPicPoolItem);
            }

            // Parse all Image <a>
            foreach (var newPicPoolItem in
                ItemFinder.ListAllImages(strDump).Select(
                    link => new ImageInfo { ImageUrl = ReplaceHexWithAscii(link.Href), ThumbnailUrl = string.Empty }))
            {
                if (newPicPoolItem.ImageUrl.Contains(@"http://rip-productions.net/redirect-to/?redirect="))
                {
                    newPicPoolItem.ImageUrl =
                        HttpUtility.UrlDecode(
                            newPicPoolItem.ImageUrl.Replace(
                                @"http://rip-productions.net/redirect-to/?redirect=", string.Empty));
                }

                rtnList.Add(newPicPoolItem);
            }

            return rtnList;
        }

        /// <summary>
        /// TODO : Change to regex
        /// Extracts links leading to other threads and postsfor indicies crawling.
        /// </summary>
        /// <param name="strDump">
        /// The str Dump.
        /// </param>
        /// <param name="sUrl">
        /// The s Url.
        /// </param>
        /// <returns>
        /// The extract index urls html.
        /// </returns>
        public static List<ImageInfo> ExtractIndexUrlsHtml(string strDump, string sUrl)
        {
            List<ImageInfo> rtnList = new List<ImageInfo>();

            const string sStartHref = "<a ";
            const string sHref = "href=\"";
            const string sEndHref = "</a>";

            // use only message content
            if (!string.IsNullOrEmpty(sUrl) && sUrl.StartsWith("http://") && sUrl.Contains("#post"))
            {
                sUrl = sUrl.Substring(sUrl.IndexOf("#post") + 5);

                string sMessageStart = string.Format("<div id=\"post_message_{0}\">", sUrl);
                const string sMessageEnd = "</blockquote>";

                int iStart = strDump.IndexOf(sMessageStart);

                iStart += sMessageStart.Length;

                int iEnd = strDump.IndexOf(sMessageEnd, iStart);

                strDump = strDump.Substring(iStart, iEnd - iStart);
            }

            string sCopy = strDump;

            ///////////////////////////////////////////////
            int iStartHref = sCopy.IndexOf(sStartHref);

            if (iStartHref >= 0)
            {
                //////////////////////////////////////////////////////////////////////////

                while (iStartHref >= 0)
                {
                    // Thread.Sleep(1);
                    int iHref = sCopy.IndexOf(sHref, iStartHref);

                    if (!(iHref >= 0))
                    {
                        iStartHref = sCopy.IndexOf(sStartHref, iStartHref + sEndHref.Length);
                        continue;
                    }

                    int iEndHref = sCopy.IndexOf(sEndHref, iHref);

                    if (iEndHref >= 0)
                    {
                        string substring = sCopy.Substring(iHref + sHref.Length, iEndHref - (iHref + sHref.Length));
                        sCopy = sCopy.Remove(iStartHref, iEndHref + sEndHref.Length - iStartHref);

                        iStartHref = substring.IndexOf("\" target=\"_blank\">");

                        if (iStartHref >= 0)
                        {
                            ImageInfo imgInfoIndexLink = new ImageInfo
                                {
                                   ThumbnailUrl = string.Empty, ImageUrl = substring.Substring(0, iStartHref) 
                                };

                            if (imgInfoIndexLink.ImageUrl.Contains(@"showthread.php") ||
                                imgInfoIndexLink.ImageUrl.Contains(@"showpost.php"))
                            {
                                if (imgInfoIndexLink.ImageUrl.Contains("&amp;"))
                                {
                                    imgInfoIndexLink.ImageUrl =
                                        imgInfoIndexLink.ImageUrl.Remove(imgInfoIndexLink.ImageUrl.IndexOf("&amp;"));
                                }

                                rtnList.Add(imgInfoIndexLink);
                            }
                        }
                    }

                    iStartHref = 0;
                    iStartHref = sCopy.IndexOf(sStartHref, iStartHref);
                }

                //////////////////////////////////////////////////////////////////////////
            }

            return rtnList;
        }

        /// <summary>
        /// TODO : Change to Regex
        /// Get Post ids of all Posts
        /// </summary>
        /// <param name="strDump">
        /// The str Dump.
        /// </param>
        /// <returns>
        /// The extract threadto posts html.
        /// </returns>
        public static List<ImageInfo> ExtractThreadtoPostsHtml(string strDump)
        {
            List<ImageInfo> rtnList = new List<ImageInfo>();

            const string Start = "<a name=\"post";

            string sEnd = "\">";

            if (MainForm.userSettings.sForumUrl.Contains(@"http://rip-") ||
                MainForm.userSettings.sForumUrl.Contains(@"http://www.rip-") ||
                MainForm.userSettings.sForumUrl.Contains(@"kitty-kats.com"))
            {
                sEnd = "\" href";
            }

            string sCopy = strDump;

            int iStart = 0;

            iStart = sCopy.IndexOf(Start, iStart);

            while (iStart >= 0)
            {
                int iEnd = sCopy.IndexOf(sEnd, iStart);

                string sPostId = sCopy.Substring(iStart + Start.Length, iEnd - (iStart + Start.Length));

                ImageInfo newThumbPicPool = new ImageInfo { ImageUrl = sPostId };

                // iEnd = 0;
                if (IsNumeric(sPostId) && !string.IsNullOrEmpty(sPostId))
                {
                    rtnList.Add(newThumbPicPool);
                }

                iStart = sCopy.IndexOf(Start, iStart + sEnd.Length);
            }

            return rtnList;
        }

        /// <summary>
        /// This func checks to see if a file already exists at destination
        ///   thats of the same name. If so, it incrementally adds numerical
        ///   values prior to the image extension until the new file path doesn't
        ///   already have a file there.
        /// </summary>
        /// <param name="sPath">
        /// Image path
        /// </param>
        /// <returns>
        /// The get suitable name.
        /// </returns>
        public static string GetSuitableName(string sPath)
        {
            string newAlteredPath = sPath;
            int iRenameCnt = 1;
            string sbegining = newAlteredPath.Substring(0, newAlteredPath.LastIndexOf("."));
            string sEnd = newAlteredPath.Substring(newAlteredPath.LastIndexOf("."));

            while (File.Exists(newAlteredPath))
            {
                newAlteredPath = sbegining + "_" + iRenameCnt + sEnd;
                iRenameCnt++;
            }

            return newAlteredPath;
        }

        /// <summary>
        /// Check if Input is a Numeric Value (Numbers)
        /// </summary>
        /// <param name="valueToCheck">
        /// </param>
        /// <returns>
        /// The is numeric.
        /// </returns>
        public static bool IsNumeric(object valueToCheck)
        {
            double dummy;
            string inputValue = Convert.ToString(valueToCheck);

            bool numeric = double.TryParse(inputValue, NumberStyles.Any, null, out dummy);

            return numeric;
        }

        /// <summary>
        /// Loads a Setting from the App.config
        /// </summary>
        /// <param name="sKey">
        /// Setting name
        /// </param>
        /// <returns>
        /// Setting value
        /// </returns>
        public static string LoadSetting(string sKey)
        {
            string setting = App.Settings[sKey].Value;

            return setting;
        }

        /// <summary>
        /// It's essential to give files legal names. Otherwise the Win32API 
        ///   sends back a bucket full of cow dung.
        /// </summary>
        /// <param name="sString">
        /// String to check
        /// </param>
        /// <returns>
        /// The remove illegal charecters.
        /// </returns>
        public static string RemoveIllegalCharecters(string sString)
        {
            string sNewComposed = sString;

            sNewComposed = sNewComposed.Replace("\\", string.Empty);
            sNewComposed = sNewComposed.Replace("/", "-");
            sNewComposed = sNewComposed.Replace("*", "+");
            sNewComposed = sNewComposed.Replace("?", string.Empty);
            sNewComposed = sNewComposed.Replace("!", string.Empty);
            sNewComposed = sNewComposed.Replace("\"", "'");
            sNewComposed = sNewComposed.Replace("<", "(");
            sNewComposed = sNewComposed.Replace(">", ")");
            sNewComposed = sNewComposed.Replace("|", "!");
            sNewComposed = sNewComposed.Replace(":", ";");
            sNewComposed = sNewComposed.Replace("&amp;", "&");
            sNewComposed = sNewComposed.Replace("&quot;", "''");
            sNewComposed = sNewComposed.Replace("&apos;", "'");
            sNewComposed = sNewComposed.Replace("&lt;", string.Empty);
            sNewComposed = sNewComposed.Replace("&gt;", string.Empty);
            sNewComposed = sNewComposed.Replace("�", "e");
            sNewComposed = sNewComposed.Replace("\t", string.Empty);

            // sNewComposed = newComposed.Replace("@", "-");
            sNewComposed = sNewComposed.Replace("\r", string.Empty);
            sNewComposed = sNewComposed.Replace("\n", string.Empty);

            return sNewComposed;
        }

        /// <summary>
        /// Although these are not hex, but rather html codes for special characters
        /// </summary>
        /// <param name="sURL">
        /// String to check
        /// </param>
        /// <returns>
        /// The replace hex with ascii.
        /// </returns>
        public static string ReplaceHexWithAscii(string sURL)
        {
            string sString = sURL;

            if (sString == null)
            {
                return string.Empty;
            }

            sString = sString.Replace("&amp;", "&");
            sString = sString.Replace("&quot;", "''");
            sString = sString.Replace("&lt;", string.Empty);
            sString = sString.Replace("&gt;", string.Empty);
            sString = sString.Replace("�", "e");
            sString = sString.Replace("\t", string.Empty);

            // sString = sString.Replace("@", "-");
            return sString;
        }

        /// <summary>
        /// Save all Jobs, and the current one which causes the crash to a CrashLog_...txt
        /// </summary>
        /// <param name="sExMessage">
        /// Exception Message
        /// </param>
        /// <param name="sStackTrace">
        /// Exception Stack Trace
        /// </param>
        /// <param name="mCurrentJob">
        /// Current Download Job
        /// </param>
        public static void SaveOnCrash(string sExMessage, string sStackTrace, JobInfo mCurrentJob)
        {
            const string ErrMessage =
                "An application error occurred. Please contact Admin (http://ripper.watchersnet.de/Feedback.aspx) " +
                "with the following information:";

            // Save Current Job and the Error to txt file
            string sFile = string.Format(
                "Crash_{0}.txt", 
                DateTime.Now.ToString().Replace("/", string.Empty).Replace(":", string.Empty).Replace(".", string.Empty)
                    .Replace(" ", "_"));

            // Save Current Job and the Error to txt file
            FileStream file = new FileStream(Path.Combine(Application.StartupPath, sFile), FileMode.CreateNew);
            StreamWriter sw = new StreamWriter(file);
            sw.WriteLine(ErrMessage);
            sw.Write(sw.NewLine);
            sw.Write(sExMessage);
            sw.Write(sw.NewLine);
            sw.Write(sw.NewLine);
            sw.WriteLine("Stack Trace:");
            sw.Write(sw.NewLine);
            sw.Write(sStackTrace);
            sw.Write(sw.NewLine);
            sw.Write(sw.NewLine);

            if (mCurrentJob != null)
            {
                sw.WriteLine("Current Job DUMP:");
                sw.Write(sw.NewLine);

                sw.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sw.WriteLine(
                    "<ArrayOfJobInfo xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
                sw.WriteLine("  <JobInfo>");
                sw.WriteLine("    <sStorePath>{0}</sStorePath>", mCurrentJob.StorePath);
                sw.WriteLine("    <sTitle>{0}</sTitle>", mCurrentJob.Title);
                sw.WriteLine("    <sPostTitle>{0}</sPostTitle>", mCurrentJob.PostTitle);
                sw.WriteLine("    <sForumTitle>{0}</sForumTitle>", mCurrentJob.ForumTitle);
                sw.WriteLine("    <sURL>{0}</sURL>", mCurrentJob.URL);
                sw.WriteLine("    <sXMLPayLoad>{0}</sXMLPayLoad>", mCurrentJob.HtmlPayLoad);
                sw.WriteLine("    <sImageCount>{0}</sImageCount>", mCurrentJob.ImageCount);
                sw.WriteLine("  </JobInfo>");
                sw.WriteLine("</ArrayOfJobInfo>");
            }

            sw.Close();
            file.Close();
        }

        /// <summary>
        /// Saves a setting to the App.config
        /// </summary>
        /// <param name="sKey">
        /// Setting Name
        /// </param>
        /// <param name="sValue">
        /// Setting Value
        /// </param>
        public static void SaveSetting(string sKey, string sValue)
        {
            if (App.Settings[sKey] != null)
            {
                App.Settings.Remove(sKey);
            }

            App.Settings.Add(sKey, sValue);

            App.SectionInformation.ForceSave = true;
            Conf.Save(ConfigurationSaveMode.Modified);

            ConfigurationManager.RefreshSection("appSettings");
        }

        #endregion

        #region Methods

        /// <summary>
        /// This function allows or disallows the inclusion of an image for fetching.
        ///   returning true DISALLOWS the image from inclusion...
        /// </summary>
        /// <param name="szImgPth">
        /// The sz Img Pth.
        /// </param>
        /// <returns>
        /// The is image none sense.
        /// </returns>
        private static bool IsImageNoneSense(string szImgPth)
        {
            return szImgPth.Contains(@"smilies") ||
                   (szImgPth.Contains(@"Smilies") ||
                    (szImgPth.Contains(@"emoticons") ||
                     (szImgPth.Contains(MainForm.userSettings.sForumUrl) && szImgPth.Contains("images/misc") ||
                      szImgPth.Contains(@"applied/buttons"))));
        }

        #endregion
    }
}