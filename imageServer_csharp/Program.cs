using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using imageServer_csharp.Drive;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;

namespace imageServer_csharp;

class Program
{
    private static TcpListener listener;
    private static readonly string WebServerPath = @"Webserver";

    static void Main(string[] args)
    {
        using (StreamReader r = new StreamReader("server_conf.json"))
        {
            string json = r.ReadToEnd();
            dynamic array = JsonConvert.DeserializeObject(json);
            string host = array.hostname;
            int port = array.port;
            listener = new TcpListener(IPAddress.Parse(host), port);
            Console.WriteLine($"Webserver started on {array.hostname}:{array.port}");
            listener.Start();
        }
        Thread th = new (new ThreadStart(StartListen));
        th.Start();
        Thread th2 = new (new ThreadStart(Watcher));
        th2.Start();
        Task task = Task.Run((Action)DriveAccess.Worker);
    }

    private static void Watcher()
    {
        while (true)
        {
            Console.WriteLine($"{DateTime.Now:MM/dd/yyyy HH:mm:ss} I'm alive");
            Thread.Sleep(5000);
        }
    }

    private static void StartListen()
    {
        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            NetworkStream stream = client.GetStream();
            byte[] requestBytes = new byte[1024];
            int bytesRead = stream.Read(requestBytes, 0, requestBytes.Length);

            Stopwatch sw = Stopwatch.StartNew();
            string request = Encoding.UTF8.GetString(requestBytes, 0, bytesRead);
            if (string.IsNullOrEmpty(request))
            {
                Console.WriteLine("Empty request");
                SendHeaders("", 400, "Bad Request", "", "", 0, ref stream);
                client.Close();
                continue;
            }
            var requestHeaders = ParseHeaders(request);

            string[] requestFirstLine = requestHeaders.requestType.Split(" ");
            var requestedPath = requestFirstLine[1];
            if (requestedPath == "/")
            {
                requestedPath = "default.html";
            }
            Console.WriteLine($"PATH: {requestedPath}");
            string httpVersion = requestFirstLine.LastOrDefault();
            string contentType = requestHeaders.headers.GetValueOrDefault("Accept");
            string contentEncoding = requestHeaders.headers.GetValueOrDefault("Acept-Encoding");
            (byte[]? content, string responseContentType) = GetContent(requestedPath).Result;
            try
            {
                if (content is not null)
                {
                    SendHeaders(httpVersion, 200, "OK", responseContentType, contentEncoding, 0, ref stream);
                    stream.Write(content);
                    sw.Stop();
                    Console.WriteLine($"Took {sw.ElapsedMilliseconds}");
                }
                else
                {
                    SendHeaders(httpVersion, 404, "Page Not Found", contentType, contentEncoding, 0, ref stream);
                }

                if (requestFirstLine[0] != "GET")
                {
                    SendHeaders(httpVersion, 405, "Method Not Allowed", contentType, contentEncoding, 0, ref stream);
                    //DUPA
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            client.Close();
        }
    }
    private static (Dictionary<string, string> headers, string requestType) ParseHeaders(string headerString)
    {
        var headerLines = headerString.Split('\r', '\n');
        string firstLine = headerLines[0];
        var headerValues = new Dictionary<string, string>();
        foreach (var headerLine in headerLines)
        {
            var headerDetail = headerLine.Trim();
            var delimiterIndex = headerLine.IndexOf(':');
            if (delimiterIndex >= 0)
            {
                var headerName = headerLine.Substring(0, delimiterIndex).Trim();
                var headerValue = headerLine.Substring(delimiterIndex + 1).Trim();
                headerValues.Add(headerName, headerValue);
            }
        }
        return (headerValues, firstLine);
    }
    private static void SendHeaders(string? httpVersion, int statusCode, string statusMsg, string? contentType, string? contentEncoding, int byteLength, ref NetworkStream networkStream)
    {
        string responseHeaderBuffer = "";

        responseHeaderBuffer = $"HTTP/1.1 {statusCode} {statusMsg}\r\n" +
            $"Connection: Keep-Alive\r\n" +
            $"Date: {DateTime.UtcNow.ToString()}\r\n" +
            $"Content-Encoding: {contentEncoding}\r\n" +
            $"Content-Type: {contentType}\r\n\r\n";

        byte[] responseBytes = Encoding.UTF8.GetBytes(responseHeaderBuffer);
        networkStream.Write(responseBytes, 0, responseBytes.Length);
    }
    private static async Task<(byte[]?, string)> GetContent(string requestedPath)
    {
        string filePath = Path.Join(WebServerPath, requestedPath);
        if (filePath.Contains("image"))
        {
            byte[]? bytes = await DriveAccess.GetNextImage();
            return (bytes, "image");
        }

        if (!File.Exists(filePath)) return (null, "");

        if (filePath.Contains("favicon"))
        {
            byte[] file = System.IO.File.ReadAllBytes(filePath);
            return (file, "image/x-icon");
        }

        else
        {
            byte[] file = System.IO.File.ReadAllBytes(filePath);
            return (file, GetMimeType(filePath));
        }
    }

    private static string GetMimeType(string fileName)
    {
        string mimeType = "application/unknown";
        string ext = System.IO.Path.GetExtension(fileName).ToLower();
        Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
        if (regKey != null && regKey.GetValue("Content Type") != null)
            mimeType = regKey.GetValue("Content Type").ToString();
        return mimeType;
    }
}