using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.NodeServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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
    public static class RequireJS
    {
        
       



        public static async Task<TResult> RunAsync<TData, TResult>(INodeServices nodeServices, string tmphost, string module, TData data)
        {




            var sb = new StringBuilder();

            // sb.AppendLine("return function (data,callback){");
            sb.AppendLine("var requirejs = require(\"requirejs\");");

           // sb.AppendLine($"var r = requirejs.config({{   packages: [{{name:'earthml-temply-headless',location:'C:/dev/EarthML/EarthML.TemplyHeadless/src/EarthML.TemplyHeadless/artifacts/src',main:'headless'}}],  baseUrl:'{Path.Combine(Directory.GetCurrentDirectory(), "tmphost").Replace("\\", "/")}'}});");
            sb.AppendLine($"var r = requirejs.config({{   packages: [{{name:'earthml-temply-headless',location:'./node_modules/earthml-temply-headless/artifacts/src',main:'headless'}}],  baseUrl:'{tmphost.Replace("\\", "/")}'}});");


            sb.AppendLine("module.exports= function (callback,data){ try{");
            sb.AppendLine($"r([\"{module}\"], function (program) {{ program.default(data, callback); }},function(err){{ console.log('host failed'); callback(err,null) }})");



            sb.AppendLine("}catch(error){console.log('host catch');callback(error,null); }}");

            Directory.CreateDirectory(tmphost);


            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(sb.ToString()));
                Guid hashguid = new Guid(hash);
                if (!File.Exists($"{tmphost}/{hashguid.ToString("N")}.js"))
                    File.WriteAllText($"{tmphost}/{hashguid.ToString("N")}.js", sb.ToString());

                return await nodeServices.InvokeAsync<TResult>("./" + hashguid.ToString("N"), data);
            }





            //  return JToken.FromObject(await Edge.Func(sb.ToString())(data)).ToObject<TResult>();
        }

    }

    [Command(Name = "simpleget", Description = "A very simple downloader", ThrowOnUnexpectedArgument =false)]
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

        [Option(Description = "The folder to install node modules in", ShortName = "hf", LongName ="host-folder")]
        private string HostFolder { get; } = "/tmphost";

        [Option(Description = "The initial width of remote page")]
        private int Width { get; } = 2000;
        [Option(Description = "The initial height of remote page")]
        private int Height { get; } = 1000;

        [Option(Description = "The initial height of remote page", ShortName = "hw")]
        private bool HelloWorld { get; } = false;

        [Option(CommandOptionType.MultipleValue, Description = "Volumes", ShortName = "v")]
        private string[] Volumes { get; } = new string[0];

        private string InputFolder { get; } = "/data";

        [Option(Description = "output directory", ShortName = "do", LongName ="data-out")]
        private string OutputFolder { get; } = "/data";

        private async Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            if (string.IsNullOrEmpty(Url) && string.IsNullOrEmpty(RemotePageLocation) && !HelloWorld)
            {
                app.ShowHelp();
                return 0;
            }
            Console.WriteLine(HostFolder);
           
                 Directory.CreateDirectory(HostFolder);
            Directory.CreateDirectory("output");
            await InstallNPMPackage(HostFolder, "requirejs");
            await  InstallNPMPackage(HostFolder, "puppeteer");
            await InstallNPMPackage(HostFolder, "websocket");
            await InstallNPMPackage(HostFolder, "isomorphic-fetch");
            await InstallNPMPackage(HostFolder, "earthml-temply-headless");

         
            var engine = new RazorLightEngineBuilder()
                .UseEmbeddedResourcesProject(typeof(Program))
                .UseMemoryCachingProvider()
                .Build();
            Console.WriteLine(string.Join(" ", app.RemainingArguments));

            var args = new JObject();
            for(var j = 0; j < app.RemainingArguments.Count; j+=2)
            {
                args[app.RemainingArguments[j].Trim('-')] = app.RemainingArguments[j + 1];
            }

            IWebHost host = new WebHostBuilder()
                    .UseKestrel()

                    .Configure(appbuilder =>
                    {

                        appbuilder.Use(async (context, next) =>
                        {
                            Console.WriteLine(context.Request.Path);

                            if (context.Request.Path == "/installDependencies")
                            {
                                var dps = JToken.Parse(await new StreamReader(context.Request.Body).ReadToEndAsync());
                                foreach (JProperty dependency in dps.SelectToken("$.dependencies"))
                                {
                                    await InstallNPMPackage(HostFolder, dependency.Name, dependency.Value.ToString());
                                }

                                context.Response.StatusCode = 200;
                                return;


                            }

                            if (context.Request.Path == "/hello-world")
                            {

                                await context.Response.WriteAsync(@"<html><head><script src=""node_modules/requirejs/require.js""></script></head><body><script type=""text/javascript"">require.config({paths:{'earthml-temply-headless':'node_modules/earthml-temply-headless/artifacts/src'}}); require(['earthml-temply-headless/remotepage/remotepage'],(RemotePageModule)=>{console.log(RemotePageModule);let rp = new RemotePageModule.RemotePage(); rp.helloWorld(); })</script></body></html>");
                                return;
                            }

                            if (context.Request.Path == "/")
                            {
                                try
                                {

                                    string result = await engine.CompileRenderAsync("GenericRemoteHost.cshtml", new { Name = "John Doe" });

                                    await context.Response.WriteAsync(result);

                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                                return;
                            }


                            await next();

                        });
                        foreach(var volumne in Volumes)
                        {

                            appbuilder.Map(volumne.Split(':').Last(), (b) =>
                            {
                                b.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(string.Join(":",volumne.Split(':').Reverse().Skip(1).Reverse())) });

                            });
                        }

                        //appbuilder.Map("/appinsight", (b) =>
                        //{
                        //    b.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(@"C:\dev\EarthML\EarthML.TemplyHeadless\samples\ApplicationInsightsGraphGenerator\wwwroot") });

                        //});

                        //appbuilder.Map("/AscendRegistration", (b) =>
                        //{
                        //    b.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(@"C:\dev\Ascend\AscendRecording\AscendRecording\www\libs\AscendRegistration") });

                        //});

                        //appbuilder.Map("/node_modules/earthml-temply-headless/artifacts/src", (b) =>
                        //{
                        //    b.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider("C:/dev/EarthML/EarthML.TemplyHeadless/src/EarthML.TemplyHeadless/artifacts/src") });
                        //});

                        appbuilder.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider( HostFolder ) });
                    })
                    .Build();


            await host.StartAsync(); ;

            if (HelloWorld)
            {
                Url = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses
                     .Select(a => a.Replace("://+", "://localhost").Replace("[::]", "127.0.0.1")).FirstOrDefault() + "/hello-world";
            }

            if (!string.IsNullOrEmpty(RemotePageLocation))
            {
                Url = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses
                    .Select(a => a.Replace("://+", "://localhost").Replace("[::]", "127.0.0.1")).FirstOrDefault() + $"?remote-main={RemotePageMain}&remote-location={WebUtility.UrlEncode(RemotePageLocation)}";
            }


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
                    ProjectPath = HostFolder,
                    InvocationTimeoutMilliseconds = 60000 * 10, 
                    WatchFileExtensions = new string[] { }
                };
                var _nodeServices = NodeServicesFactory.CreateNodeServices(options);

                try
                {
                    var data = await RequireJS.RunAsync<object, JToken>(_nodeServices, HostFolder, "earthml-temply-headless/Headless/headless", new
                    {
                        url = Url,
                        headlessHost = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses
                        .Select(a => a.Replace("://+", "://localhost").Replace("[::]", "127.0.0.1")).FirstOrDefault(),
                        headless = true,
                        size = new
                        {
                            width = Width,
                            height = Height
                        },
                        data = args,
                        inputPrefix = InputFolder,
                        outputPrefix = OutputFolder
                    });


                    Console.WriteLine(data.ToString());
                }catch(Exception ex)
                {
                    Console.WriteLine(ex);

                    await Task.Delay(60000);    
                }
            }
            if (host != null)
            {
                await host.StopAsync();
            }
            return 0;
        }

        private static Task InstallNPMPackage(string runningFolder, string package, string version = null)
        {
            return Task.Factory.StartNew(() =>
            {
                Console.WriteLine($"Installing {package}{(string.IsNullOrEmpty(version) ? "" : $"@{version}")}");
                bool isWindows = System.Runtime.InteropServices.RuntimeInformation
                                                               .IsOSPlatform(OSPlatform.Windows);

                if (!Directory.Exists(Path.Combine(runningFolder, "node_modules", package)))
                {
                    if (isWindows)
                    {


                        var npminstall = Process.Start(new ProcessStartInfo
                        {
                            FileName = @"""C:\Program Files\nodejs\node.exe""",
                            Arguments = $@"""C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js"" install {package}{(string.IsNullOrEmpty(version) ? "" : $"@{version}")}",
                            WorkingDirectory = runningFolder,

                        });
                        npminstall.WaitForExit();
                    }
                    else
                    {
                        var npminstall = Process.Start(new ProcessStartInfo
                        {
                            FileName = @"npm",
                            Arguments = $"install {package}{(string.IsNullOrEmpty(version) ? "" : $"@{version}")}",
                            WorkingDirectory = runningFolder,

                        });
                        npminstall.WaitForExit();
                    }
                }
            });
        }
    }
}
