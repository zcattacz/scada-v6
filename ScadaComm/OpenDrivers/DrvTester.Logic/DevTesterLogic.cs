﻿// Copyright (c) Rapid Software LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Scada.Comm.Config;
using Scada.Comm.Devices;
using Scada.Comm.Lang;
using Scada.Data.Models;
using Scada.Lang;
using System;
using System.Diagnostics;

namespace Scada.Comm.Drivers.DrvTester.Logic
{
    /// <summary>
    /// Implements the device logic.
    /// <para>Реализует логику КП.</para>
    /// </summary>
    internal class DevTesterLogic : DeviceLogic
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public DevTesterLogic(ICommContext commContext, ILineContext lineContext, DeviceConfig deviceConfig)
            : base(commContext, lineContext, deviceConfig)
        {
            CanSendCommands = true;
        }


        /// <summary>
        /// Performs actions after setting the connection.
        /// </summary>
        public override void OnConnectionSet()
        {
            if (Connection != null)
                Connection.NewLine = "\x0D";
        }

        /// <summary>
        /// Performs a communication session.
        /// </summary>
        public override void Session()
        {
            base.Session();
            FinishRequest();
            FinishSession();
        }

        /// <summary>
        /// Sends the telecontrol command.
        /// </summary>
        public override void SendCommand(TeleCommand cmd)
        {
            base.SendCommand(cmd);

            if (cmd.CmdCode == "SendBin" || cmd.CmdNum == 1)
            {
                byte[] buffer = cmd.CmdData ?? Array.Empty<byte>();
                Stopwatch stopwatch = Stopwatch.StartNew();
                Connection.Write(buffer, 0, buffer.Length);
                stopwatch.Stop();

                Log.WriteLine(Locale.IsRussian ?
                    "Отправлено за {0} мс" :
                    "Sent in {0} ms", stopwatch.ElapsedMilliseconds);
            }
            else if (cmd.CmdCode == "SendStr" || cmd.CmdNum == 2)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                Connection.WriteLine(cmd.GetCmdDataString());
                stopwatch.Stop();

                Log.WriteLine(Locale.IsRussian ?
                    "Отправлено за {0} мс" :
                    "Sent in {0} ms", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                LastRequestOK = false;
                Log.WriteLine(CommPhrases.InvalidCommand);
            }

            FinishCommand();
        }
    }
}