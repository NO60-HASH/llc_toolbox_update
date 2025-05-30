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
        //默认下载路径
        private string def_download_url = "https://download.zeroasso.top/files/LLC_MOD_Toolbox.7z";
        //差分更新包下载路径
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
        private string? _currentSavePath; // 当前正在下载的文件路径
        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileUrl">下载地址</param>
        /// <param name="savePath">保存地址</param>
        /// <param name="statusLabel">用于显示进度的label</param>
        /// <param name="progressBar">用于显示进度的progressBar</param>
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

            UpdateStatus("开始下载...");
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
                                UpdateStatus($"下载中: {progressValue}% 完成", progressValue);
                            }
                        }
                    }

                    UpdateStatus("下载完成！", 100);
                    _currentSavePath = null; // 下载完成，无需清理
                }
                catch (OperationCanceledException)
                {
                    UpdateStatus("下载已取消");
                    throw new OperationCanceledException("用户取消了下载。", _cts.Token);
                }
                catch (Exception ex)
                {
                    UpdateStatus("下载失败: " + ex.Message);
                    throw;
                }
            }
        }
        /// <summary>
        /// 计算文件Sha256
        /// </summary>
        /// <param name="filePath">文件地址</param>
        /// <returns>返回Sha256</returns>
        public static string CalculateSHA256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(fileStream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
        /// <summary>
        /// 解压文件
        /// </summary>
        /// <param name="archivePath">输入文件地址</param>
        /// <param name="output">输出文件地址</param>
        public void Unarchive(string archivePath, string output)
        {
            try
            {
                SevenZipExtractor.SetLibraryPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.dll"));
                using (SevenZipExtractor extractor = new SevenZipExtractor(archivePath))
                {
                    extractor.ExtractArchive(output);
                }
                Console.WriteLine("解压完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
            }
        }
        /// <summary>
        /// 获取该网址的文本，通常用于API。
        /// </summary>
        /// <param name="Url">网址</param>
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
            //获取temp文件夹
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
                    //获取最新版本号  
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
                        throw new Exception("无法解析 JSON 数据");
                    }
                }
                if (usePatch)
                {
                    url = patch_download_url;
                }
                //下载文件
                await DownloadFileAsync(url, temp_path, label1, progressBar);
                progressBar.Value = 100;
                label1.Text = "合并文件中...";
                //获取软件hash
                string hash = "";
                string hash_result = await GetURLText("https://api.zeroasso.top/v2/hash/get_hash");
                JsonNode? hashNode = JsonNode.Parse(hash_result);
                if (hashNode is JsonObject hashResult)
                {
                    hash = hashResult["box_hash"]?.ToString() ?? string.Empty;
                }
                else
                {
                    throw new Exception("无法解析 JSON 数据");
                }

                if (CalculateSHA256(temp_path) == hash)
                {
                    Unarchive(temp_path, @"..\");
                    progressBar.Value = 140;
                    label1.Text = "完成";
                    MessageBox.Show("更新完成");

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
                    throw new Exception("校验失败");
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("取消下载，更新器即将关闭", "取消", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("下载出现问题，请到官网下载最新版本。", "出现错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                //清除下载缓存，关闭更新器
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
                _cts.Cancel(); // 取消下载任务

                // 删除未完成的文件
                if (!string.IsNullOrEmpty(_currentSavePath) && File.Exists(_currentSavePath))
                {
                    try
                    {
                        File.Delete(_currentSavePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("无法删除未完成的文件：" + ex.Message);
                    }
                }
            }

            base.OnFormClosing(e);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // 取消下载任务
            }
        }
    }
}
