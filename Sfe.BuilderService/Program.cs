using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NETCore.Encrypt;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Sfe.BuilderUtils;
using Newtonsoft.Json;

namespace Sfe.BuilderService
{
    public class Program
    {
        public static string Key = string.Empty;
        public static string PackPath = string.Empty;
        public static void Main(string[] args)
        {
            PackPath = AppDomain.CurrentDomain.BaseDirectory + "Build\\";
            Help.CreateDir(PackPath);
            CreateWebHostBuilder(args).Build().Run();
        }
        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var Config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("Config.json", optional: true, reloadOnChange: true).Build();
            Program.Key = Config.GetValue<string>("key");
            return WebHost.CreateDefaultBuilder(args).UseKestrel(Option =>
            {
                Option.Limits.MaxRequestBodySize = null;
            }).UseStartup<Startup>().UseConfiguration(Config);
        }
    }
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public IConfiguration Configuration { get; }
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<FormOptions>(options =>
            {
                options.ValueCountLimit = int.MaxValue;
                options.ValueLengthLimit = int.MaxValue;
                options.KeyLengthLimit = int.MaxValue;
                options.MultipartBodyLengthLimit = int.MaxValue;
                options.MultipartBoundaryLengthLimit = int.MaxValue;
            });
            services.AddLogging(Options => { Options.ClearProviders(); });
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.Run(async (Context) =>
            {
                if (IsBuilder(Context))
                {
                    try
                    {
                        byte[] ResultBytes;
                        using (var buffer = new MemoryStream())
                        {
                            Context.Request.Body.CopyTo(buffer);
                            ResultBytes = buffer.ToArray();
                        }
                        ResultBytes = EncryptProvider.DESDecrypt(ResultBytes, Program.Key);
                        if (ResultBytes.Length == 0)
                        {
                            await Context.Response.WriteAsync("密钥错误！");
                            return;
                        }
                        var Result = MessagePackSerializer.Typeless.Deserialize(ResultBytes) as Dictionary<object, object>;
                        string Act = Result["Act"].ToString();
                        if (Act == "Test")
                        {
                            await Context.Response.WriteAsync("OK");
                        }
                        if (Act == "CheckOutPath")
                        {
                            string OutPath = Result["OutPath"].ToString();
                            await Context.Response.WriteAsync(Directory.Exists(OutPath) ? "OK" : "Error");
                        }
                        if (Act == "GetDirMD5")
                        {
                            string OutPath = Result["OutPath"].ToString();
                            await Context.Response.WriteAsync(JsonConvert.SerializeObject(Help.GetDirMD5(OutPath)));
                        }
                        if (Act == "Publish")
                        {
                            string FileName = Result["FileName"].ToString();
                            byte[] Pack = Result["Pack"] as byte[];
                            string OutPath = Result["OutPath"].ToString();
                            string[] BeforeCommands = (Result["BeforeCommands"] as object[]).Select(c => c.ToString()).ToArray();
                            string[] AfterCommands = (Result["AfterCommands"] as object[]).Select(c => c.ToString()).ToArray();
                            bool DeleteFiles = (bool)Result["DeleteFiles"];
                            if (File.Exists(Program.PackPath + FileName))
                            {
                                File.Delete(Program.PackPath + FileName);
                            }
                            using (FileStream fs = new FileStream(Program.PackPath + FileName, FileMode.Create))
                            {
                                fs.Write(Pack, 0, Pack.Length);
                            }
                            Help.Run(BeforeCommands);
                            if (DeleteFiles)
                            {
                                Help.DeleteDir(OutPath);
                            }
                            ZipFile.ExtractToDirectory(Program.PackPath + FileName, OutPath, true);
                            Help.Run(AfterCommands);
                            await Context.Response.WriteAsync("发布成功！");
                        }
                    }
                    catch (Exception Ex)
                    {
                        await Context.Response.WriteAsync("错误:" + Ex.Message + "！");
                    }
                }
                await Context.Response.WriteAsync(string.Empty);
            });
        }
        public bool IsBuilder(HttpContext Context)
        {
            string MService = Context.Request.Headers["Builder"];
            return !string.IsNullOrWhiteSpace(MService) && MService.Equals("BuilderService");
        }
    }
}