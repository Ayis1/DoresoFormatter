using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System.Threading;
using Google;

namespace DoresoFormatter
{
    public partial class MainWindow : Window
    {
        readonly string idle = new("Awaiting input");
        string csvPath = new("");
        string outputPath = new("");
        string playlistTitle = new("");
        List<String> results;       

        public MainWindow()
        {
            InitializeComponent();
            Console.Text = idle;
            results = new();
        }

        private void SelectFileClick(object sender, RoutedEventArgs e)
        {
            csvPath = "";
            PreParseConsoleInfo("selecting path");
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "";
            dialog.DefaultExt = ".csv";
            dialog.Filter = "csv files (.csv)|*.csv";

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                csvPath = dialog.FileName;
            }
            PreParseConsoleInfo("Path selected at: " + csvPath);
            FilenameText.Text = csvPath;
        }

        private void SelectOutputFolder(object sender, RoutedEventArgs e)
        {
            outputPath = "";
            PreParseConsoleInfo("Selecting output desitnation");
            using var dialog = new FolderBrowserDialog
            {
                Description = "Time to select a folder",
                UseDescriptionForTitle = true,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + Path.DirectorySeparatorChar,
                ShowNewFolderButton = true
            };
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                OutputText.Text = dialog.SelectedPath;
                outputPath = dialog.SelectedPath;
                PreParseConsoleInfo("Output folder selected at: " + outputPath);
            }
        }

        private async void ParseCSV(object sender, RoutedEventArgs e)
        {
            Console.AppendText(Environment.NewLine);
            using (var reader = new StreamReader(csvPath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                IEnumerable<Song> records = csv.GetRecords<Song>();
                foreach (var record in records)
                {
                    string youtubeUrl = await GetYoutubeURLAsync(record.Detail_URL);
                    results.Add(ParsedSongText(record.Artists, youtubeUrl, record.Title));
                    Console.AppendText(Environment.NewLine + ParsedSongText(record.Artists, youtubeUrl, record.Title));

                }
            }
            string fileName = "DoresoParse - " + DateTime.Now.ToString("yyyy-dd-M HH-mm-ss") + ".txt";
            playlistTitle = fileName.Replace(".txt", "");
            if (!File.Exists(outputPath))
            {
                using StreamWriter stream = File.CreateText(outputPath + "\\" + fileName);
                foreach (string result in results)
                {
                    await stream.WriteLineAsync(result);
                }
                Console.AppendText(Environment.NewLine + "Successfully wrote " + results.Count + " songs to " + fileName + " from CSV " + csvPath + 
                    Environment.NewLine + "Create new playlist available");
                CreatePlaylist.IsEnabled = true;
            }
        }

        private static string ParsedSongText(string? artist, string? url, string? title)
        {
            return "Arist: " + artist + Environment.NewLine + "Title: " + title + Environment.NewLine + "Link: " + url + Environment.NewLine + Environment.NewLine;
        }

        private void PreParseConsoleInfo(string entry)
        {
            Console.AppendText(Environment.NewLine + entry);

            if (csvPath != "" && outputPath != "")
            {
                Console.AppendText(Environment.NewLine + "Ready to parse CSV");
                ParseCSVBtn.IsEnabled = true;
            }
        }

        private void ClearConsole(object sender, RoutedEventArgs e)
        {
            Console.Clear();
            Console.Text = idle;
        }

        private void Console_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Console.ScrollToEnd();
        }

        private async Task<string> GetYoutubeURLAsync(string? link)
        {
            Console.AppendText(Environment.NewLine + "Getting Doreso youtube link and transforming");
            string youtubeURL = "";
            HttpClient cli = new(new HttpClientHandler() { UseDefaultCredentials = true });
            HttpResponseMessage response = await cli.GetAsync(link);
            var data = await response.Content.ReadAsStreamAsync();
            string html = String.Empty;
            using (StreamReader sr = new(data))
            {
                html = sr.ReadToEnd();
            }

            // RegEx to find youtube URL from site.
            // Example format is https://www.youtube.com/embed/wAVEWJTJdIA
            // Transform substring into clickable link. replace /embed/ with /watch?v=

            Regex reg = new(@"youtube.com/embed/[A-Za-z0-9\-_]{11}");
            if (reg.IsMatch(html))
            {
                string url = reg.Match(html).Value;
                youtubeURL = url.Replace("/embed/", "/watch?v=");
            }
            return youtubeURL;
        }

        private async void CreatePlaylist_Click(object sender, RoutedEventArgs e)
        {
            Console.AppendText(Environment.NewLine + "Creating playlist " + playlistTitle);
            try
            {
                await CreateNewPlaylist(results, playlistTitle);
            }
            catch (AggregateException ex)
            {
                Console.AppendText( Environment.NewLine + "Playlist creation failed.");
                foreach (var error in ex.InnerExceptions)
                {
                    Console.AppendText(Environment.NewLine + "Error: " + error.Message);
                }
            }
        }

        public async Task CreateNewPlaylist(List<string> playlist, string playlistTitle)
        {
            UserCredential credential;
            using (var stream = new FileStream(@"C:\Users\insrt\source\repos\AhaFormatter\AhaFormatter\client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                //Will throw null reference exception if client_secrets.json path is not correct.
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    new[] { YouTubeService.Scope.Youtube },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(this.GetType().ToString())
                );
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = this.GetType().ToString()
            });

            Playlist newPlaylist = new();
            newPlaylist.Snippet = new PlaylistSnippet();
            newPlaylist.Snippet.Title = playlistTitle;
            newPlaylist.Snippet.Description = "A playlist created with my DoresoFormatter";
            newPlaylist.Status = new PlaylistStatus();
            newPlaylist.Status.PrivacyStatus = "private";
            newPlaylist = await youtubeService.Playlists.Insert(newPlaylist, "snippet,status").ExecuteAsync();

            Console.AppendText(Environment.NewLine + "Created new playlist " + playlistTitle);

            foreach (string song in playlist)
            {
                Regex reg = new(@"youtube.com/watch\?v=[A-Za-z0-9\-_]{11}");
                if (reg.IsMatch(song))
                {
                    try
                    {
                        string videoId = reg.Match(song).Value;
                        var idOnly = videoId.Replace("youtube.com/watch?v=", "");
                        PlaylistItem songItem = new();
                        songItem.Snippet = new PlaylistItemSnippet();
                        songItem.Snippet.PlaylistId = newPlaylist.Id;
                        songItem.Snippet.ResourceId = new ResourceId();
                        songItem.Snippet.ResourceId.Kind = "youtube#video";
                        songItem.Snippet.ResourceId.VideoId = idOnly;
                        songItem = await youtubeService.PlaylistItems.Insert(songItem, "snippet").ExecuteAsync();
                        Console.AppendText(Environment.NewLine + "Added " + songItem.Snippet.Title + " to " + playlistTitle);
                    }
                    catch (GoogleApiException ex)
                    {
                        Console.AppendText(Environment.NewLine + "error: " + ex.Message);
                    }
                }
            }
            Console.AppendText(Environment.NewLine + "Finished creating playlist.");
        }
    }
}
