using System.Collections.Generic;
using System.Threading.Tasks;

namespace YouTubeSync.Interfaces
{
    public interface IYouTubeDownloader
    {
        Task DownloadVideoAsync(string videoUrl, string outputDirectory);
        Task DownloadVideosAsync(IEnumerable<string> videoUrls, string outputDirectory);
    }
}
