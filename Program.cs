using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YouTubeSync.Services;

class Program
{
    static async Task Main(string[] args)
    {
        var downloader = new YouTubeDownloader();

        var videoUrls = new List<string>
        {
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            "https://www.youtube.com/watch?v=BBJa32lCaaY",
            "https://youtu.be/ZFk4xyS6hFY?si=i2k0Au0xeylyg38w"
        };

        string outputDirectory = "Downloads";

        Console.WriteLine("Starting download and processing...");
        var processingTasks = new List<Task>();

        foreach (var videoUrl in videoUrls)
        {
            processingTasks.Add(downloader.DownloadVideoAsync(videoUrl, outputDirectory));
        }

        await Task.WhenAll(processingTasks);

        Console.WriteLine("All downloads and processing completed.");
    }
}
