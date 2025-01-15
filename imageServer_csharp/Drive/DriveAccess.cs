using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace imageServer_csharp.Drive
{
    public class DriveAccess
    {
        private const string PathToServiceAccountKeyFile = @"Drive/serviceAccountCredentials.json";

        private DriveService GetService()
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

        public async Task<byte[]?> GetFile(string fileId)
        {
            //return System.IO.File.ReadAllBytes("Drive/AnimeHoodies_1hfd6j1.jpeg");
            try
            {
                var service = GetService();
                MemoryStream ms = new();
                var file = service.Files.Get(fileId);
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
    }
}
