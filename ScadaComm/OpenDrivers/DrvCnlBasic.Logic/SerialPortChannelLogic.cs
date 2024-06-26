﻿// Copyright (c) Rapid Software LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Scada.Comm.Channels;
using Scada.Comm.Config;
using Scada.Comm.Devices;
using Scada.Lang;
using System;
using System.IO.Ports;
using System.Threading;

namespace Scada.Comm.Drivers.DrvCnlBasic.Logic
{
    /// <summary>
    /// Implements the serial port channel logic.
    /// <para>Реализует логику канала последовательного порта.</para>
    /// </summary>
    internal class SerialPortChannelLogic : ChannelLogic
    {
        /// <summary>
        /// The maximum time allowed to elapse before the arrival of the next byte, ms.
        /// </summary>
        protected const int ReadIntervalTimeout = 100;

        protected readonly SerialPortChannelOptions options;    // the channel options
        protected readonly IncomingRequestArgs requestArgs; // the incoming request arguments

        protected SerialPortConnection serialConn; // the serial connection
        protected Thread thread;               // the thread for receiving data in slave mode
        protected volatile bool terminated;    // necessary to stop the thread


        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public SerialPortChannelLogic(ILineContext lineContext, ChannelConfig channelConfig)
            : base(lineContext, channelConfig)
        {
            options = new SerialPortChannelOptions(channelConfig.CustomOptions);
            requestArgs = new IncomingRequestArgs();

            serialConn = null;
            thread = null;
            terminated = false;
        }


        /// <summary>
        /// Gets the channel behavior.
        /// </summary>
        protected override ChannelBehavior Behavior
        {
            get
            {
                return options.Behavior;
            }
        }

        /// <summary>
        /// Gets the current communication channel status as text.
        /// </summary>
        public override string StatusText
        {
            get
            {
                if (Locale.IsRussian)
                {
                    return serialConn == null ?
                        "Последовательный порт" :
                        serialConn.SerialPort.PortName + (serialConn.Connected ? ", открыт" : ", закрыт");
                }
                else
                {
                    return serialConn == null ?
                        "Serial port" :
                        serialConn.SerialPort.PortName + (serialConn.Connected ? ", open" : ", closed");
                }
            }
        }


        /// <summary>
        /// Opens the serial port.
        /// </summary>
        protected void OpenSerialPort()
        {
            Log.WriteLine();
            Log.WriteAction(Locale.IsRussian ?
                "Открытие последовательного порта {0}" :
                "Open serial port {0}", serialConn.SerialPort.PortName);
            serialConn.Open();
        }

        /// <summary>
        /// Closes the serial port.
        /// </summary>
        protected void CloseSerialPort()
        {
            Log.WriteLine();
            Log.WriteAction(Locale.IsRussian ?
                "Закрытие последовательного порта {0}" :
                "Close serial port {0}", serialConn.SerialPort.PortName);
            serialConn.Close();
        }

        /// <summary>
        /// Listens to the serial port for incoming data.
        /// </summary>
        /// <remarks>This method works in a separate thread. It is used on Linux.</remarks>
        protected void ListenSerialPort()
        {
            while (!terminated)
            {
                try
                {
                    lock (serialConn)
                    {
                        if (serialConn.Connected && serialConn.SerialPort.BytesToRead > 0 &&
                            !ReceiveIncomingRequest(LineContext.SelectDevices(), serialConn, requestArgs))
                        {
                            serialConn.DiscardInBuffer();
                        }
                    }

                    Thread.Sleep(SlaveThreadDelay);
                }
                catch (Exception ex)
                {
                    Log.WriteError(ex, Locale.IsRussian ?
                        "Ошибка при прослушивании последовательного порта" :
                        "Error listening to the serial port");
                    Thread.Sleep(ScadaUtils.ThreadDelay);
                }
            }
        }


        /// <summary>
        /// Makes the communication channel ready for operating.
        /// </summary>
        public override void MakeReady()
        {
            CheckBehaviorSupport();

            serialConn = new SerialPortConnection(Log, new SerialPort(
                options.PortName, options.BaudRate, options.Parity, options.DataBits, options.StopBits)
            {
                DtrEnable = options.DtrEnable,
                RtsEnable = options.RtsEnable
            });

            SetDeviceConnection(serialConn);
        }

        /// <summary>
        /// Starts the communication channel.
        /// </summary>
        public override void Start()
        {
            OpenSerialPort();

            if (Behavior == ChannelBehavior.Slave)
            {
                terminated = false;
                thread = new Thread(ListenSerialPort);
                thread.Start();
            }
        }

        /// <summary>
        /// Stops the communication channel.
        /// </summary>
        public override void Stop()
        {
            if (thread != null)
            {
                terminated = true;
                thread.Join();
                thread = null;
            }

            SetDeviceConnection(null);
            CloseSerialPort();
        }

        /// <summary>
        /// Performs actions before polling the specified device.
        /// </summary>
        public override void BeforeSession(DeviceLogic deviceLogic)
        {
            if (Behavior == ChannelBehavior.Slave)
                Monitor.Enter(serialConn.SyncRoot);

            if (!serialConn.Connected)
                OpenSerialPort();
        }

        /// <summary>
        /// Performs actions after polling the specified device.
        /// </summary>
        public override void AfterSession(DeviceLogic deviceLogic)
        {
            if (serialConn.Connected && serialConn.WriteError)
                CloseSerialPort();

            if (Behavior == ChannelBehavior.Slave)
                Monitor.Exit(serialConn.SyncRoot);
        }
    }
}
