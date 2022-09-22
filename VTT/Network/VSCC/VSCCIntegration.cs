namespace VTT.Network.VSCC
{
    using NetCoreServer;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    public class VSCCIntegration
    {
        public IntegrationClient SocketClient { get; set; }

        public bool Running => this.SocketClient != null;
        public bool IsConnected => this.Running && this.SocketClient.Connected;

        public List<IIntegrator> ServerCommandParsers { get; } = new List<IIntegrator>();

        public VSCCIntegration()
        {
            this.ServerCommandParsers.Add(new IntegratorStop());
            this.ServerCommandParsers.Add(new IntegratorPoll());
            this.ServerCommandParsers.Add(new IntegratorRoll());
            this.ServerCommandParsers.Add(new IntegratorMessage());
            this.ServerCommandParsers.Add(new IntegratorMacro());
        }

        public void Create()
        {
            Client.Instance.Logger.Log(VTT.Util.LogLevel.Info, "Starting VSCC Integration");
            this.SocketClient = new IntegrationClient(IPAddress.Loopback, 23521) { Container = this };
            this.SocketClient.ConnectAsync();
        }

        public void HandleMessage(string msgIn)
        {
            Client.Instance.Logger.Log(VTT.Util.LogLevel.Debug, "Got VSCC Integration message " + msgIn);
            if (Client.Instance.NetClient == null || !Client.Instance.NetClient.IsConnected)
            {
                return;
            }

            try
            {
                this.HandleServerCommand(JObject.Parse(msgIn), msgIn);
            }
            catch (JsonException e)
            {
                Client.Instance.Logger.Log(VTT.Util.LogLevel.Error, "Got malformed json from server!");
                Client.Instance.Logger.Exception(VTT.Util.LogLevel.Error, e);
            }
        }

        public void HandleServerCommand(JObject json, string msg)
        {
            if (json.ContainsKey("type"))
            {
                string t = json["type"].ToObject<string>().ToLower();
                foreach (IIntegrator integrator in this.ServerCommandParsers)
                {
                    if (integrator.Accepts(t))
                    {
                        Client.Instance.Logger.Log(VTT.Util.LogLevel.Debug, "Using integrator " + integrator.GetType().Name);
                        if (integrator.Process(json, msg, this))
                        {
                            break;
                        }
                    }
                }

                this.SendError(7, "Unknown packet type.");
            }
            else
            {
                this.SendError(2, "No packet type specified.");
            }
        }

        /* Error code definitions:
         * -1: Catastrophical client error, terminate the connection. 
         * 0: No error.
         * 1: Invalid JSON sent/received.
         * 2: Malformed data.
         * 3: Invalid argument type supplied.
         * 4: Operation not supported.
         * 5: Operation not implemented.
         * 6: Permission error.
         * 7: Unknown packet type.
         * 127: Undefined client error
         */
        public void SendError(int code, string message)
        {
            JObject data = new JObject
            {
                ["code"] = code,
                ["message"] = message
            };

            this.Send(data);
        }

        public void Send(JObject jo) => this.Send(jo.ToString());

        public void Send(string msgOut)
        {
            if (this.SocketClient != null && this.SocketClient.IsConnected)
            {
                this.SocketClient.SendTextAsync(msgOut);
            }
        }

        public void Destroy()
        {
            Client.Instance.Logger.Log(VTT.Util.LogLevel.Info, "VSCC Integration destroyed");
            this.SocketClient.Disconnect();
            this.SocketClient.Dispose();
            this.SocketClient = null;
        }
    }

    public class IntegrationClient : WsClient
    {
        public VSCCIntegration Container { get; set; }
        public bool Connected { get; private set; }

        public IntegrationClient(IPAddress address, int port) : base(address, port)
        {
        }

        protected override void OnConnected()
        {
            HttpRequest request = new HttpRequest();
            request.SetBegin("GET", "/");
            request.SetHeader("Host", "localhost");
            request.SetHeader("Accept", "*/*");
            request.SetHeader("Accept-Encoding", "gzip, deflate, br");
            request.SetHeader("Cache-Control", "no-cache");
            request.SetHeader("Connection", "keep-alive, Upgrade");
            request.SetHeader("Origin", "http://localhost");
            request.SetHeader("Pragma", "no-cache");
            request.SetHeader("Sec-Fetch-Dest", "websocket");
            request.SetHeader("Sec-Fetch-Mode", "websocket");
            request.SetHeader("Sec-Websocket-Extensions", "permessage-deflate");
            request.SetHeader("Sec-Websocket-Key", Convert.ToBase64String(WsNonce));
            request.SetHeader("Sec-Websocket-Version", "13");
            request.SetHeader("Upgrade", "websocket");
            request.SetBody();
            this.SendRequestAsync(request);
        }

        protected override void OnDisconnected() => this.Container.Destroy();

        protected override void OnError(SocketError error)
        {
            Client.Instance.Logger.Log(VTT.Util.LogLevel.Error, "VSCC integration socket error " + error);
            this.Container.Destroy();
        }

        public override void OnWsError(SocketError error)
        {
            Client.Instance.Logger.Log(VTT.Util.LogLevel.Error, "VSCC integration socket error " + error);
            this.Container.Destroy();
        }

        public override void OnWsError(string error)
        {
            Client.Instance.Logger.Log(VTT.Util.LogLevel.Error, "VSCC integration socket error " + error);
            this.Container.Destroy();
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            string text = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            this.Container.HandleMessage(text);
        }

        public override void OnWsConnected(HttpResponse response)
        {
            base.OnWsConnected(response);
            this.Connected = true;
        }
    }
}
