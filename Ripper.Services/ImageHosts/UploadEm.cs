//////////////////////////////////////////////////////////////////////////
// Code Named: VG-Ripper
// Function  : Extracts Images posted on RiP forums and attempts to fetch
//			   them to disk.
//
// This software is licensed under the MIT license. See license.txt for
// details.
// 
// Copyright (c) The Watcher
// Partial Rights Reserved.
// 
//////////////////////////////////////////////////////////////////////////
// This file is part of the RiP Ripper project base.

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Threading;

namespace Ripper
{
    using Ripper.Core.Components;
    using Ripper.Core.Objects;

    /// <summary>
    /// Worker class to get images hosted on UploadEm.com
    /// </summary>
    public class UploadEm : ServiceTemplate
    {
        public UploadEm(ref string sSavePath, ref string strURL, ref string thumbURL, ref string imageName, ref int imageNumber, ref Hashtable hashtable)
            : base(sSavePath, strURL, thumbURL, imageName, imageNumber, ref hashtable)
        {
            //
            // Add constructor logic here
            //
        }


        protected override bool DoDownload()
        {
            string strImgURL = ImageLinkURL;

            if (EventTable.ContainsKey(strImgURL))
            {
                return true;
            }

            try
            {
                if (!Directory.Exists(SavePath))
                    Directory.CreateDirectory(SavePath);
            }
            catch (IOException ex)
            {
                //MainForm.DeleteMessage = ex.Message;
                //MainForm.Delete = true;

                return false;
            }
            
            CacheObject CCObj = new CacheObject();
            CCObj.IsDownloaded = false;
            //CCObj.FilePath = strFilePath;
            CCObj.Url = strImgURL;
            try
            {
                EventTable.Add(strImgURL, CCObj);
            }
            catch (ThreadAbortException)
            {
                return true;
            }
            catch (Exception)
            {
                if (EventTable.ContainsKey(strImgURL))
                {
                    return false;
                }
                else
                {
                    EventTable.Add(strImgURL, CCObj);
                }
            }

            string strIVPage = GetImageHostPage(ref strImgURL);

            if (strIVPage.Length < 10)
            {
               
                return false;
            }

            string strNewURL = strImgURL.Substring(0, strImgURL.IndexOf("/", 8) + 1);

            int iStartSRC = 0;
            int iEndSRC = 0;

            iStartSRC = strIVPage.IndexOf("Note: Large images are scaled to fit.  <a href=\"");

            if (iStartSRC < 0)
            {
                return false;
            }

            iStartSRC += 48;

            iEndSRC = strIVPage.IndexOf("\" class=\"small\">Click Here to view full size.</a> </p>", iStartSRC);

            if (iEndSRC < 0)
            {
                return false;
            }

            strNewURL = strIVPage.Substring(iStartSRC, iEndSRC - iStartSRC);


            string strFilePath = strNewURL.Substring(strNewURL.LastIndexOf("/") + 1);

            strFilePath = Path.Combine(SavePath, Utility.RemoveIllegalCharecters(strFilePath));

            CCObj.FilePath = strFilePath;

            //////////////////////////////////////////////////////////////////////////

            string NewAlteredPath = Utility.GetSuitableName(strFilePath);
            if (strFilePath != NewAlteredPath)
            {
                strFilePath = NewAlteredPath;
                ((CacheObject)EventTable[ImageLinkURL]).FilePath = strFilePath;
            }

            try
            {
                WebClient client = new WebClient();
                client.Headers.Add("Referer: " + strImgURL);
                client.Headers.Add("User-Agent: Mozilla/5.0 (Windows; U; Windows NT 5.2; en-US; rv:1.7.10) Gecko/20050716 Firefox/1.0.6");
                client.DownloadFile(strNewURL, strFilePath);
                client.Dispose();
            }
            catch (ThreadAbortException)
            {
                ((CacheObject)EventTable[strImgURL]).IsDownloaded = false;
                ThreadManager.GetInstance().RemoveThreadbyId(ImageLinkURL);

                return true;
            }
            catch (IOException ex)
            {
                //MainForm.DeleteMessage = ex.Message;
                //MainForm.Delete = true;

                ((CacheObject)EventTable[strImgURL]).IsDownloaded = false;
                ThreadManager.GetInstance().RemoveThreadbyId(ImageLinkURL);

                return true;
            }
            catch (WebException)
            {
                ((CacheObject)EventTable[strImgURL]).IsDownloaded = false;
                ThreadManager.GetInstance().RemoveThreadbyId(ImageLinkURL);

                return false;
            }

            ((CacheObject)EventTable[ImageLinkURL]).IsDownloaded = true;
            //CacheController.GetInstance().u_s_LastPic = ((CacheObject)eventTable[mstrURL]).FilePath;
            CacheController.Instance().LastPic =((CacheObject)EventTable[ImageLinkURL]).FilePath = strFilePath;

            return true;
        }

        //////////////////////////////////////////////////////////////////////////

        
    }
}
