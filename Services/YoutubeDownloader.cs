using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YouTubeSync.Interfaces;

namespace YouTubeSync.Services
{
    public class YouTubeDownloader : IYouTubeDownloader
    {
        private readonly YoutubeClient _youtubeClient;

        public YouTubeDownloader()
        {
            _youtubeClient = new YoutubeClient();
        }

        // Download a single video
        public async Task DownloadVideoAsync(string videoUrl, string outputDirectory)
        {
            try
            {
                var videoId = VideoId.Parse(videoUrl);
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);

                // Attempt to get the best muxed stream
                var muxedStream = streamManifest.GetMuxedStreams().TryGetWithHighestVideoQuality();

                if (muxedStream != null)
                {
                    // Muxed stream available - download it directly
                    string filePath = Path.Combine(outputDirectory, $"{videoId}.{muxedStream.Container.Name}");
                    Directory.CreateDirectory(outputDirectory);

                    Console.WriteLine($"Downloading muxed stream: {muxedStream.VideoQuality.Label} / {muxedStream.Container.Name}...");
                    using (var progress = new ConsoleProgress())
                        await _youtubeClient.Videos.Streams.DownloadAsync(muxedStream, filePath, progress);

                    Console.WriteLine($"Download completed: {filePath}");
                    return;
                }

                // No muxed streams available - download audio and video separately
                Console.WriteLine("no muxed streams found, downloading adaptive streams...");

                var videoStream = streamManifest.GetVideoStreams().GetWithHighestVideoQuality();
                var audioStream = streamManifest.GetAudioStreams().GetWithHighestBitrate();

                if (videoStream == null || audioStream == null)
                {
                    Console.WriteLine($"No suitable video or audio streams found for {videoUrl}.");
                    return;
                }

                
                string videoPath = Path.Combine(outputDirectory, $"{videoId}_video.{videoStream.Container.Name}");
                string audioPath = Path.Combine(outputDirectory, $"{videoId}_audio.{audioStream.Container.Name}");
                Directory.CreateDirectory(outputDirectory);

                //download all the streams
                Console.WriteLine($"Downloading video stream: {videoStream.VideoQuality.Label} / {videoStream.Container.Name}...");
                Console.WriteLine($"Downloading audio stream: {audioStream.Bitrate} / {audioStream.Container.Name}...");

                var downloadVideoTask = DownloadStreamAsync(videoStream, videoPath);
                var downloadAudioTask = DownloadStreamAsync(audioStream, audioPath);

                await Task.WhenAll(downloadVideoTask, downloadAudioTask);

                // mix the two streams
                string outputFilePath = Path.Combine(outputDirectory, $"{videoId}_final_output.mp4");
                Console.WriteLine("Combining video and audio into final output...");
                await CombineVideoAndAudioAsync(videoPath, audioPath, outputFilePath);

                Console.WriteLine($"Download and merge completed: {outputFilePath}");

                File.Delete(videoPath);
                File.Delete(audioPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading {videoUrl}: {ex.Message}");
            }
        }

        public async Task DownloadVideosAsync(IEnumerable<string> videoUrls, string outputDirectory)
        {
            var downloadTasks = videoUrls.Select(videoUrl => DownloadVideoAsync(videoUrl, outputDirectory));
            await Task.WhenAll(downloadTasks);
        }

        private async Task DownloadStreamAsync(IStreamInfo streamInfo, string filePath)
        {
            using (var progress = new ConsoleProgress())
                await _youtubeClient.Videos.Streams.DownloadAsync(streamInfo, filePath, progress);
        }

        private async Task CombineVideoAndAudioAsync(string videoPath, string audioPath, string outputPath)
        {
            Console.WriteLine($"Combining video: {videoPath} and audio: {audioPath} into {outputPath}");
            string arguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a copy \"{outputPath}\" -y";

            await RunFFmpegProcessAsync(arguments);

            Console.WriteLine($"Combination complete: {outputPath}");
        }

        private async Task RunFFmpegProcessAsync(string arguments)
        {
            var tcs = new TaskCompletionSource<bool>();

            Console.WriteLine($"Starting FFmpeg with arguments: {arguments}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"FFmpeg error/output: {e.Data}");
                }
            };

            process.Exited += (sender, args) =>
            {
                Console.WriteLine("FFmpeg process exited.");
                tcs.SetResult(true);
                process.Dispose();
            };

            process.Start();
            process.BeginErrorReadLine(); // Start capturing StandardError

            await tcs.Task;

            Console.WriteLine("FFmpeg process completed successfully.");
        }

        // check progress of everything
        public class ConsoleProgress : IProgress<double>, IDisposable
        {
            public void Report(double value)
            {
                Console.Write($"\rProgress: {value:P1}");
            }

            public void Dispose()
            {
                Console.WriteLine();
            }
        }
    }
}
