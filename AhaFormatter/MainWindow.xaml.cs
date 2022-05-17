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

namespace DoresoFormatter
{
    public partial class MainWindow : Window
    {
        readonly string idle = new("Awaiting input");
        string csvPath = new("");
        string outputPath = new("");
        YoutubeAPI api;

        public MainWindow()
        {
            InitializeComponent();
            Console.Text = idle;
            api = new();
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
            List<String> results = new();
            using (var reader = new StreamReader(csvPath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<Song>();
                foreach (var record in records)
                {
                    string youtubeUrl = await GetYoutubeURLAsync(record.Detail_URL);
                    results.Add(ParsedSongText(record.Artists, youtubeUrl, record.Title));
                    Console.AppendText(Environment.NewLine + ParsedSongText(record.Artists, youtubeUrl, record.Title));

                }
            }
            string fileName = "DoresoParse - " + DateTime.Now.ToString("yyyy-dd-M HH-mm-ss") + ".txt";
            if (!File.Exists(outputPath))
            {
                using StreamWriter stream = File.CreateText(outputPath + "\\" + fileName);
                foreach (string result in results)
                {
                    await stream.WriteLineAsync(result);
                }
                Console.AppendText(Environment.NewLine + "Successfully wrote " + results.Count + " songs to " + fileName + " from CSV " + csvPath);
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

        private void pseudoConsole_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Console.ScrollToEnd();
        }

        private async Task<string> GetYoutubeURLAsync(string? link)
        {
            Console.AppendText("Getting Doreso youtube link and transforming");
            string youtubeURL = "";
            HttpClient cli = new(new HttpClientHandler() { UseDefaultCredentials = true });
            HttpResponseMessage response = await cli.GetAsync(link);
            var data = await response.Content.ReadAsStreamAsync();
            string html = String.Empty;
            using (StreamReader sr = new StreamReader(data))
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
    }
}
