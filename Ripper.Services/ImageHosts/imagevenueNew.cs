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
using System.Text.RegularExpressions;
using System.Threading;

namespace Ripper
{
    using Ripper.Core.Components;
    using Ripper.Core.Objects;

    /// <summary>
    /// Worker class to get images hosted on ImageVenue
    /// </summary>
    public class imagevenueNew : ServiceTemplate
    {
        public imagevenueNew(ref string sSavePath, ref string strURL, ref string thumbURL, ref string imageName, ref int imageNumber, ref Hashtable hashtable)
            : base(sSavePath, strURL, thumbURL, imageName, imageNumber, ref hashtable)
        {
            //
            // Add constructor logic here
            //
        }


        protected override bool DoDownload()
        {
            var imageURL = ImageLinkURL;
            string filePath = "";
            var page = this.GetImageHostPage(ref imageURL);
            string imageDownloadURL = "";
            var baseUrl = new Uri(ImageLinkURL).Host;
            var match = Regex.Match(page,
                        @"id=""thepic"" onLoad=""scaleImg\(\);""   SRC=""(?<url>[^\""""]+)"" alt=""(?<name>[^\""""]+)""",
                        RegexOptions.Compiled);

            if (match.Success)
            {
                imageDownloadURL = "http://" + baseUrl + "/" + match.Groups["url"].Value.Replace("&amp;", "&");
                int nameIndex = match.Groups["name"].Value.LastIndexOf("/");
                // TODO: Extension von url holen
                filePath = match.Groups["name"].Value.Substring(nameIndex + 1);
            }
            else
            {

                ((CacheObject)EventTable[imageURL]).IsDownloaded = false;
                return false;
            }


            // Finally Download the Image
            return this.DownloadImageAsync(imageDownloadURL, filePath);
        }

        //////////////////////////////////////////////////////////////////////////

        
    }
}
