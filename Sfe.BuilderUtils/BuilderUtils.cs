using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Sfe.BuilderUtils
{
    public class LocalConfig
    {
        public string Mode { get; set; } = "Release";
        public string CsprojFile { get; set; } = ".csproj项目文件";
        public string OutPath { get; set; } = "输出路径";
        public string TargetFramework { get; set; } = "netcoreapp2.2";
        public bool SelfContained { get; set; } = false;
        public string RuntimeIdentifier { get; set; } = "win-x64";
        public bool DeleteFiles { get; set; } = true;
        public string[] HandleFile { get; set; } = new string[] { };
        public string[] BeforeCommands { get; set; } = new string[] { };
        public string[] AfterCommands { get; set; } = new string[] { };
    }
    public class RemoteConfog : LocalConfig
    {
        public string Key { get; set; } = "数据密钥，必须使用key命令创建";
        public string ServerAddress { get; set; } = "远程服务完整地址";
    }

    public class CheckFile
    {
        public string Path { get; set; }
        public string MD5 { get; set; }
        public string Sign { get; set; }
    }
    public class Help
    {
        public static void CreateDir(params string[] TempPaths)
        {
            foreach (var item in TempPaths)
            {
                if (!Directory.Exists(item))
                {
                    Directory.CreateDirectory(item);
                }
            }
        }
        public static void DeleteDir(string TempPath)
        {
            DirectoryInfo Dir = new DirectoryInfo(TempPath);
            FileSystemInfo[] Fileinfo = Dir.GetFileSystemInfos();
            foreach (FileSystemInfo i in Fileinfo)
            {
                if (i is DirectoryInfo)
                {
                    DirectoryInfo SubDir = new DirectoryInfo(i.FullName);
                    SubDir.Delete(true);
                }
                else
                {
                    File.Delete(i.FullName);
                }
            }
        }
        public static void DeleteDir(string TempPath, params string[] ExcludePaths)
        {
            if (TempPath.Substring(TempPath.Length - 1, 1) != "\\")
            {
                TempPath += "\\";
            }
            DirectoryInfo Dir = new DirectoryInfo(TempPath);
            var Files = Dir.GetFiles("*.*", SearchOption.AllDirectories);
            foreach (var item in Files)
            {
                string FileName = item.FullName.Replace(TempPath, "");
                if (!ExcludePaths.Any(c => c == FileName))
                {
                    item.Delete();
                }
            }
            var Dirs = Dir.GetDirectories("*.*", SearchOption.AllDirectories).ToList().OrderByDescending(c => c.FullName);
            foreach (var item in Dirs)
            {
                if (item.GetFileSystemInfos().Count() == 0)
                {
                    item.Delete();
                }
            }
        }
        public static List<CheckFile> GetDirMD5(string TempPath)
        {
            if (TempPath.Substring(TempPath.Length - 1, 1) != "\\")
            {
                TempPath += "\\";
            }
            List<CheckFile> CheckFiles = new List<CheckFile>();
            DirectoryInfo Dir = new DirectoryInfo(TempPath);
            var Files = Dir.GetFiles("*.*", SearchOption.AllDirectories);
            foreach (var item in Files)
            {
                string FilePath = item.FullName.Replace(TempPath, "");
                string MD5 = GetFileMD5(item.FullName);
                CheckFiles.Add(new CheckFile()
                {
                    Path = FilePath,
                    MD5 = MD5,
                    Sign = GetMD5(FilePath + MD5)
                });
            }
            return CheckFiles;
        }
        public static string GetFileMD5(string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            file.Close();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }
        public static string GetMD5(string source)
        {
            byte[] sor = Encoding.UTF8.GetBytes(source);
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] result = md5.ComputeHash(sor);
            StringBuilder strbul = new StringBuilder(40);
            for (int i = 0; i < result.Length; i++)
            {
                strbul.Append(result[i].ToString("x2"));//加密结果"x2"结果为32位,"x3"结果为48位,"x4"结果为64位
            }
            return strbul.ToString();
        }
        public static string Run(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                return "";
            }
            string Command = string.Empty;
            string ReStr = string.Empty;
            foreach (var item in args)
            {
                Command += item + Environment.NewLine;
            }
            var ShellFile = AppDomain.CurrentDomain.BaseDirectory;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ShellFile += "win.bat";
                Command = "@echo off" + Environment.NewLine + Command;
                File.WriteAllText(ShellFile, Command, Encoding.ASCII);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ShellFile += "linux.sh";
                Command = "#!/bin/bash" + Environment.NewLine + Command;
                File.WriteAllText(ShellFile, Command, Encoding.ASCII);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ShellFile += "OSX.sh";
                Command = "#!/bin/bash" + Environment.NewLine + Command;
                File.WriteAllText(ShellFile, Command, Encoding.ASCII);
            }
            else
            {
                return "不能识别识别平台";
            }
            var psi = new ProcessStartInfo(ShellFile) { RedirectStandardOutput = true };
            var proc = Process.Start(psi);
            if (proc == null)
            {
                Console.WriteLine("错误：不能执行脚本.");
            }
            else
            {
                ReStr += "---------------开始执行脚本----------------" + Environment.NewLine;
                using (var sr = proc.StandardOutput)
                {
                    while (!sr.EndOfStream)
                    {
                        ReStr += sr.ReadLine() + Environment.NewLine;
                    }
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
                ReStr += "---------------脚本执行完毕------------------" + Environment.NewLine;
            }
            File.Delete(ShellFile);
            return ReStr;
        }
    }
}