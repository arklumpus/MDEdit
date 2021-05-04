/*
    MDEdit - A MarkDown source code editor with syntax highlighting and
    real-time preview.
    Copyright (C) 2021  Giorgio Bianchini
 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, version 3.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MDEdit
{
    /// <summary>
    /// This class holds static methods for downloading image files from remote servers with an application-wide cache.
    /// </summary>
    public static class AsynchronousImageCache
    {
        private const string imageCacheId = "bb5a724e-93f7-431d-87c3-53f31d8da16e";

        private static readonly string imageCacheFolder;

        private static readonly Dictionary<string, string> imageCache;

        private static readonly HashSet<string> fetchingImages;

        private static readonly object cacheLock;

        /// <summary>
        /// An event that is invoked when an image that was requested asynchronously becomes available.
        /// </summary>
        public static event EventHandler<CacheUpdatedEventArgs> CacheUpdated;

        static AsynchronousImageCache()
        {
            imageCacheFolder = Path.Combine(Path.GetTempPath(), imageCacheId);
            Directory.CreateDirectory(imageCacheFolder);
            imageCache = new Dictionary<string, string>();
            fetchingImages = new HashSet<string>();
            cacheLock = new object();
        }

        private static bool exitHandlerSet = false;

        /// <summary>
        /// This method should be invoked at some point before the application exits; it ensures that the image cache folder is cleared when the application is closed.
        /// </summary>
        public static void SetExitEventHandler()
        {
            if (!exitHandlerSet)
            {
                if (Avalonia.Application.Current.ApplicationLifetime is IControlledApplicationLifetime lifetime)
                {
                    lifetime.Exit += (s, e) =>
                    {
                        try
                        {
                            Directory.Delete(imageCacheFolder, true);
                        }
                        catch
                        {
                            try
                            {
                                foreach (string sr in Directory.GetFiles(imageCacheFolder, "*.*"))
                                {
                                    try
                                    {
                                        File.Delete(sr);
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    };
                }
                exitHandlerSet = true;
            }
        }

        /// <summary>
        /// Resolves an image Uri asynchronously. If the image has already been downloaded previously and is available in the cache, its path on disk is returned immediately.
        /// Otherwise, it is queued for download and when it becomes available, the <see cref="CacheUpdated"/> event is invoked. If an image is requested again while it is
        /// being downloaded, a new download of the image is prevented.
        /// </summary>
        /// <param name="imageUri">The Uri of the image to download, either absolute or relative to the <paramref name="baseUriString"/>.</param>
        /// <param name="baseUriString">The base Uri for resolving the <paramref name="imageUri"/>.</param>
        /// <returns>An <see cref="ImageRetrievalResult"/> containing the path to a copy of the image file on disk. If the <see cref="ImageRetrievalResult.ImagePath"/>
        /// property of the return value is <see langword="null"/>, the image was not available in the cache and has been queued for download.</returns>
        public static ImageRetrievalResult ImageUriResolverAsynchronous(string imageUri, string baseUriString)
        {
            bool found;
            string cachedImage;

            lock (cacheLock)
            {
                found = imageCache.TryGetValue(baseUriString + "|||" + imageUri, out cachedImage);
            }

            if (!found)
            {
                bool notAlreadyFetching;

                lock (cacheLock)
                {
                    notAlreadyFetching = fetchingImages.Add(baseUriString + "|||" + imageUri);
                }

                if (notAlreadyFetching)
                {
                    Thread thr = new Thread(() =>
                    {
                        (string imagePath, bool wasDownloaded) = VectSharp.Markdown.HTTPUtils.ResolveImageURI(imageUri, baseUriString);

                        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                        {
                            string id = Guid.NewGuid().ToString();

                            string cachedImage = Path.Combine(imageCacheFolder, id + Path.GetExtension(imagePath));

                            if (wasDownloaded)
                            {
                                if (!Directory.Exists(imageCacheFolder))
                                {
                                    Directory.CreateDirectory(imageCacheFolder);
                                }

                                File.Move(imagePath, cachedImage);
                                Directory.Delete(Path.GetDirectoryName(imagePath));
                            }
                            else
                            {
                                File.Copy(imagePath, cachedImage);
                            }

                            lock (cacheLock)
                            {
                                imageCache[baseUriString + "|||" + imageUri] = cachedImage;
                                fetchingImages.Remove(baseUriString + "|||" + imageUri);
                            }

                            CacheUpdated?.Invoke(null, new CacheUpdatedEventArgs(baseUriString, imageUri));
                        }
                        else
                        {
                            lock (cacheLock)
                            {
                                fetchingImages.Remove(baseUriString + "|||" + imageUri);
                            }
                        }
                    });

                    thr.Start();
                }

                return new ImageRetrievalResult(null, false);
            }
            else
            {
                return new ImageRetrievalResult(cachedImage, false);
            }
        }

        /// <summary>
        /// Resolves an image Uri synchronously. If the image has already been downloaded previously and is available in the cache, its path on disk is returned immediately.
        /// Otherwise, this method blocks until the image is downloaded. If an image is requested by this method while it is being downloaded as a result of a call to
        /// <see cref="ImageUriResolverAsynchronous(string, string)"/>, it is downloaded a second time.
        /// </summary>
        /// <param name="imageUri">The Uri of the image to download, either absolute or relative to the <paramref name="baseUriString"/>.</param>
        /// <param name="baseUriString">The base Uri for resolving the <paramref name="imageUri"/>.</param>
        /// <returns>An <see cref="ImageRetrievalResult"/> containing the path to a copy of the image file on disk. If the <see cref="ImageRetrievalResult.ImagePath"/>
        /// property of the return value is <see langword="null"/>, an error occurred while the image was being accessed (e.g. it was not found on the server).</returns>
        public static ImageRetrievalResult ImageUriResolverSynchronous(string imageUri, string baseUriString)
        {
            lock (cacheLock)
            {
                if (!imageCache.TryGetValue(baseUriString + "|||" + imageUri, out string cachedImage))
                {
                    (string imagePath, bool wasDownloaded) = VectSharp.Markdown.HTTPUtils.ResolveImageURI(imageUri, baseUriString);

                    if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    {
                        string id = Guid.NewGuid().ToString();

                        cachedImage = Path.Combine(imageCacheFolder, id + Path.GetExtension(imagePath));

                        if (wasDownloaded)
                        {
                            if (!Directory.Exists(imageCacheFolder))
                            {
                                Directory.CreateDirectory(imageCacheFolder);
                            }

                            File.Move(imagePath, cachedImage);
                            Directory.Delete(Path.GetDirectoryName(imagePath));
                        }
                        else
                        {
                            File.Copy(imagePath, cachedImage);
                        }

                        imageCache[baseUriString + "|||" + imageUri] = cachedImage;
                    }
                    else
                    {
                        cachedImage = null;
                    }
                }

                return new ImageRetrievalResult(cachedImage, false);
            }
        }
    }

    /// <summary>
    /// Represents the result of an image retrieval request.
    /// </summary>
    public struct ImageRetrievalResult
    {
        /// <summary>
        /// The path to a file on disk containing the image. The file will have an appropriate extension based on the image file.
        /// </summary>
        public string ImagePath { get; }

        /// <summary>
        /// This value is set to <see langword="true"/> if the image file was downloaded from a remote server and saved as a temporary file which may be deleted after the consuming code is done with it.
        /// </summary>
        public bool WasDownloaded { get; }

        /// <summary>
        /// Creates a new <see cref="ImageRetrievalResult"/> object.
        /// </summary>
        /// <param name="imagePath">The path to the file on disk containing the image.</param>
        /// <param name="wasDownloaded">Set to <see langword="true"/> if the image file was downloaded from a remote server and may be deleted after the consuming code is done with it.</param>
        public ImageRetrievalResult(string imagePath, bool wasDownloaded)
        {
            this.ImagePath = imagePath;
            this.WasDownloaded = wasDownloaded;
        }

        /// <summary>
        /// Converts a <see cref="ImageRetrievalResult"/> into a tuple.
        /// </summary>
        /// <param name="result">The <see cref="ImageRetrievalResult"/> to convert.</param>
        /// <returns>
        /// A tuple containing the <see cref="ImagePath"/> and <see cref="WasDownloaded"/> properties of the <paramref name="result"/>.
        /// </returns>
        public static implicit operator (string, bool) (ImageRetrievalResult result)
        {
            return (result.ImagePath, result.WasDownloaded);
        }
    }

    /// <summary>
    /// Event data for the <see cref="AsynchronousImageCache.CacheUpdated"/> event.
    /// </summary>
    public class CacheUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// The base image Uri of the image that has been resolved.
        /// </summary>
        public string BaseImageUri { get; }

        /// <summary>
        /// The Uri of the image that has been resolved (either absolute, or relative to the <see cref="BaseImageUri"/>).
        /// </summary>
        public string ImageUri { get; }

        internal CacheUpdatedEventArgs(string baseImageUri, string imageUri)
        {
            this.BaseImageUri = baseImageUri;
            this.ImageUri = imageUri;
        }
    }
}