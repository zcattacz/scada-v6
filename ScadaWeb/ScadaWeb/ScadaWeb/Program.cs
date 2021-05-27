/*
 * Copyright 2021 Rapid Software LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : Webstation Application
 * Summary  : The Webstation application
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2021
 * Modified : 2021
 */

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scada.Config;
using Scada.Lang;
using Scada.Log;
using Scada.Web.Code;
using Scada.Web.Lang;
using System;
using System.IO;
using System.Reflection;

namespace Scada.Web
{
    /// <summary>
    /// The Webstation application.
    /// <para>Приложение Вебстанция.</para>
    /// </summary>
    public class Program
    {
        private static WebContext webContext; // the application context
        private static ILog log;              // the application log


        /// <summary>
        /// Initializes the application context.
        /// </summary>
        private static void InitContext()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            webContext = new WebContext();
            webContext.Init(exeDir);

            log = webContext.Log;
            log.WriteBreak();

            if (!Locale.LoadCulture(Path.Combine(exeDir, "..", "Config", InstanceConfig.DefaultFileName),
                out string errMsg))
            {
                log.WriteError(errMsg);
            }

            webContext.LoadConfig();
        }

        /// <summary>
        /// Localizes the application.
        /// </summary>
        private static void LocalizeApp()
        {
            if (webContext != null)
            {
                if (!Locale.LoadDictionaries(webContext.AppDirs.LangDir, "ScadaCommon", out string errMsg))
                    log.WriteError(errMsg);

                if (!Locale.LoadDictionaries(webContext.AppDirs.LangDir, "ScadaWeb", out errMsg))
                    log.WriteError(errMsg);

                CommonPhrases.Init();
                WebPhrases.Init();
            }
        }

        /// <summary>
        /// Writes an application start message to the log.
        /// </summary>
        private static void LogStart()
        {
            log.WriteAction(Locale.IsRussian ?
                "Вебстанция {0} запущена" :
                "Webstation {0} started", WebUtils.AppVersion);
        }

        /// <summary>
        /// Writes an application stop message to the log.
        /// </summary>
        private static void LogStop()
        {
            log.WriteAction(Locale.IsRussian ?
                "Вебстанция остановлена" :
                "Webstation is stopped");
            log.WriteBreak();
        }


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            InitContext();
            LogStart();
            LocalizeApp();
            CreateHostBuilder(args).Build().Run();
            LogStop();
        }

        /// <summary>
        /// Initializes a new instance of the host builder.
        /// </summary>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup(context => new Startup(context.Configuration, webContext));
                });
    }
}
