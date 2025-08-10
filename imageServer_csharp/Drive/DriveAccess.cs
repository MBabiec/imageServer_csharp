using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace imageServer_csharp.Drive
{
    public class DriveAccess
    {
        private const string PathToServiceAccountKeyFile = @"Drive/serviceAccountCredentials.json";
        private static Queue<byte[]> images = new();
        private static List<string> imageIds;
        private static readonly Random rnd = new();

        private static DriveService GetService()
        {
            string credJson = File.ReadAllText(PathToServiceAccountKeyFile);
            GoogleCredential credential1 = GoogleCredential.FromJson(credJson);
            GoogleCredential credential2 = credential1.CreateScoped(DriveService.ScopeConstants.DriveReadonly);
            DriveService service = new(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential2
            });
            return service;
        }

        public static async Task<byte[]> GetNextImage()
        {
            if (images.Count == 0)
            {
                return await GetFile(imageIds[rnd.Next(0, imageIds.Count)]);
            }
            return images.Dequeue();
        }

        private static async Task<byte[]?> GetFile(string fileId)
        {
            //return System.IO.File.ReadAllBytes("Drive/AnimeHoodies_1hfd6j1.jpeg");
            try
            {
                var service = GetService();
                MemoryStream ms = new();
                var file = service.Files.Get(fileId);
                file.MediaDownloader.ProgressChanged += Download_ProgressChanged;
                var res = await file.DownloadAsync(ms);
                if (res.Status.Equals(Google.Apis.Download.DownloadStatus.Completed))
                {
                    return ms.ToArray();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public static async Task WorkerAsync()
        {
            if (imageIds == null)
            {
                imageIds = [];
                var path = @"Drive/data.csv";
                using (TextFieldParser csvParser = new TextFieldParser(path))
                {
                    csvParser.CommentTokens = ["#"];
                    csvParser.SetDelimiters([","]);
                    csvParser.HasFieldsEnclosedInQuotes = true;

                    while (!csvParser.EndOfData)
                    {
                        string[] fields = csvParser.ReadFields();
                        imageIds.Add(fields[0]);
                    }
                }
            }

            while (true)
            {

                if (images.Count < 5)
                {
                    try
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        Console.WriteLine("Requesting image");
                        var im = await GetFile(imageIds[rnd.Next(imageIds.Count)]);
                        Console.WriteLine("Got image, adding to queue");
                        if (im != null)
                        {
                            images.Enqueue(im);
                        }
                        sw.Stop();
                        //Console.WriteLine($"Got new image in {sw.ElapsedMilliseconds}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                else
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            Console.WriteLine("Image worker crashed...");
        }
        static void Download_ProgressChanged(IDownloadProgress progress)
        {
            Console.WriteLine(progress.Status + " " + progress.BytesDownloaded);
        }
    }
}
