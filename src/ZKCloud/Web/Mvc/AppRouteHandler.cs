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

namespace ZKCloud.Web.Mvc
{
    /// <summary>
    /// ϵͳ·�ɴ������
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
            { //û��ֱ���ҵ���Ӧ��controller �µ� action, תΪ��̬��ͼ����
              
                //����Ҫ��Ҫ�Ѵ������·����������������ٴ�������ͬ��·����Ҫ��̬����������ֱ�Ӵӻ����л�ȡ��ͼ�ļ�����
                var requestPath = context.HttpContext.Request.Path.Value ?? string.Empty;
                int index = requestPath.IndexOf('?');
                if (index > 0)
                {
                    requestPath = requestPath.Replace(context.HttpContext.Request.QueryString.ToString(), "");//ȥ��url����
                }

                requestPath = requestPath.TrimStart('/').ToLower();
                string[] paths = requestPath.Split('/');
                if (paths.Length == 2 && paths[1].Equals("index") || paths.Length >= 3) //����ƥ��  /app/index  ��  /app/controller/action����·��
                {
                    string viewName = "";
                    string app = paths[0];
                    string ctr = paths[1];
                    string act = paths.Length > 2 ? paths[2] : "";
                    string id = paths.Length > 3 ? paths[3] : "";
                    //����·��������ͼ����
                    if (paths.Length == 3)
                    {
                        viewName = string.Format("~/apps/{0}/template/{1}_{2}", app, ctr, act);
                    }
                    else if (paths[1].Equals("index"))
                    {
                        viewName = string.Format("~/apps/{0}/template/{1}", app, ctr);
                    }


                    //�޸ĳɷ��ʵĶ�̬��ͼ Controller �� action
                    var routeData = new RouteData();
                    routeData.Values.Add("app", "demo01");
                    routeData.Values.Add("controller", "test");
                    routeData.Values.Add("action", "dynamic");
                    routeData.Values.Add("id", id);

                    context.HttpContext.Items.Add("__check__", ctr);
                    context.HttpContext.Items.Add("__dynamicviewname__", viewName);
                    context.RouteData = routeData; //����ԭ��Ҫ���ʵ�controller �� action
                    
                    //����һ��(������Ҷ�̬����ͼcontrooller �� action)
                    actionDescriptor = await _actionSelector.SelectAsync(context);
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
                await InvokeActionAsync(context, actionDescriptor);//ִ�� action ����
                context.IsHandled = true;

                //���·����Ϣ��ò�ƻ�����´���������´������·�����ϴζ̣���ô�´ε�·����Ϣ�����������Ϣ
                context.RouteData.Values.Clear();                
            }
            catch(Exception ex)
            {
                Debug.WriteLine("===========excute the action exception��" + ex.Message);
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
