﻿/* 01010011 01101000 01101001 01101110 01101111  01000001 01101101 01100001 01101011 01110101 01110011 01100001
 *
 *  Project: Tumblr Tools - Image parser and downloader from Tumblr blog system
 *
 *  Author: Shino Amakusa
 *
 *  Created: 2013
 *
 *  Last Updated: August, 2017
 *
 * 01010011 01101000 01101001 01101110 01101111  01000001 01101101 01100001 01101011 01110101 01110011 01100001 */

using System.Collections.Generic;
using System.Linq;
using Tumblr_Tool.Enums;
using Tumblr_Tool.Helpers;
using Tumblr_Tool.Objects;
using Tumblr_Tool.Objects.Tumblr_Objects;

namespace Tumblr_Tool.Managers
{
    public class PhotoPostParseManager
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="blog"></param>
        /// <param name="saveLocation"></param>
        /// <param name="generateLog"></param>
        /// <param name="parseSets"></param>
        /// <param name="parseJpeg"></param>
        /// <param name="parsePng"></param>
        /// <param name="parseGif"></param>
        /// <param name="imageSize"></param>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <param name="apiVersion"></param>
        public PhotoPostParseManager(TumblrBlog blog = null, string saveLocation = null, bool generateLog = false, bool parseSets = true,
            bool parseJpeg = true, bool parsePng = true, bool parseGif = true, ImageSize imageSize = ImageSize.None, int offset = 0, int limit = 0, TumblrApiVersion apiVersion = TumblrApiVersion.V2Json)
        {
            if (blog != null)
            {
                TumblrUrl = WebHelper.RemoveTrailingBackslash(blog.Url);
                TumblrDomain = WebHelper.GetDomainName(blog.Url);
            }

            GenerateLog = generateLog;

            ApiQueryOffset = offset;
            SaveLocation = saveLocation;

            ApiQueryPostLimit = limit;

            ErrorList = new HashSet<string>();

            Blog = blog;
            ProcessingStatusCode = ProcessingCode.Ok;
            ParsePhotoSets = parseSets;
            ParseJpeg = parseJpeg;
            ParsePng = parsePng;
            ParseGif = parseGif;
            ImageSize = imageSize;
            ApiVersion = apiVersion;

            Blog?.Posts?.Clear();
            TotalNumberOfPosts = 0;
            ImageList = new HashSet<PhotoPostImage>();
            TotalNumberOfImages = 0;
            DocumentManager = new RemoteDocumentManager()
            {
                ApiVersion = apiVersion
            };
            ImageCommentsList = new Dictionary<string, string>();
        }

