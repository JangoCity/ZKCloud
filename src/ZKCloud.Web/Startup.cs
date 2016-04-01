﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.StaticFiles;
using Microsoft.AspNet.FileProviders;
using Microsoft.AspNet.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using ZKCloud.Container;
using ZKCloud.Domain.Repositories;
using ZKCloud.Domain.Repositories.EntityFramework;
using ZKCloud.Domain.Services;
using ZKCloud.Web.Mvc;
using ZKCloud.Web.Mvc.Dynamic;
using ZKCloud.Runtime;
using ZKCloud.Localize;
using Microsoft.AspNet.Mvc.Controllers;
using Microsoft.AspNet.Mvc;
using ZKCloud.Web.Apps.Demo01.Domain.Services;
using ZKCloud.Apps;

namespace ZKCloud.Web {
    public class Startup {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services) {
			AppManager.LoadAll();
			services.AddMvc();
			services.AddAppRazorViewEngine();
			services.AddAppUrlHelper();
			services.AddDynamicControllerTypeProvider();
			services.AddCaching();
			services.AddSession(options =>
			{
				options.IdleTimeout = TimeSpan.FromMinutes(60);
				options.CookieName = ".ZKCloud";
			});
		}

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app) {
			app.UseIISPlatformHandler();

			app.UseFileServer(new FileServerOptions()
			{
				FileProvider = new PhysicalFileProvider(RuntimeContext.Current.Path.BaseDirectory),
				RequestPath = new PathString(""),
				EnableDirectoryBrowsing = false
			});

			app.UsePerHttpRequestContainer();
			app.UseSession();
			app.UseMvc(routes => {

                routes.MapRoute(
                    name: "default",
                    template: "{app}/{controller}/{action}",
                    defaults: new { app = "base", controller = "common", action = "index" });
                //新添加的路由配置 by jak
                routes.MapAppRoute(
                    name: "app_one",
                    template: "{app}/{controller?}",
                    defaults: new { controllr = "index", action = "" });
                routes.MapAppRoute(
                    name: "app_two",
                    template: "{app}/{controller}/{action}");
                routes.MapAppRoute(
                    name: "app_three",
                    template: "{app}/{controller}/{action}/{action1}");
                routes.MapAppRoute(
                    name: "app_four",
                    template: "{app}/{controller}/{action}/{action1}/{action2}/{id?}");
            });
			
            app.RegisterEntityFrameworkRepositoryContext();
            app.RegisterAllModelCreators();
            app.RegisterAllRepositories();
            app.RegisterAllServices();
            app.RegisterAllTranslateProviders();


			DynamicApiControllerBuilder.For<ITestDataService>().WithApp("demo03").Build();
		}

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}
