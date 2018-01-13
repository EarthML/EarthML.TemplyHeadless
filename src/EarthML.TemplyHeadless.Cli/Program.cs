using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.NodeServices;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using RazorLight;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EarthML.TemplyHeadless.Cli
{
    public class RequireJS
    {
        private static INodeServices _nodeServices { get; set; }
        public static INodeServices nodeServices
        {
            get
            {
                if (_nodeServices == null)
                {

                    var services = new ServiceCollection();
                    services.AddNodeServices(a =>
                    {
                        // a.ProjectPath = Directory.GetCurrentDirectory(); 
                        // Set any properties that you want on 'options' here
                    });

                    var serviceProvider = services.BuildServiceProvider();
                    var options = new NodeServicesOptions(serviceProvider)
                    {
                        ProjectPath = Directory.GetCurrentDirectory() + "/tmphost",
                        InvocationTimeoutMilliseconds = 60000 * 5,
                        WatchFileExtensions = new string[] { }
                    };
                    _nodeServices = NodeServicesFactory.CreateNodeServices(options);

                }
                return _nodeServices;
            }
        }



        public static async Task<TResult> RunAsync<TData, TResult>(string module, TData data)
        {




            var sb = new StringBuilder();

            // sb.AppendLine("return function (data,callback){");
            sb.AppendLine("var requirejs = require(\"requirejs\");");

            sb.AppendLine($"var r = requirejs.config({{   packages: [{{name:'earthml-temply-headless',location:'./node_modules/earthml-temply-headless/artifacts/src',main:'headless'}}],  baseUrl:'{Path.Combine(Directory.GetCurrentDirectory(), "tmphost").Replace("\\", "/")}'}});");

            sb.AppendLine("module.exports= function (callback,data){ try{");
            sb.AppendLine($"r([\"{module}\"], function (program) {{ program.default(data, callback); }},function(err){{ console.log('host failed'); callback(err,null) }})");



            sb.AppendLine("}catch(error){console.log('host catch');callback(error,null); }}");

            Directory.CreateDirectory("tmphost");


            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(sb.ToString()));
                Guid hashguid = new Guid(hash);
                if (!File.Exists($"tmphost/{hashguid.ToString("N")}.js"))
                    File.WriteAllText($"tmphost/{hashguid.ToString("N")}.js", sb.ToString());

                return await nodeServices.InvokeAsync<TResult>("./" + hashguid.ToString("N"), data);
            }





            //  return JToken.FromObject(await Edge.Func(sb.ToString())(data)).ToObject<TResult>();
        }

    }

    [Command(Name = "simpleget", Description = "A very simple downloader")]
    [HelpOption]
    class Program
    {
        static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);

        [Option(Description = "The url for remote page hosting")]
        private string Url { get; set; }

        [Option(Description = "The url for remote page index.js", ShortName = "rm")]
        private string RemotePageMain { get; set; } = "index";
        [Option(Description = "The url for remote page index.js", ShortName = "rl")]
        private string RemotePageLocation { get; set; }

        [Option(Description = "The folder to install node modules in", ShortName = "hf")]
        private string HostFolder { get; } = "tmphost";

        [Option(Description = "The initial width of remote page")]
        private int Width { get; } = 800;
        [Option(Description = "The initial height of remote page")]
        private int Height { get; } = 600;

        [Option(Description = "The initial height of remote page", ShortName = "hw")]
        private bool HelloWorld { get; } = false;

        private async Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            if (string.IsNullOrEmpty(Url) && string.IsNullOrEmpty(RemotePageLocation) && !HelloWorld)
            {
                app.ShowHelp();
                return 0;
            }

       //     Directory.CreateDirectory(HostFolder);

            InstallNPMPackage(HostFolder, "requirejs");
            InstallNPMPackage(HostFolder, "puppeteer");
            InstallNPMPackage(HostFolder, "websocket");
            InstallNPMPackage(HostFolder, "earthml-temply-headless");


            var engine = new RazorLightEngineBuilder()
                .UseEmbeddedResourcesProject(typeof(Program))
                .UseMemoryCachingProvider()
                .Build();


            IWebHost host = new WebHostBuilder()
                    .UseKestrel()

                    .Configure(appbuilder =>
                    {

                        appbuilder.Use(async (context, next) =>
                        {

                            if (context.Request.Path == "/hello-world")
                            {

                                await context.Response.WriteAsync(@"<html><head><script src=""node_modules/requirejs/require.js""></script></head><body><script type=""text/javascript"">require.config({paths:{'earthml-temply-headless':'node_modules/earthml-temply-headless/artifacts/src'}}); require(['earthml-temply-headless/remotepage/remotepage'],(RemotePageModule)=>{console.log(RemotePageModule);let rp = new RemotePageModule.RemotePage(); rp.helloWorld(); })</script></body></html>");
                                return;
                            }

                            if(context.Request.Path == "/")
                            {
                                try
                                {
                                     
                                    string result = await engine.CompileRenderAsync("GenericRemoteHost.cshtml", new { Name = "John Doe" });
                                    
                                    await context.Response.WriteAsync(result);

                                }catch(Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                                return;
                            }

                            await next();

                        });

                        appbuilder.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "tmphost")) });
                    })
                    .Build();


            await host.StartAsync(); ;

            if (HelloWorld)
            {  
                Url = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses
                     .Select(a => a.Replace("://+", "://localhost").Replace("[::]","127.0.0.1")).FirstOrDefault() +"/hello-world";
            }

            if (!string.IsNullOrEmpty(RemotePageLocation))
            {
                Url = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses
                    .Select(a => a.Replace("://+", "://localhost").Replace("[::]", "127.0.0.1")).FirstOrDefault() + $"?remote-main={RemotePageMain}&remote-location={WebUtility.UrlEncode(RemotePageLocation)}";
            }
            

            {

                var data = await RequireJS.RunAsync<object, JToken>("earthml-temply-headless/Headless/headless", new
                {
                    url = Url,
                    headless = true,
                    size = new
                    {
                        width = Width,
                        height = Height
                    },
                    data = new { hello = "world" },
                    inputPrefix = Path.Combine(Directory.GetCurrentDirectory(), "input"),
                    outputPrefix = Path.Combine(Directory.GetCurrentDirectory(), "output")
                });

                Console.WriteLine(data.ToString());
            }
            if (host != null)
            {
                await host.StopAsync();
            }
            return 0;
        }

        private static void InstallNPMPackage(string runningFolder, string package)
        {
            bool isWindows = System.Runtime.InteropServices.RuntimeInformation
                                                           .IsOSPlatform(OSPlatform.Windows);

            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), runningFolder, "node_modules", package)))
            {
                if (isWindows)
                {


                    var npminstall = Process.Start(new ProcessStartInfo
                    {
                        FileName = @"""C:\Program Files\nodejs\node.exe""",
                        Arguments = $@"""C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js"" install {package}",
                        WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), runningFolder),

                    });
                    npminstall.WaitForExit();
                }
                else
                {
                    var npminstall = Process.Start(new ProcessStartInfo
                    {
                        FileName = @"npm",
                        Arguments = $"install {package}",
                        WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), runningFolder),

                    });
                    npminstall.WaitForExit();
                }
            }
        }
    }
}