        public TumblrApiVersion ApiVersion { get; set; }
        public TumblrBlog Blog { get; set; }
        public HashSet<PhotoPostImage> ImageList { get; set; }
        public ImageSize ImageSize { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsLogUpdated { get; set; }
        public int NumberOfParsedPosts { get; set; }
        public int PercentComplete { get; set; }
        public ProcessingCode ProcessingStatusCode { get; set; }
        public int TotalNumberOfPosts { get; set; }
        public string TumblrDomain { get; set; }
        public SaveFile TumblrPostLog { get; set; }

        private int ApiQueryOffset { get; set; }
        private int ApiQueryPostLimit { get; set; }
        private RemoteDocumentManager DocumentManager { get; set; }
        private HashSet<string> ErrorList { get; set; }
        private HashSet<TumblrPost> ExistingHash { get; set; }
        //private HashSet<string> ExistingImageList { get; set; }
        private bool GenerateLog { get; set; }
        private Dictionary<string, string> ImageCommentsList { get; set; }
        private bool ParseGif { get; set; }
        private bool ParseJpeg { get; set; }
        private bool ParsePhotoSets { get; set; }
        private bool ParsePng { get; set; }
        private HashSet<TumblrPost> Posts { get; set; }
        private string SaveLocation { get; set; }
        private int TotalNumberOfImages { get; set; }
        private string TumblrUrl { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool GetTumblrBlogInfo()
        {
            try
            {
                return DocumentManager.GetRemoteBlogInfo(TumblrApiHelper.GeneratePostTypeQueryUrl(TumblrDomain, TumblrPostType.Photo, 0, 1), Blog);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="parseMode"></param>
        /// <returns></returns>
        public TumblrBlog ParseAllBlogPhotoPosts(BlogPostsScanMode parseMode)
        {
            try
            {
                ProcessingStatusCode = ProcessingCode.Crawling;
                //ExistingImageList = FileHelper.GenerateFolderImageList(SaveLocation);

                Blog.Posts = Blog.Posts ?? new HashSet<TumblrPost>();

                int numPostsPerDocument = (int)NumberOfPostsPerApiDocument.ApiV2;

                TotalNumberOfPosts = (TotalNumberOfPosts == 0) ? Blog.TotalPosts : TotalNumberOfPosts;

                bool finished = false;

                PercentComplete = 0;

                while (ApiQueryOffset < TotalNumberOfPosts && !IsCancelled && !finished)
                {
                    ParseBlogPhotoPosts(parseMode, ApiQueryOffset);

                    if (parseMode == BlogPostsScanMode.NewestPostsOnly && ExistingHash.Count > 0) finished = true;

                    NumberOfParsedPosts += (parseMode == BlogPostsScanMode.FullBlogRescan || Posts.Count == 0) ? numPostsPerDocument : Posts.Count;

                    NumberOfParsedPosts = (NumberOfParsedPosts > TotalNumberOfPosts) ? NumberOfParsedPosts : NumberOfParsedPosts;

                    PercentComplete = TotalNumberOfPosts > 0 ? (int)((NumberOfParsedPosts / (double)TotalNumberOfPosts) * 100.00) : 0;
                    ApiQueryOffset += numPostsPerDocument;

                    if (GenerateLog) UpdateLogFile(Blog.Name);

                    Blog.Posts = new HashSet<TumblrPost>();
                }

                TotalNumberOfImages = ImageList.Count;

                if (ImageList.Count == 0) Blog.Posts = new HashSet<TumblrPost>();

                return Blog;
            }
            catch
            {
                return Blog;
            }
        }

        private void ParseBlogPhotoPosts(BlogPostsScanMode parseMode, int offset)
        {
            Posts = GetTumblrPhotoPostList(ApiQueryOffset);
            ExistingHash = new HashSet<TumblrPost>((from p in Posts
                                                    where FileHelper.FileExists(SaveLocation, p.Photos.Last().Filename)
                                                    select p));
            Posts.RemoveWhere(x => ExistingHash.Contains(x));

            if (Posts.Count != 0) { Blog.Posts.UnionWith(Posts); GenerateImageListForDownload(Posts); }

            if (parseMode == BlogPostsScanMode.FullBlogRescan) Blog.Posts.UnionWith(ExistingHash);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool TumblrExists()
        {
            try
            {
                return TumblrApiHelper.GeneratePostTypeQueryUrl(TumblrDomain, TumblrPostType.All, 0, 1).TumblrExists();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="posts"></param>
        private void GenerateImageListForDownload(HashSet<TumblrPost> posts)
        {
            try
            {
                foreach (var tumblrPost in posts)
                {
                    var post = (PhotoPost)tumblrPost;
                    if (!ParsePhotoSets && post.Photos.Count > 1)
                    {
                        // do not parse images from photoset
                    }
                    else
                    {
                        foreach (PhotoPostImage image in post.Photos)
                        {
                            try
                            {
                                ImageList.Add(image);
                            }
                            catch
                            {
                                return;
                            }
                        }
                    }
                }

                if (ImageList.Count > 0)
                {
                    HashSet<PhotoPostImage> removeHash = new HashSet<PhotoPostImage>();

                    if (!ParseGif)
                    {
                        removeHash.UnionWith(new HashSet<PhotoPostImage>((from p in ImageList where p.Filename.ToLower().EndsWith(".gif") select p)));
                    }

                    if (!ParseJpeg)
                    {
                        removeHash.UnionWith(new HashSet<PhotoPostImage>((from p in ImageList where p.Filename.ToLower().EndsWith(".jpg") || p.Filename.ToLower().EndsWith(".jpeg") select p)));
                    }

                    if (!ParsePng)
                    {
                        removeHash.UnionWith(new HashSet<PhotoPostImage>((from p in ImageList where p.Filename.ToLower().EndsWith(".png") select p)));
                    }

                    ImageList.RemoveWhere(x => removeHash.Contains(x));
                }
            }
            catch
            {
                //ignored
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private HashSet<TumblrPost> GetTumblrPhotoPostList(int offset = 0)
        {
            try
            {
                var query = TumblrApiHelper.GeneratePostTypeQueryUrl(TumblrDomain, TumblrPostType.Photo, offset);

                DocumentManager.GetRemoteDocument(query);

                if ((ApiVersion == TumblrApiVersion.V2Json && DocumentManager.JsonDocument != null))
                {
                    DocumentManager.ImageSize = ImageSize;
                    HashSet<TumblrPost> posts = DocumentManager.GetPostListFromDoc(TumblrPostType.Photo);
                    return posts;
                }
                ProcessingStatusCode = ProcessingCode.UnableDownload;
                return new HashSet<TumblrPost>();
            }
            catch
            {
                return new HashSet<TumblrPost>();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="logFileName"></param>
        private void UpdateLogFile(string logFileName)
        {
            try
            {
                if (TumblrPostLog == null || TumblrPostLog.Blog.Name != logFileName)
                {
                    TumblrPostLog = new SaveFile(string.Concat(logFileName, ".log"), Blog);
                }

                foreach (TumblrPost post in Blog.Posts)
                {
                    TumblrPostLog.Blog.Posts.RemoveWhere(p => p.Id == post.Id);

                    TumblrPostLog.Blog.Posts.Add(post);
                    IsLogUpdated = true;
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}