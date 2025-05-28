using Microsoft.VisualBasic.Logging;
using System.Diagnostics;
using System.Security.Policy;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.IO.Compression;
using SevenZip;
using System.Configuration;

namespace update
{
    public partial class Form1 : Form
    {

        private static int installPhase = 0;
        private float progressPercentage = 0;
        public Form1()
        {
            InitializeComponent();
        }
        public Form1(string[] args)
        {
            InitializeComponent();
            useGithub = args.Any(u=>u == "-github");
        }
        private async Task DownloadFileAsync(string fileUrl,string savePath,Label statusLabel, ProgressBar progressBar)
        {
            // ȷ��UI���°�ȫ
            void UpdateStatus(string text, int progress = 0)
            {
                if (statusLabel.InvokeRequired || progressBar.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        statusLabel.Text = text;
                        progressBar.Value = progress;
                    });
                }
                else
                {
                    statusLabel.Text = text;
                    progressBar.Value = progress;
                }
            }

            UpdateStatus("���ڿ�ʼ����...");
            using (var client = new HttpClient())
            {
                try
                {
                    // ����Ŀ��Ŀ¼
                    string directory = Path.GetDirectoryName(savePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        long totalRead = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes.HasValue && totalBytes.Value > 0)
                            {
                                double percentage = (double)totalRead / totalBytes.Value * 100;
                                int progressValue = (int)percentage;
                                UpdateStatus($"������: {progressValue}% ���", progressValue);
                            }
                        }
                    }

                    UpdateStatus("������ɣ�", 100);
                }
                catch (Exception ex)
                {
                    UpdateStatus("����ʧ��: " + ex.Message);
                    MessageBox.Show("��������" + ex.Message);
                }
            }
        }
        /// <summary>
        /// �����ļ�Sha256
        /// </summary>
        /// <param name="filePath">�ļ���ַ</param>
        /// <returns>����Sha256</returns>
        public static string CalculateSHA256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(fileStream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
        /// <summary>
        /// ��ѹ�ļ�
        /// </summary>
        /// <param name="archivePath">�����ļ���ַ</param>
        /// <param name="output">����ļ���ַ</param>
        public void Unarchive(string archivePath, string output)
        {
            try
            {
                SevenZipExtractor.SetLibraryPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.dll"));
                using (SevenZipExtractor extractor = new SevenZipExtractor(archivePath))
                {
                    extractor.ExtractArchive(output);
                }
                Console.WriteLine("��ѹ���");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"��������: {ex.Message}");
            }
        }

        /// <summary>
        /// ��ȡ����ַ���ı���ͨ������API��
        /// </summary>
        /// <param name="Url">��ַ</param>
        /// <returns></returns>
        public static async Task<string> GetURLText(string Url)
        {
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", "LLC_MOD_Toolbox");
                string raw = string.Empty;
                raw = await client.GetStringAsync(Url);
                return raw;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
        bool useGithub = false;
        string temp_path;
        string box_path;
        string hash;
        private async void Form1_Load(object sender, EventArgs e)
        {
            box_path = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.Trim('\\')).FullName;
            //��ȡtemp�ļ���  
            temp_path = Path.Combine(Path.GetTempPath(), "LLC_Toolbox_temp");
            if (!Directory.Exists(temp_path))
            {
                Directory.CreateDirectory(temp_path);
            }
            temp_path = Path.Combine(temp_path, "LLC_MOD_Toolbox.7z");
            try
            {
                string url = "https://download.zeroasso.top/files/LLC_MOD_Toolbox.7z";
                if (useGithub)
                {
                    //��ȡ���°汾��  
                    using HttpClient client = new();
                    client.DefaultRequestHeaders.Add("User-Agent", "LLC_MOD_Toolbox");
                    string raw = string.Empty;
                    raw = await client.GetStringAsync("https://api.github.com/repos/LocalizeLimbusCompany/LLC_MOD_Toolbox/releases/latest");
                    JsonNode? parsedNode = JsonNode.Parse(raw);
                    if (parsedNode is JsonObject result)
                    {
                        string verson = result["name"]?.ToString() ?? string.Empty;
                        url = $"https://github.com/LocalizeLimbusCompany/LLC_MOD_Toolbox/releases/download/{verson}/LLC_MOD_Toolbox.7z";
                    }
                    else
                    {
                        throw new Exception("�޷����� JSON ����");
                    }
                }
                //�����ļ�
                await DownloadFileAsync(url, temp_path,label1,progressBar);
                //��ȡ���hash
                string hash_result = await GetURLText("https://api.zeroasso.top/v2/hash/get_hash");
                hash = JsonObject.Parse(hash_result)["nox_hash"].ToString();

                if (CalculateSHA256(temp_path) == hash)
                {
                    Unarchive(temp_path,box_path);
                    MessageBox.Show("�������");
                    box_path = Path.Combine(box_path, "LLC_MOD_Toolbox.exe");
                    if (File.Exists(box_path))
                    {
                        Process.Start(box_path);
                    }
                }
                else
                {
                    throw new Exception("У��ʧ��");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("���س������⣬�뵽�����������°汾��", "���ִ���", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                File.Delete(temp_path);
                Application.Exit();
            }
        }
    }
}
