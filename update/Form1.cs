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
        private bool useGithub;
        private bool usePatch;
        private string temp_path = Path.Combine(Path.GetTempPath(), "LLC_Toolbox_temp");
        private string verson_url = "https://api.github.com/repos/LocalizeLimbusCompany/LLC_MOD_Toolbox/releases/latest";
        //Ĭ������·��
        private string def_download_url = "https://download.zeroasso.top/files/LLC_MOD_Toolbox.7z";
        //��ָ��°�����·��
        private string patch_download_url = "https://download.zeroasso.top/files/LLC_MOD_Toolbox.7z";

        public Form1()
        {
            InitializeComponent();
        }
        public Form1(string[] args)
        {
            InitializeComponent();
            useGithub = !args.Any(u => u == "-github");
            usePatch = args.Any(u => u == "-patch");
        }
        private CancellationTokenSource _cts;
        private string? _currentSavePath; // ��ǰ�������ص��ļ�·��
        /// <summary>
        /// �����ļ�
        /// </summary>
        /// <param name="fileUrl">���ص�ַ</param>
        /// <param name="savePath">�����ַ</param>
        /// <param name="statusLabel">������ʾ���ȵ�label</param>
        /// <param name="progressBar">������ʾ���ȵ�progressBar</param>
        private async Task DownloadFileAsync(string fileUrl, string savePath, Label statusLabel, ProgressBar progressBar)
        {
            _currentSavePath = savePath;
            _cts = new CancellationTokenSource();

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

            UpdateStatus("��ʼ����...");
            using (var client = new HttpClient())
            {
                try
                {
                    string directory = Path.GetDirectoryName(savePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;

                    using (var contentStream = await response.Content.ReadAsStreamAsync(_cts.Token))
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        long totalRead = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, _cts.Token);
                            totalRead += bytesRead;

                            if (totalBytes.HasValue && totalBytes > 0)
                            {
                                int progressValue = (int)((double)totalRead / totalBytes.Value * 100);
                                UpdateStatus($"������: {progressValue}% ���", progressValue);
                            }
                        }
                    }

                    UpdateStatus("������ɣ�", 100);
                    _currentSavePath = null; // ������ɣ���������
                }
                catch (OperationCanceledException)
                {
                    UpdateStatus("������ȡ��");
                    throw new OperationCanceledException("�û�ȡ�������ء�", _cts.Token);
                }
                catch (Exception ex)
                {
                    UpdateStatus("����ʧ��: " + ex.Message);
                    throw;
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
        private async void Form1_Load(object sender, EventArgs e)
        {
            //��ȡtemp�ļ���
            if (!Directory.Exists(temp_path))
            {
                Directory.CreateDirectory(temp_path);
            }
            temp_path = Path.Combine(temp_path, "LLC_MOD_Toolbox.7z");
            try
            {
                string url = def_download_url;
                if (useGithub)
                {
                    //��ȡ���°汾��  
                    using HttpClient client = new();
                    client.DefaultRequestHeaders.Add("User-Agent", "LLC_MOD_Toolbox");
                    string raw = string.Empty;
                    raw = await client.GetStringAsync(verson_url);
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
                if (usePatch)
                {
                    url = patch_download_url;
                }
                //�����ļ�
                await DownloadFileAsync(url, temp_path, label1, progressBar);
                progressBar.Value = 100;
                label1.Text = "�ϲ��ļ���...";
                //��ȡ���hash
                string hash = "";
                string hash_result = await GetURLText("https://api.zeroasso.top/v2/hash/get_hash");
                JsonNode? hashNode = JsonNode.Parse(hash_result);
                if (hashNode is JsonObject hashResult)
                {
                    hash = hashResult["box_hash"]?.ToString() ?? string.Empty;
                }
                else
                {
                    throw new Exception("�޷����� JSON ����");
                }

                if (CalculateSHA256(temp_path) == hash)
                {
                    Unarchive(temp_path, @"..\");
                    progressBar.Value = 140;
                    label1.Text = "���";
                    MessageBox.Show("�������");

                    if (File.Exists(@"..\LLC_MOD_Toolbox.exe"))
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = @"..\LLC_MOD_Toolbox.exe",
                            WorkingDirectory = @"..\"
                        };
                        Process.Start(startInfo);
                    }
                }
                else
                {
                    throw new Exception("У��ʧ��");
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("ȡ�����أ������������ر�", "ȡ��", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("���س������⣬�뵽�����������°汾��", "���ִ���", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                //������ػ��棬�رո�����
                if (File.Exists(temp_path))
                {
                    File.Delete(temp_path);
                }
                Application.Exit();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // ȡ����������

                // ɾ��δ��ɵ��ļ�
                if (!string.IsNullOrEmpty(_currentSavePath) && File.Exists(_currentSavePath))
                {
                    try
                    {
                        File.Delete(_currentSavePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("�޷�ɾ��δ��ɵ��ļ���" + ex.Message);
                    }
                }
            }

            base.OnFormClosing(e);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // ȡ����������
            }
        }
    }
}
