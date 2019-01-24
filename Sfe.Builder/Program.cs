using MessagePack;
using NETCore.Encrypt;
using Newtonsoft.Json;
using Sfe.BuilderUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Sfe.Builder
{

    class Program
    {

        static void Main(string[] args)
        {
            Help.CreateDir(AppDomain.CurrentDomain.BaseDirectory + "Config\\", AppDomain.CurrentDomain.BaseDirectory + "Build\\");
            switch (args[0].ToLower())
            {
                case "t":
                case "template":
                    if (args.Length == 3)
                    {
                        CreateTemplate(args[1], args[2]);
                    }
                    break;
                case "p":
                case "publish":
                    if (args.Length == 2)
                    {
                        Publish(args[1]);
                    }
                    break;
                case "k":
                case "key":
                    if (args.Length == 1)
                    {
                        Console.WriteLine();
                        Console.WriteLine("-------------------------Key--------------------------");
                        Console.WriteLine(EncryptProvider.CreateDesKey());
                        Console.WriteLine("-------------------------Key--------------------------");
                    }
                    break;
                default:
                    Console.WriteLine();
                    Console.WriteLine("-------------------------Help-------------------------");
                    Console.WriteLine("使用方法请查看README.txt");
                    Console.WriteLine("-------------------------Help-------------------------");
                    break;
            }
        }
        static List<string> HandleStr(string InStr)
        {
            InStr = InStr.Trim();
            var InStrs = InStr.Split("\n", StringSplitOptions.RemoveEmptyEntries);
            List<string> ReStr = new List<string>(); ;
            foreach (var item in InStrs)
            {
                if (item.IndexOf("的还原在") > -1)
                {
                    ReStr.Add(HandleResotre(item.Trim()));
                }
                if (item.IndexOf("warning ") > -1)
                {
                    ReStr.Add(HandlWarning(item.Trim()));
                }
                if (item.IndexOf("error ") > -1)
                {
                    ReStr.Add(HandlError(item.Trim()));
                }
            }
            return ReStr;
            string HandleResotre(string Str)
            {
                string Hs = TQStr(Str, "的还原在 ", " 内完成");
                string FileName = Path.GetFileName(LQStr(Str, " 的还原在"));
                return $"{FileName} => Resotre => 耗时：{Hs}";
            }
            string HandlWarning(string Str)
            {
                string FileName = Path.GetFileName(TQStr(Str, "[", "]"));
                string Info = TQStr(Str, "warning ", "[");
                return $"{FileName} => Warning => {Info}";
            }
            string HandlError(string Str)
            {
                string FileName = Path.GetFileName(TQStr(Str, "[", "]"));
                string Info = LQStr(Str, "[");
                return $"{FileName} => ERROR => {Info}";
            }
            string LQStr(string Str, string EnStr)
            {
                int En = Str.IndexOf(EnStr);
                if (En == -1) { return ""; }
                return Str.Substring(0, En);
            }
            string TQStr(string Str, string StStr, string EnStr)
            {
                int St = Str.IndexOf(StStr);
                if (St == -1) { return ""; }
                St += StStr.Length;
                int En = Str.IndexOf(EnStr, St);
                if (En == -1) { return ""; }
                return Str.Substring(St, En - St);
            }
        }
        static void CreateTemplate(string TemplateType, string FileName)
        {
            string SavaPath = AppDomain.CurrentDomain.BaseDirectory + "Config\\";
            switch (TemplateType.ToLower())
            {
                case "l":
                case "local":
                    File.WriteAllText(SavaPath + FileName + ".json", JsonConvert.SerializeObject(new LocalConfig()), Encoding.UTF8);
                    break;
                case "r":
                case "remote":
                    File.WriteAllText(SavaPath + FileName + ".json", JsonConvert.SerializeObject(new RemoteConfog()), Encoding.UTF8);
                    break;
            }
            Console.WriteLine();
            Console.WriteLine("-----------------------Template-----------------------");
            Console.WriteLine(SavaPath + FileName + ".json");
            Console.WriteLine("-----------------------Template-----------------------");
        }
        static void Publish(string ConfigFile)
        {
            //检测配置文件是否存在
            Console.WriteLine($">>:检测配置文件");
            if (ConfigFile.IndexOf(".json") == -1)
            {
                ConfigFile += ".json";
            }
            ConfigFile = "Config\\" + ConfigFile;
            if (!File.Exists(ConfigFile))
            {
                Console.WriteLine($">>:错误：配置文件{ConfigFile}不存在!");
                return;
            }
            //对配置文件配置项进行基本检测
            var ReStr = File.ReadAllText(ConfigFile);
            if (!CheckTemplate(ReStr)) { return; }
            bool IsRemote = ReStr.IndexOf("Auth") > -1 && ReStr.IndexOf("ServerAddress") > -1 ? true : false;
            //生成Model
            var Model = JsonConvert.DeserializeObject<LocalConfig>(ReStr);
            var RemoteModel = JsonConvert.DeserializeObject<RemoteConfog>(ReStr);
            string OutPath = AppDomain.CurrentDomain.BaseDirectory + "Temp\\";
            if (Directory.Exists(OutPath))
            {
                Directory.Delete(OutPath, true);
            }
            //生成命令字符串
            string ActStr = $"dotnet publish {Model.CsprojFile} ";
            ActStr += $"-c {Model.Mode} -p:DebugType=none;DebugSymbols=false ";
            ActStr += Model.SelfContained ? "--self-contained true -r " + Model.RuntimeIdentifier + " " : "--self-contained false ";
            ActStr += $"-f {Model.TargetFramework} -o {OutPath}";
            Console.WriteLine(">>:开始生成项目");
            var PublishStr = Help.Run(new string[] { ActStr });
            var PublishReStr = HandleStr(PublishStr);
            var HasError = PublishReStr.Any(c => c.IndexOf("=> ERROR =>") > -1);
            Console.WriteLine(string.Join("\n", PublishReStr.Where(c => c.IndexOf("=> ERROR =>") > -1 || c.IndexOf("=> Resotre =>") > -1)));
            Console.WriteLine($"Warning:{PublishReStr.Where(c => c.IndexOf("=> Warning =>") > -1).Count()}个警告。");
            if (HasError)
            {
                if (Directory.Exists(OutPath))
                {
                    Directory.Delete(OutPath, true);
                }
                Console.WriteLine(">>:项目生成失败");
                return;
            }
            Console.WriteLine(">>:项目生成完成");
            if (Model.HandleFile != null && Model.HandleFile.Length > 0)
            {
                Console.WriteLine(">>:开始处理文件");
                foreach (var item in Model.HandleFile)
                {
                    HandleFile(item, OutPath);
                }
                Console.WriteLine(">>:文件处理完成");
            }
            if (!Model.DeleteFiles)
            {
                Console.WriteLine(">>:获取目标目录文件MD5列表");
                var InCheckFiles = Help.GetDirMD5(OutPath);
                var OutCheckFiles = new List<CheckFile>();
                if (IsRemote)
                {
                    OutCheckFiles = JsonConvert.DeserializeObject<List<CheckFile>>(Post(RemoteModel.ServerAddress, new
                    {
                        Act = "GetDirMD5",
                        Model.OutPath
                    }, RemoteModel.Key));
                }
                else
                {
                    OutCheckFiles = Help.GetDirMD5(Model.OutPath);
                }
                var ExceptList = new List<CheckFile>();
                foreach (var item in InCheckFiles)
                {
                    if (!OutCheckFiles.Any(c => c.Sign == item.Sign))
                    {
                        ExceptList.Add(item);
                    }
                }
                if (ExceptList.Count < 1)
                {
                    Directory.Delete(OutPath, true);
                    Console.WriteLine(">>:没有文件需要更新,发布结束");
                    return;
                }
                Help.DeleteDir(OutPath, ExceptList.Select(c => c.Path).ToArray());
            }
            Console.WriteLine(">>:开始打包");
            string ZipName = Path.GetFileName(Model.CsprojFile).Replace(".csproj", $"_{(IsRemote ? "Remote" : "Local")}_{DateTime.Now.ToString("yyyyMMddHHmm")}.zip");
            string ZipPath = AppDomain.CurrentDomain.BaseDirectory + "Build\\" + ZipName;
            if (File.Exists(ZipPath))
            {
                File.Delete(ZipPath);
            }
            ZipFile.CreateFromDirectory(OutPath, ZipPath);
            Directory.Delete(OutPath, true);
            Console.WriteLine(">>:完成打包");
            //开始发布
            if (!IsRemote)
            {
                Console.WriteLine(">>:开始发布");
                Help.Run(Model.BeforeCommands);
                if (Model.DeleteFiles)
                {
                    Console.WriteLine(">>:删除原有文件");
                    Help.DeleteDir(Model.OutPath);
                }
                ZipFile.ExtractToDirectory(ZipPath, Model.OutPath, true);
                Help.Run(Model.AfterCommands);
                Console.WriteLine(">>:发布完成:结束");
            }
            else
            {
                Console.WriteLine(">>:开始上传");
                byte[] FileBytes = new byte[] { };
                using (FileStream fs = new FileStream(ZipPath, FileMode.Open, FileAccess.Read))
                {
                    FileBytes = new byte[fs.Length];
                    fs.Read(FileBytes, 0, (int)fs.Length);
                }
                ReStr = Post(RemoteModel.ServerAddress, new
                {
                    Act = "Publish",
                    RemoteModel.OutPath,
                    RemoteModel.AfterCommands,
                    RemoteModel.BeforeCommands,
                    Pack = FileBytes,
                    FileName = ZipName,
                    RemoteModel.DeleteFiles
                }, RemoteModel.Key);
                Console.WriteLine(">>:返回:" + ReStr);
            }
        }

        static bool CheckTemplate(string TemplateStr)
        {
            bool IsRemote = false;
            if (TemplateStr.IndexOf("Auth") > -1 || TemplateStr.IndexOf("ServerAddress") > -1)
            {
                IsRemote = true;
            }
            var Model = JsonConvert.DeserializeObject<RemoteConfog>(TemplateStr);
            if (string.IsNullOrWhiteSpace(Model.OutPath))
            {
                Console.WriteLine($">>:错误：[OutPath]输出路径不能为空！");
                return false;
            }
            if (IsRemote)
            {
                var Temp = JsonConvert.DeserializeObject<RemoteConfog>(TemplateStr);
                if (string.IsNullOrWhiteSpace(Temp.Key))
                {
                    Console.WriteLine($">>:错误：[Key]数据密钥不能为空！");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(Temp.ServerAddress))
                {
                    Console.WriteLine($">>:错误：[ServerAddress]远程服务完整地址不能为空！");
                    return false;
                }
                if (Post(Temp.ServerAddress, new { Act = "Test" }, Temp.Key) != "OK")
                {
                    Console.WriteLine($">>:错误：远程通讯测试失败，请检查[ServerAddress]和[Key]！");
                    return false;
                }
                if (Post(Temp.ServerAddress, new
                {
                    Act = "CheckOutPath",
                    Model.OutPath
                }, Temp.Key) != "OK")
                {
                    Console.WriteLine($">>:错误：[OutPath]输出路径不存在！");
                    return false;
                }
            }
            else
            {
                if (!Directory.Exists(Model.OutPath))
                {
                    Console.WriteLine($">>:错误：[OutPath]输出路径不存在！");
                    return false;
                }
            }
            List<string> Modes = new List<string>() { "Release", "Debug" };
            if (!Modes.Contains(Model.Mode))
            {
                Console.WriteLine($">>:错误：[Mode]无法识别，可选值：{string.Join("、", Modes)}！");
                return false;
            }
            if (!File.Exists(Model.CsprojFile))
            {
                Console.WriteLine($">>:错误：[CsprojFile]文件不存在！");
                return false;
            }
            List<string> TargetFrameworks = new List<string>() { "netcoreapp2.0", "netcoreapp2.1", "netcoreapp2.2" };
            if (!TargetFrameworks.Contains(Model.TargetFramework))
            {
                Console.WriteLine($">>:错误：[TargetFramework]无法识别，可选值：{string.Join("、", TargetFrameworks)}！"); return false;
            }
            if (string.IsNullOrWhiteSpace(Model.RuntimeIdentifier) && Model.SelfContained)
            {
                Console.WriteLine($">>:错误：当[SelfContained]为True时必须设置[RuntimeIdentifier]！"); return false;
            }
            return true;
        }
        static string Post(string Url, object Param, string Key)
        {
            using (HttpClient Client = new HttpClient())
            {
                Client.DefaultRequestHeaders.Add("Builder", "BuilderService");
                using (HttpContent httpContent = new ByteArrayContent(EncryptProvider.DESEncrypt(MessagePackSerializer.Typeless.Serialize(Param), Key)))
                {
                    HttpResponseMessage response = Client.PostAsync(Url, httpContent).Result;
                    return response.Content.ReadAsStringAsync().Result;
                }
            }
        }
        static void HandleFile(string InStr, string PathStr)
        {
            var InStrs = InStr.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (InStrs.Length < 1) { return; }
            switch (InStrs[0].ToLower())
            {
                case "delete":
                    if (InStrs.Length == 2)
                    {
                        string FilePath = PathStr + "\\" + InStrs[1];
                        if (File.Exists(FilePath))
                        {
                            File.Delete(FilePath);
                        }
                        Console.WriteLine(">>:删除文件 => " + InStrs[1]);
                    }
                    break;
                case "rename":
                    if (InStrs.Length == 3)
                    {
                        string FilePath = PathStr + "\\" + InStrs[1];
                        if (File.Exists(FilePath))
                        {
                            FileInfo fi = new FileInfo(FilePath);
                            if (File.Exists(PathStr + "\\" + InStrs[2]))
                            {
                                File.Delete(PathStr + "\\" + InStrs[2]);
                            }
                            fi.MoveTo(PathStr + "\\" + InStrs[2]);
                        }
                        Console.WriteLine(">>:变更文件 => " + InStrs[1] + " => " + InStrs[2]);
                    }
                    break;
                case "replace":
                    if (InStrs.Length == 4)
                    {
                        string FilePath = PathStr + "\\" + InStrs[1];
                        if (File.Exists(FilePath))
                        {
                            var TextStr = File.ReadAllText(FilePath);
                            TextStr = TextStr.Replace(InStrs[2], InStrs[3]);
                            File.WriteAllText(FilePath, TextStr);
                        }
                        Console.WriteLine(">>:替换文件 => " + InStrs[1]);
                    }
                    break;
            }
        }
    }
}
