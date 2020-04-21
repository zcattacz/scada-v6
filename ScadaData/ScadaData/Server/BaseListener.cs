﻿/*
 * Copyright 2020 Mikhail Shiryaev
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
 * Module   : ScadaData
 * Summary  : Represents the base class for TCP listeners which waits for client connections
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2020
 * Modified : 2020
 */

using Scada.Log;
using Scada.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Scada.Server
{
    /// <summary>
    /// Represents the base class for TCP listeners which waits for client connections.
    /// <para>Представляет базовый класс TCP-прослушивателей, которые ожидают подключения клиентов.</para>
    /// </summary>
    public abstract class BaseListener
    {
        /// <summary>
        /// The maximum number of client sessions.
        /// </summary>
        protected const int MaxSessionCount = 100;
        /// <summary>
        /// The maximum number of attempts to get a unique session ID.
        /// </summary>
        private const int MaxGetSessionIDAttempts = 100;
        /// <summary>
        /// The period of disconnection of inactive clients.
        /// </summary>
        private static readonly TimeSpan DisconnectPeriod = TimeSpan.FromSeconds(5);
        /// <summary>
        /// The time after which an inactive client is disconnected.
        /// </summary>
        protected static readonly TimeSpan ClientLifetime = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The listener options.
        /// </summary>
        protected readonly ListenerOptions listenerOptions;
        /// <summary>
        /// The listener log.
        /// </summary>
        protected readonly ILog log;

        /// <summary>
        /// Listens for TCP connections.
        /// </summary>
        protected TcpListener tcpListener;
        /// <summary>
        /// The connected clients accessed by session ID.
        /// </summary>
        protected ConcurrentDictionary<long, ConnectedClient> clients;
        /// <summary>
        /// The working thread of the listener.
        /// </summary>
        protected Thread thread;
        /// <summary>
        /// Necessary to stop the thread.
        /// </summary>
        protected volatile bool terminated;


        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public BaseListener(ListenerOptions listenerOptions, ILog log)
        {
            this.listenerOptions = listenerOptions ?? throw new ArgumentNullException("listenerOptions");
            this.log = log ?? throw new ArgumentNullException("log");

            tcpListener = null;
            clients = null;
            thread = null;
            terminated = false;
        }


        /// <summary>
        /// Work cycle running in a separate thread.
        /// </summary>
        protected void Execute()
        {
            DateTime disconnectDT = DateTime.MinValue;

            while (!terminated)
            {
                try
                {
                    // connect new clients
                    while (tcpListener.Pending() && !terminated && 
                        CreateSession(out ConnectedClient client))
                    {
                        TcpClient tcpClient = tcpListener.AcceptTcpClient();
                        tcpClient.SendTimeout = listenerOptions.Timeout;
                        tcpClient.ReceiveTimeout = listenerOptions.Timeout;

                        Thread clientTread = new Thread(ClientExecute);
                        client.Init(tcpClient, clientTread);
                        log.WriteAction(string.Format(Locale.IsRussian ?
                            "Клиент {0} подключился" :
                            "Client {0} connected", client.Address));
                        clientTread.Start(client);
                    }

                    // disconnect inactive clients
                    DateTime utcNow = DateTime.UtcNow;
                    if (utcNow - disconnectDT >= DisconnectPeriod)
                    {
                        disconnectDT = utcNow;
                        RemoveInactiveSessions();
                    }

                    Thread.Sleep(ScadaUtils.ThreadDelay);
                }
                catch (Exception ex)
                {
                    log.WriteException(ex, Locale.IsRussian ?
                        "Ошибка в цикле работы прослушивателя" :
                        "Error in the listener work cycle");
                    Thread.Sleep(ScadaUtils.ThreadDelay);
                }
            }
        }

        /// <summary>
        /// Work cycle of a client.
        /// </summary>
        protected void ClientExecute(object clientArg)
        {
            ConnectedClient client = (ConnectedClient)clientArg;

            while (!client.Terminated)
            {
                try
                {
                    if (client.TcpClient.Available > 0)
                    {
                        client.RegisterActivity();
                        ReceiveData(client);
                    }

                    Thread.Sleep(ScadaUtils.ThreadDelay);
                }
                catch (Exception ex)
                {
                    log.WriteException(ex, Locale.IsRussian ?
                        "Ошибка в цикле работы клиента {0}" :
                        "Error in the client {0} work cycle", client.Address);
                    Thread.Sleep(ScadaUtils.ThreadDelay);
                }
            }
        }

        /// <summary>
        /// Creates a new session.
        /// </summary>
        protected bool CreateSession(out ConnectedClient client)
        {
            long sessionID = 0;
            bool sessionOK = false;
            client = null;

            if (clients.Count < MaxSessionCount)
            {
                client = new ConnectedClient();
                sessionID = ScadaUtils.GetRandomLong();
                int attemptNum = 0;
                bool duplicated;

                while (duplicated = sessionID == 0 ||
                    ++attemptNum <= MaxGetSessionIDAttempts && !clients.TryAdd(sessionID, client))
                {
                    sessionID = ScadaUtils.GetRandomLong();
                }

                sessionOK = !duplicated;
            }

            if (sessionOK)
            {
                log.WriteAction(string.Format(Locale.IsRussian ?
                    "Создана сессия с ид. {0}" :
                    "Session with ID {0} created", sessionID));
                client.SessionID = sessionID;
                return true;
            }
            else
            {
                log.WriteError(Locale.IsRussian ?
                    "Не удалось создать сессию" :
                    "Unable to create session");
                client = null;
                return false;
            }
        }

        /// <summary>
        /// Removes the inactive sessions.
        /// </summary>
        protected void RemoveInactiveSessions()
        {
            DateTime utcNow = DateTime.UtcNow;
            List<long> keysToRemove = new List<long>();

            foreach (KeyValuePair<long, ConnectedClient> pair in clients)
            {
                if (utcNow - pair.Value.ActivityTime > ClientLifetime)
                    keysToRemove.Add(pair.Key);
            }

            foreach (long key in keysToRemove)
            {
                if (clients.TryRemove(key, out ConnectedClient value))
                    DisconnectClient(value);
            }
        }

        /// <summary>
        /// Disconnects the client.
        /// </summary>
        protected void DisconnectClient(ConnectedClient client)
        {
            try
            {
                client.Terminated = true;
                client.JoinThread();
                client.Disconnect();

                log.WriteAction(string.Format(Locale.IsRussian ?
                    "Клиент {0} отключился" :
                    "Client {0} disconnected", client.Address));
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Locale.IsRussian ?
                    "Ошибка при отключении клиента {0}" :
                    "Error disconnecting client {0}", client.Address);
            }
        }

        /// <summary>
        /// Disconnects all clients.
        /// </summary>
        protected void DisconnectAll()
        {
            try
            {
                ICollection<ConnectedClient> clientList = clients.Values; // make a snapshot

                foreach (ConnectedClient client in clientList)
                {
                    client.Terminated = true;
                }

                foreach (ConnectedClient client in clientList)
                {
                    client.JoinThread();
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Locale.IsRussian ?
                    "Ошибка при отключении всех клиентов" :
                    "Error disconnecting all clients");
            }
        }

        /// <summary>
        /// Receives data from the client.
        /// </summary>
        protected void ReceiveData(ConnectedClient client)
        {
            try
            {
                bool formatError = true;
                string errDescr = "";
                byte[] inBuf = client.InBuf;
                int bytesRead = client.NetStream.Read(inBuf, 0, ProtocolUtils.HeaderLength);

                if (bytesRead == ProtocolUtils.HeaderLength)
                {
                    DataPacket request = new DataPacket
                    {
                        TransactionID = BitConverter.ToUInt16(inBuf, 0),
                        DataLength = BitConverter.ToInt32(inBuf, 2),
                        SessionID = BitConverter.ToInt64(inBuf, 6),
                        FunctionID = BitConverter.ToUInt16(inBuf, 14),
                        Buffer = inBuf
                    };

                    if (request.DataLength + 6 > inBuf.Length)
                    {
                        errDescr = Locale.IsRussian ?
                            "длина данных слишком велика" :
                            "data length is too big";
                    }
                    else if (!(request.SessionID == 0 && request.FunctionID == FunctionID.GetSessionInfo ||
                        request.SessionID != 0 && request.SessionID == client.SessionID))
                    {
                        errDescr = Locale.IsRussian ?
                            "неверный идентификатор сессии" :
                            "incorrect session ID";
                    }
                    else
                    {
                        // read the rest of the data
                        int bytesToRead = request.DataLength - 8;
                        bytesRead = bytesToRead > 0 ? client.ReadData(ProtocolUtils.HeaderLength, bytesToRead) : 0;

                        if (bytesRead == bytesToRead)
                        {
                            formatError = false;
                            ProcessRequest(client, request);
                        }
                        else
                        {
                            errDescr = Locale.IsRussian ?
                                "не удалось прочитать все данные" :
                                "unable to read all data";
                        }
                    }
                }

                if (formatError)
                {
                    log.WriteError(string.Format(Locale.IsRussian ?
                        "Некорректный формат данных, полученных от клиента {0}: {1}" :
                        "Incorrect format of data received from the client {0}: {1}",
                        client.Address, errDescr));

                    // clear the stream by receiving available data
                    if (client.NetStream.DataAvailable)
                        client.NetStream.Read(inBuf, 0, inBuf.Length);
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Locale.IsRussian ?
                    "Ошибка при приёме данных от клиента {0}" :
                    "Error receiving data from the client {0}", client.Address);
            }
        }

        /// <summary>
        /// Processes an incoming request already stored in the client input buffer.
        /// </summary>
        protected void ProcessRequest(ConnectedClient client, DataPacket request)
        {
            // process standard request
            ResponsePacket response = null; // response to send
            bool handled = true;            // request was handled

            switch (request.FunctionID)
            {
                case FunctionID.GetSessionInfo:
                    GetSessionInfo(client, request, out response);
                    break;

                case FunctionID.Login:
                    Login(client, request, out response);
                    break;

                default:
                    handled = false;
                    break;
            }

            // process custom request
            if (!handled)
            {
                ProcessCustomRequest(client, request, out response, out handled);

                if (!handled)
                {
                    response = new ResponsePacket(request, client.OutBuf);
                    response.SetError(ErrorCode.IllegalFunction);
                }
            }

            // send response
            if (response != null)
                client.NetStream.Write(response.Buffer, 0, response.BufferLength);
        }

        /// <summary>
        /// Gets the information about the current session.
        /// </summary>
        protected void GetSessionInfo(ConnectedClient client, DataPacket request, out ResponsePacket response)
        {
            response = new ResponsePacket(request, client.OutBuf) { SessionID = client.SessionID };
            ProtocolUtils.CopyString(GetServerName(), response.Buffer, ProtocolUtils.ArgumentIndex, out int lenght);
            response.ArgumentLength = lenght;
            response.Encode();
        }

        /// <summary>
        /// Performs a login function.
        /// </summary>
        protected void Login(ConnectedClient client, DataPacket request, out ResponsePacket response)
        {
            response = new ResponsePacket(request, client.OutBuf);
        }

        /// <summary>
        /// Gets the server name and version.
        /// </summary>
        protected abstract string GetServerName();

        /// <summary>
        /// Validates the username and password.
        /// </summary>
        protected virtual bool ValidateUser(string username, string password, out string errMsg)
        {
            errMsg = "";
            return true;
        }

        /// <summary>
        /// Processes an incoming request by a derived class.
        /// </summary>
        protected virtual void ProcessCustomRequest(ConnectedClient client, DataPacket request, 
            out ResponsePacket response, out bool handled)
        {
            response = null;
            handled = false;
        }


        /// <summary>
        /// Starts listening.
        /// </summary>
        public bool Start()
        {
            try
            {
                if (thread == null)
                {
                    log.WriteAction(Locale.IsRussian ?
                        "Запуск прослушивателя" :
                        "Start listener");

                    tcpListener = new TcpListener(IPAddress.Any, listenerOptions.Port);
                    tcpListener.Start();

                    clients = new ConcurrentDictionary<long, ConnectedClient>();
                    terminated = false;
                    thread = new Thread(Execute);
                    thread.Start();
                }
                else
                {
                    log.WriteAction(Locale.IsRussian ?
                        "Прослушиватель уже запущен" :
                        "Listener is already started");
                }

                return true;
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Locale.IsRussian ?
                    "Ошибка при запуске прослушивателя" :
                    "Error starting listener");
                return false;
            }
        }

        /// <summary>
        /// Stops listening.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (thread != null)
                {
                    log.WriteAction(Locale.IsRussian ?
                        "Остановка прослушивателя" :
                        "Stop listener");

                    terminated = true;
                    thread.Join();
                    tcpListener.Stop();
                    DisconnectAll();

                    tcpListener = null;
                    clients = null;
                    thread = null;

                    log.WriteAction(Locale.IsRussian ?
                        "Прослушиватель остановлен" :
                        "Listener is stopped");
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Locale.IsRussian ?
                    "Ошибка при остановке прослушивателя" :
                    "Error stopping listener");
            }
        }
    }
}
