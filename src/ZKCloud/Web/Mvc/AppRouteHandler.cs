// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc.Abstractions;
using Microsoft.AspNet.Mvc.Core;
using Microsoft.AspNet.Mvc.Diagnostics;
using Microsoft.AspNet.Mvc.Internal;
using Microsoft.AspNet.Mvc.Logging;
using Microsoft.AspNet.Mvc.Routing;
using Microsoft.AspNet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNet.Mvc.Infrastructure;
using Microsoft.AspNet.Mvc;
using System.Collections.Generic;

namespace ZKCloud.Web.Mvc
{
    /// <summary>
    /// 系统路由处理程序
    /// </summary>
    public class AppRouteHandler : IRouter
    {
        private IActionContextAccessor _actionContextAccessor;
        private IActionInvokerFactory _actionInvokerFactory;
        private IActionSelector _actionSelector;
        private ILogger _logger;
        private DiagnosticSource _diagnosticSource;

        public VirtualPathData GetVirtualPath(VirtualPathContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            EnsureServices(context.Context);

            // The contract of this method is to check that the values coming in from the route are valid;
            // that they match an existing action, setting IsBound = true if the values are OK.
            context.IsBound = _actionSelector.HasValidAction(context);

            // We return null here because we're not responsible for generating the url, the route is.
            return null;
        }

        public async Task RouteAsync(RouteContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            
            var services = context.HttpContext.RequestServices;

            // Verify if AddMvc was done before calling UseMvc
            // We use the MvcMarkerService to make sure if all the services were added.
            MvcServicesHelper.ThrowIfMvcNotRegistered(services);
            EnsureServices(context.HttpContext);
                        
            var actionDescriptor = await _actionSelector.SelectAsync(context);
            if (actionDescriptor == null)
            { //没有直接找到对应的controller 下的 action, 转为动态视图查找

                //这里要不要把处理过的路径缓存起来，如果再次遇到相同的路径需要动态处理的则可以直接从缓存中获取视图文件名称
                var requestPath = context.HttpContext.Request.Path.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(requestPath)) //只有请求路径不为空时进行动态处理                {
                    int index = requestPath.IndexOf('?');                    if (index > 0)                    {                        requestPath = requestPath.Replace(context.HttpContext.Request.QueryString.ToString(), "");//去掉url参数                    }                    requestPath = requestPath.TrimStart('/').ToLower();                    List<string> paths = new List<string>(requestPath.Split('/'));
                    string viewName = "";                    string app = paths[0];                    string ctr = paths.Count > 1 ? paths[1] : "";

                    //根据路径构造视图名称 
                    //匹配 /app 路径, 在后面默认加上index
                    if (paths.Count == 1)                    {                        paths.Add("index");                    }                    paths.Remove(app);//移除app的名称

                    //匹配 /app/*************后面的路径构造成视图文件名称, /app/后面可以接任意长度路径                                          
                    viewName = string.Format("~/apps/{0}/template/page/{1}", app, string.Join("_", paths));

                    //修改成访问的动态视图 Controller 和 action
                    var routeData = new RouteData(context.RouteData);

                    //if (routeData.Values.ContainsKey(app)) routeData.Values.Remove("app");
                    routeData.Values.Clear();                    routeData.Values.Add("app", "base");                    //if (routeData.Values.ContainsKey("controller")) routeData.Values.Remove("controller");                    routeData.Values.Add("controller", "common");                    //if (routeData.Values.ContainsKey("action")) routeData.Values.Remove("action");                    routeData.Values.Add("action", "dynamic");                    //routeData.Values.Add("id", id);
                    context.HttpContext.Items.Add("__appname__", app);                    context.HttpContext.Items.Add("__check__", ctr);                    context.HttpContext.Items.Add("__dynamicviewname__", viewName);                    context.RouteData = routeData; //覆盖原来要访问的controller 和 action
                    //再找一次(这次是找动态的视图controoller 和 action)                    actionDescriptor = await _actionSelector.SelectAsync(context);
                }
            }

            // Replacing the route data allows any code running here to dirty the route values or data-tokens
            // without affecting something upstream.
            var oldRouteData = context.RouteData;
            var newRouteData = new RouteData(oldRouteData);

            if (actionDescriptor.RouteValueDefaults != null)
            {
                foreach (var kvp in actionDescriptor.RouteValueDefaults)
                {
                    if (!newRouteData.Values.ContainsKey(kvp.Key))
                    {
                        newRouteData.Values.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            // Removing RouteGroup from RouteValues to simulate the result of conventional routing
            newRouteData.Values.Remove(AttributeRouting.RouteGroupKey);

            try
            {
                context.RouteData = newRouteData;

                _diagnosticSource.BeforeAction(actionDescriptor, context.HttpContext, context.RouteData);
                await InvokeActionAsync(context, actionDescriptor);//执行 action 方法
                context.IsHandled = true;

                //清除路由信息？貌似会带到下次请求，如果下次请求的路径比上次短，那么下次的路由信息将会出现脏信息
                context.RouteData.Values.Clear();  //這裡的值不能清除啊，有可能還要在內部的部分也用到
                //清楚保存的临时信息
                context.HttpContext.Items.Remove("__appname__");                context.HttpContext.Items.Remove("__check__");                context.HttpContext.Items.Remove("__dynamicviewname__");

            }
            catch (Exception ex)
            {
                Debug.WriteLine("===========excute the action exception：" + ex.Message);
            }
            finally
            {
                _diagnosticSource.AfterAction(actionDescriptor, context.HttpContext, context.RouteData);

                if (!context.IsHandled)
                {
                    context.RouteData = oldRouteData;
                }
            }
        }

        private Task InvokeActionAsync(RouteContext context, ActionDescriptor actionDescriptor)
        {
            var actionContext = new ActionContext(context.HttpContext, context.RouteData, actionDescriptor);
            _actionContextAccessor.ActionContext = actionContext;

            var invoker = _actionInvokerFactory.CreateInvoker(actionContext);
            if (invoker == null)
            {
                throw new InvalidOperationException("action excute exception by "+ actionDescriptor.Name);
                //throw new InvalidOperationException(
                //    Resources.FormatActionInvokerFactory_CouldNotCreateInvoker(
                //        actionDescriptor.DisplayName));
            }

            return invoker.InvokeAsync();
        }

        private void EnsureServices(HttpContext context)
        {
            if (_actionContextAccessor == null)
            {
                _actionContextAccessor = context.RequestServices.GetRequiredService<IActionContextAccessor>();
            }

            if (_actionInvokerFactory == null)
            {
                _actionInvokerFactory = context.RequestServices.GetRequiredService<IActionInvokerFactory>();
            }

            if (_actionSelector == null)
            {
                _actionSelector = context.RequestServices.GetRequiredService<IActionSelector>();
            }

            if (_logger == null)
            {
                var factory = context.RequestServices.GetRequiredService<ILoggerFactory>();
                _logger = factory.CreateLogger<AppRouteHandler>();
            }
            
            if (_diagnosticSource == null)
            {
                _diagnosticSource = context.RequestServices.GetRequiredService<DiagnosticSource>();
            }
        }
    }
}
