namespace VTT.Network
{
    using NetCoreServer;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using VTT.Network.HTTPAPI;
    using VTT.Util;

    public class HTTPAPIEndpoint : IWebSocket
    {
        public Server Container { get; }
        public TcpSession Session { get; }

        static HTTPAPIEndpoint()
        {
            MethodInfo requestIsPendingHeader = typeof(HttpRequest).GetMethod("IsPendingHeader", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo requestReceiveHeader = typeof(HttpRequest).GetMethod("ReceiveHeader", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo requestReceiveBody = typeof(HttpRequest).GetMethod("ReceiveBody", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo websocketSendBuffer = typeof(WebSocket).GetField("WsSendBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
            ParameterExpression requestParam = Expression.Parameter(typeof(HttpRequest), "instance");
            HttpRequest_IsPendingHeader = Expression.Lambda<Func<HttpRequest, bool>>(
                Expression.Call(requestParam, requestIsPendingHeader),
                requestParam
            ).Compile();

            ParameterExpression bufferParam = Expression.Parameter(typeof(byte[]), "buffer");
            ParameterExpression offsetParam = Expression.Parameter(typeof(int), "offset");
            ParameterExpression sizeParam = Expression.Parameter(typeof(int), "size");
            HttpRequest_ReceiveHeader = Expression.Lambda<Func<HttpRequest, byte[], int, int, bool>>(
                Expression.Call(requestParam, requestReceiveHeader, bufferParam, offsetParam, sizeParam),
                requestParam, bufferParam, offsetParam, sizeParam
            ).Compile();

            HttpRequest_ReceiveBody = Expression.Lambda<Func<HttpRequest, byte[], int, int, bool>>(
                Expression.Call(requestParam, requestReceiveBody, bufferParam, offsetParam, sizeParam),
                requestParam, bufferParam, offsetParam, sizeParam
            ).Compile();

            ParameterExpression wsParam = Expression.Parameter(typeof(WebSocket), "ws");
            WebSocket_GetBuffer = Expression.Lambda<Func<WebSocket, NetCoreServer.Buffer>>(
                Expression.Field(wsParam, websocketSendBuffer),
                wsParam
            ).Compile();
        }
        
        private HttpRequest _request;
        private HttpResponse _response;
        private WebSocket _ws;
        private bool _isWS;

        public HTTPAPIEndpoint(TcpServer server, TcpSession session)
        {
            this._request = new HttpRequest();
            this._response = new HttpResponse();
            this.Container = (Server)server;
            this.Session = session;
            this._ws = new WebSocket(this);
        }

        #region Reflection Delegates
        private static readonly Func<HttpRequest, bool> HttpRequest_IsPendingHeader;
        private static readonly Func<HttpRequest, byte[], int, int, bool> HttpRequest_ReceiveHeader;
        private static readonly Func<HttpRequest, byte[], int, int, bool> HttpRequest_ReceiveBody;
        private static readonly Func<WebSocket, NetCoreServer.Buffer> WebSocket_GetBuffer;
        #endregion

        public void OnReceived(byte[] buffer, long offset, long size)
        {
            if (!this._isWS)
            {
                if (HttpRequest_IsPendingHeader(this._request))
                {
                    if (HttpRequest_ReceiveHeader(this._request, buffer, (int)offset, (int)size))
                    {
                        this.OnHeader();
                    }

                    size = 0L;
                }

                if (this._request.IsErrorSet)
                {
                    this.Container.Logger.Log(LogLevel.Error, "Invalid HTTP request for HTTP API endpoint!");
                    this._request.Clear();
                    this.Session.Disconnect();
                }
                else if (HttpRequest_ReceiveBody(this._request, buffer, (int)offset, (int)size))
                {
                    this.OnBody();
                    this._request.Clear();
                }
                else if (this._request.IsErrorSet)
                {
                    this.Container.Logger.Log(LogLevel.Error, "Invalid HTTP request for HTTP API endpoint!");
                    this._request.Clear();
                    this.Session.Disconnect();
                }
            }
            else
            {
                this._ws.PrepareReceiveFrame(buffer, offset, size);
            }
        }

        private void OnHeader()
        {
        }

        private void OnBody()
        {
            if (this._ws.PerformServerUpgrade(this._request, this._response))
            {
                this._isWS = true;
            }
            else
            {
                if (string.Equals(this._request.Method, "get", StringComparison.InvariantCultureIgnoreCase))
                {
                    this._response.SetBegin(200);
                    this._response.SetHeader("Content-Language", "en-US");
                    this._response.SetHeader("Content-Type", "text/html; charset=utf-8");
                    this._response.SetHeader("Date", DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss") + " GMT");
                    this._response.SetBody(this.MakeAPIDocs());
                    this.Session.Send(this._response.Cache.Data, this._response.Cache.Offset, this._response.Cache.Size);
                    this._response.Clear();
                }
                else
                {
                    // API request
                    if (string.Equals(this._request.Method, "post", StringComparison.InvariantCultureIgnoreCase))
                    {
                        this.FindAuthorizedClientFor(this.FindHeader("Authorization"), out ClientInfo aclient, out ServerClient oclient);
                        string[] splt = this._request.Body.Split('&');
                        Dictionary<string, string> data = new();
                        foreach (string kv in splt)
                        {
                            string akv = kv.Replace("\\a", "&");
                            int eqIdx = akv.IndexOf('=');
                            if (eqIdx != -1)
                            {
                                data[akv.Substring(0, eqIdx)] = akv.Substring(eqIdx + 1);
                            }
                        }

                        this.HandleAPIRequest(aclient, oclient, data);
                    }
                }
            }
        }

        private bool FindAuthorizedClientFor(string auth, out ClientInfo ci, out ServerClient oc)
        {
            if (auth.StartsWith("basic ", StringComparison.InvariantCultureIgnoreCase))
            {
                string kv = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Substring(6)));
                string[] split = kv.Split(':');
                if (split.Length != 2)
                {
                    ci = null;
                    oc = null;
                    return false;
                }

                if (!Guid.TryParse(split[0], out Guid result))
                {
                    ci = null;
                    oc = null;
                    return false;
                }

                return this.IterateClientsForKV(result, Convert.FromBase64String(split[1]), out ci, out oc);
            }

            if (auth.StartsWith("auth ", StringComparison.InvariantCultureIgnoreCase))
            {
                return this.IterateClientsForKV(Guid.Empty, Convert.FromBase64String(auth.Substring(5)), out ci, out oc);
            }

            ci = null;
            oc = null;
            return false;
        }

        public bool IterateClientsForKV(Guid id, byte[] key, out ClientInfo ci, out ServerClient oc)
        {
            if (id.IsEmpty())
            {
                if (key == null || key.Length != 32)
                {
                    ci = null;
                    oc = null;
                    return false;
                }

                lock (this.Container.clientsLock)
                {
                    foreach (ClientInfo sc in this.Container.ClientInfos.Values)
                    {
                        if (key.SequenceEqual(sc.SessionAuthToken))
                        {
                            ci = sc;
                            this.Container.ClientsByID.TryGetValue(ci.ID, out oc);
                            return true;
                        }
                    }
                }

                ci = null;
                oc = null;
                return false;
            }

            if (this.Container.ClientInfos.TryGetValue(id, out ClientInfo sca) && sca.Secret.SequenceEqual(key))
            {
                ci = sca;
                this.Container.ClientsByID.TryGetValue(ci.ID, out oc);
                return true;
            }

            ci = null;
            oc = null;
            return false;
        }

        private string FindHeader(string hName)
        {
            for (int i = 0; i < this._request.Headers; ++i)
            {
                (string, string) h = this._request.Header(i);
                if (string.Equals(h.Item1, hName, StringComparison.OrdinalIgnoreCase))
                {
                    return h.Item2;
                }
            }

            return string.Empty;
        }

        private string MakeAPIDocs()
        {
            string basePage = IOVTT.ResourceToString("VTT.Embed.httpapidocs.html");
            if (basePage.Contains("#PRAGMA AUTOGEN_ENTRY"))
            {
                StringBuilder autogen = new StringBuilder();
                foreach (string doc in APIMethod.MethodDocs)
                {
                    autogen.AppendLine("<hr>");
                    autogen.Append(doc);
                }

                basePage = basePage.Replace("#PRAGMA AUTOGEN_ENTRY", autogen.ToString());
            }

            return basePage;
        }

        void IWebSocket.SendUpgrade(HttpResponse response)
        {
            this.Session.Send(this._response.Cache.Data, this._response.Cache.Offset, this._response.Cache.Size);
            this._response.Clear();
        }

        void IWebSocket.OnWsConnecting(HttpRequest request)
        {
        }

        void IWebSocket.OnWsConnected(HttpResponse response)
        {
        }

        bool IWebSocket.OnWsConnecting(HttpRequest request, HttpResponse response)
        {
            return true;
        }

        void IWebSocket.OnWsConnected(HttpRequest request)
        {
        }

        void IWebSocket.OnWsDisconnecting()
        {
        }

        void IWebSocket.OnWsDisconnected()
        {
        }

        void IWebSocket.OnWsReceived(byte[] buffer, long offset, long size)
        {
            string text = Encoding.UTF8.GetString(buffer, (int)offset, (int)size).Trim();
            Dictionary<string, string> kvs = new();
            ClientInfo aclient = null;
            ServerClient sclient = null;
            if (text.StartsWith('{') && text.EndsWith('}'))
            {
                try
                {
                    JObject jo = JObject.Parse(text);
                    if (jo.TryGetValue("auth", StringComparison.InvariantCultureIgnoreCase, out JToken authTokenValue))
                    {
                        if (authTokenValue.Type == JTokenType.String)
                        {
                            string val = authTokenValue.Value<string>();
                            if (val.StartsWith("basic ", StringComparison.InvariantCultureIgnoreCase) || val.StartsWith("auth ", StringComparison.InvariantCultureIgnoreCase))
                            {
                                this.FindAuthorizedClientFor(val, out aclient, out sclient);
                            }
                            else
                            {
                                this.IterateClientsForKV(Guid.Empty, Convert.FromBase64String(val), out aclient, out sclient);
                            }
                        }
                    }

                    foreach (KeyValuePair<string, JToken> kv in jo)
                    {
                        if (!string.Equals(kv.Key, "auth", StringComparison.InvariantCultureIgnoreCase))
                        {
                            kvs[kv.Key] = kv.Value.ToString();
                        }
                    }
                }
                catch
                {
                    // Unknown error?
                    this._ws.PrepareSendFrame(129, true, Encoding.UTF8.GetBytes("{\"status\": 400, \"message\": \"Bad Request\", \"details\":\"Malformed JSON!\"}").AsSpan());
                    this.Session.Send(WebSocket_GetBuffer(this._ws).AsSpan());
                }
            }
            else
            {
                string[] splt = text.Split('&');
                foreach (string kv in splt)
                {
                    string akv = kv.Replace("\\a", "&");
                    int eqIdx = akv.IndexOf('=');
                    if (eqIdx != -1)
                    {
                        string key = akv.Substring(0, eqIdx);
                        string value = akv.Substring(eqIdx + 1);
                        if (!string.Equals(key, "auth", StringComparison.InvariantCultureIgnoreCase))
                        {
                            kvs[key] = value;
                        }
                        else
                        {
                            if (value.StartsWith("basic ", StringComparison.InvariantCultureIgnoreCase) || value.StartsWith("auth ", StringComparison.InvariantCultureIgnoreCase))
                            {
                                this.FindAuthorizedClientFor(value, out aclient, out sclient);
                            }
                            else
                            {
                                this.IterateClientsForKV(Guid.Empty, Convert.FromBase64String(value), out aclient, out sclient);
                            }
                        }
                    }
                }
            }

            this.HandleAPIRequest(aclient, sclient, kvs);
        }

        void IWebSocket.OnWsClose(byte[] buffer, long offset, long size, int status = 1000)
        {
        }

        private bool SendPong(ReadOnlySpan<byte> buffer)
        {
            this._ws.PrepareSendFrame(138, true, buffer);
            this.Session.Send(WebSocket_GetBuffer(this._ws).AsSpan());
            return true;
        }

        void IWebSocket.OnWsPing(byte[] buffer, long offset, long size)
        {
            this.SendPong(buffer.AsSpan((int)offset, (int)size));
        }

        void IWebSocket.OnWsPong(byte[] buffer, long offset, long size)
        {
        }

        void IWebSocket.OnWsError(string error)
        {
        }

        void IWebSocket.OnWsError(SocketError error)
        {
        }

        private void HandleAPIRequest(ClientInfo authorizedSC, ServerClient onlineSC, Dictionary<string, string> kvs)
        {
            if (!kvs.ContainsKey("method"))
            {
                this.SendAPIResponse(400, "Bad Request", "API requests must have a method parameter!");
                return;
            }

            if (!APIMethod.TryAccept(kvs["method"], out APIMethod method))
            {
                this.SendAPIError(406, "Unknown API method!");
                return;
            }

            method.IdentifiedClient = authorizedSC;
            method.OnlineClient = onlineSC;
            method.Construct(this, kvs);
            method.Act(this);
        }

        private static readonly Dictionary<int, string> htmlStatusCodes = new Dictionary<int, string>() {
            [400] = "Bad Request",
            [401] = "Unauthorized",
            [403] = "Forbidden",
            [405] = "Method Not Allowed",
            [406] = "Not Acceptable"
        };

        public void SendAPIError(int status, string error) => this.SendAPIResponse(status, htmlStatusCodes[status], error);
        public void SendAPIOk(APIMethod caller, params (string, object)[] kvs) => this.SendAPIResponse(200, "OK", string.Empty, (kvs ?? Enumerable.Empty<(string, object)>()).Append(("method", caller.Identifier)).ToDictionary(x => x.Item1, x => x.Item2));

        private void SendAPIResponse(int status, string message, string details, Dictionary<string, object> kvs = null)
        {
            if (this._isWS)
            {
                JObject ret = new JObject
                {
                    ["status"] = status,
                    ["message"] = message
                };

                if (!string.IsNullOrEmpty(details))
                {
                    ret["details"] = details;
                }

                if (kvs != null)
                {
                    foreach (KeyValuePair<string, object> kv in kvs)
                    {
                        ret[kv.Key] = JToken.FromObject(kv.Value);
                    }
                }

                this._ws.PrepareSendFrame(129, true, Encoding.UTF8.GetBytes(ret.ToString(Formatting.None)).AsSpan());
                this.Session.Send(WebSocket_GetBuffer(this._ws).AsSpan());
            }
            else
            {
                this._response.SetBegin(status);
                if (status > 299)
                {
                    this._response.SetBody(details);
                }
                else
                {
                    StringBuilder sb = new();
                    if (kvs != null)
                    {
                        foreach (KeyValuePair<string, object> kv in kvs)
                        {
                            sb.Append(kv.Key);
                            sb.Append('=');
                            sb.Append(kv.Value.ToString());
                            sb.Append('&');
                        }

                        sb.Remove(sb.Length - 1, 1);
                    }

                    string ret = sb.ToString();
                    this._response.SetBody(sb.ToString());
                }

                this.Session.Send(this._response.Cache.Data, this._response.Cache.Offset, this._response.Cache.Size);
                this._response.Clear();
            }
        }
    }
}
