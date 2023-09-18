using NReco.VideoConverter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YouGetGUI
{
    public partial class Form1 : Form
    {
        private readonly YoutubeClient youtubeClient = new YoutubeClient();
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            StatusIdicator.Visible = false;
            tabControl1.ImageList = imageList1; // Assign the ImageList to the TabControl
            tabPage1.ImageIndex = 0; // Set the image for tabPage1
            tabPage2.ImageIndex = 1; // Set the image for tabPage2
        }
        private async void downloadButton_Click(object sender, EventArgs e)
        {
            string videoUrl = videoUrlTextBox.Text;
            string outputDirectory = outputDirectoryTextBox.Text;

            try
            {
                var videoInfo = await youtubeClient.Videos.GetAsync(videoUrl);
                var streamInfoSet = await youtubeClient.Videos.Streams.GetManifestAsync(videoInfo.Id);

                var streamInfo = streamInfoSet.GetMuxedStreams().GetWithHighestVideoQuality();

                if (streamInfo != null)
                {
                    // Sanitize the video title to remove invalid characters
                    var sanitizedTitle = SanitizeFileName(videoInfo.Title);
                    var fileExtension = streamInfo.Container.Name;
                    var videoFileName = $"{sanitizedTitle}.{fileExtension}";

                    // Create a custom progress reporter
                    var progressReporter = new Progress<double>(progress =>
                    {
                        // Update the ProgressBar value based on the download progress
                        int progressPercentage = (int)(progress * 100);
                        downloadProgressBar.Value = progressPercentage;
                    });

                    // Download the video with the custom progress reporter
                    await youtubeClient.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(outputDirectory, videoFileName), progress: progressReporter);

                    downloadStatusLabel.Text = $"Download Completed";
                    StatusIdicator.Visible = true;
                    StatusindicatorRed.Visible = false;

                    // Ask the user if they want to convert to MP3
                    if (MessageBox.Show("Do you want to convert the video to MP3?", "Conversion", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        StatusindicatorRed.Visible = true;
                        downloadProgressBar.Value = 20;
                        var mp3FileName = $"{sanitizedTitle}.mp3";
                        var inputFilePath = Path.Combine(outputDirectory, videoFileName);
                        var outputFilePath = Path.Combine(outputDirectory, mp3FileName);
                        downloadProgressBar.Value = 30;
                        var ffMpeg = new FFMpegConverter();
                        downloadProgressBar.Value = 60;
                        ffMpeg.ConvertMedia(inputFilePath, outputFilePath, "mp3");
                        downloadProgressBar.Value = 100;
                        conversionStatusLabel.Text = $"Converted to MP3";
                        StatusIdicator.Visible = true;
                        StatusindicatorRed.Visible = false;
                    }
                }
                else
                {
                    MessageBox.Show("No suitable streams found.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private bool isDragging = false;
        private Point offset;

        private void panelTitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                offset = e.Location;
            }
        }

        private void panelTitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point newLocation = this.PointToScreen(e.Location);
                newLocation.Offset(-offset.X, -offset.Y);
                this.Location = newLocation;
            }
        }

        private void panelTitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }
        private string SanitizeFileName(string fileName)
        {
            var invalidChars = new string(Path.GetInvalidFileNameChars());
            var regex = new Regex($"[{Regex.Escape(invalidChars)}]");
            return regex.Replace(fileName, "_");
        }

        private void panelTitleBar_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        private async void videoUrlTextBox_TextChanged(object sender, EventArgs e)
        {
            string videoUrl = videoUrlTextBox.Text;

            try
            {
                conversionStatusLabel.Text = "Status:";
                TitleLabel.Text = "Title";
                LengthLabel.Text = "Length: Loading..."; // Add this line to show "Loading..." initially

                // Extract the video ID from the YouTube URL
                string videoId = ExtractVideoId(videoUrl);
                StatusIdicator.Visible = false;
                StatusindicatorRed.Visible = true;
                downloadProgressBar.Value = 0;

                // Fetch the video information
                var videoInfo = await youtubeClient.Videos.GetAsync(videoId);

                if (!string.IsNullOrEmpty(videoId))
                {
                    // Construct the thumbnail URL using the video ID
                    var thumbnailUrl = $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";
                    downloadProgressBar.Value = 20;

                    // Fetch the image data with a user agent
                    using (var webClient = new WebClient())
                    {
                        webClient.Headers.Add("User-Agent", "Mozilla/5.0"); // Add a user agent
                        var imageBytes = webClient.DownloadData(thumbnailUrl);
                        // Check if the downloaded data is a valid image
                        if (IsValidImage(imageBytes))
                        {
                            using (var ms = new MemoryStream(imageBytes))
                            {
                                thumbnailPictureBox.Image = Image.FromStream(ms);
                                TitleLabel.Text = videoInfo.Title;
                                LengthLabel.Text = $"Length: {videoInfo.Duration}"; // Display the duration
                                AuthorLabel.Text = $"Author: {videoInfo.Author}";
                                downloadProgressBar.Value = 100;
                                downloadStatusLabel.Text = "Image Downloaded";
                                StatusIdicator.Visible = true;
                                StatusindicatorRed.Visible = false;
                            }
                        }
                        else
                        {
                            // Display a placeholder image or set the PictureBox to null
                            thumbnailPictureBox.Image = Properties.Resources._1486486298_arrow_down_download_downloads_downloading_save_81238; // Use the placeholder image
                            LengthLabel.Text = "Length: N/A"; // display duration
                            AuthorLabel.Text = "Author: N/A";
                        }
                    }
                }
                else
                {
                    // Handle the case where the video ID cannot be extracted
                    Console.WriteLine("Invalid YouTube URL.");
                    downloadStatusLabel.Text = "Invalid URL";
                    LengthLabel.Text = "Length: N/A";
                    AuthorLabel.Text = "Author: N/A";
                    thumbnailPictureBox.Image = null; // Clear the PictureBox
                }
            }
            catch (Exception ex)
            {
                // Handle any errors that may occur while fetching the thumbnail
                Console.WriteLine($"An error occurred: {ex.Message}");
                downloadStatusLabel.Text = "Invalid URL";
                LengthLabel.Text = "Length: N/A";
                AuthorLabel.Text = "Author: N/A";
                thumbnailPictureBox.Image = null; // Clear the PictureBox
            }
        }
        private bool IsValidImage(byte[] imageBytes)
        {
            try
            {
                using (var ms = new MemoryStream(imageBytes))
                {
                    Image.FromStream(ms);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        // Function to extract the video ID from a YouTube URL
        private string ExtractVideoId(string url)
        {
            string videoId = null;
            try
            {
                var match = Regex.Match(url, @"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/ ]{11})");
                if (match.Success)
                {
                    videoId = match.Groups[1].Value;
                }
            }
            catch (Exception)
            {
                // Handle any potential exceptions during the regular expression matching
            }
            return videoId;
        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        private void OutputSelect_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    // Set the selected folder path as the output directory
                    outputDirectoryTextBox.Text = folderDialog.SelectedPath;
                }
            }
        }

        private void LengthLabel_Click(object sender, EventArgs e)
        {

        }
    }
}
