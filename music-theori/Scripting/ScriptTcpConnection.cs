using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;

namespace theori.Scripting
{
    public class ScriptTcpConnection : Disposable
    {
        public static ScriptTcpConnection? TryCreate(string host, int port)
        {
            try
            {
                var hostInfo = Dns.GetHostEntry(host);
                foreach (var addr in hostInfo.AddressList)
                {
                    var endPoint = new IPEndPoint(addr, port);

                    //var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    var socket = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(endPoint);

                    if (!socket.Connected)
                        continue;

                    return new ScriptTcpConnection(socket);
                }

                return null;
            }
            catch (Exception e)
            {
                Logger.Log(e.Message);
                return null;
            }
        }

        private readonly Socket m_socket;
        private byte[] m_buffer = new byte[1024];

        private IAsyncResult? m_result;

        private readonly ConcurrentQueue<string> m_messageData = new ConcurrentQueue<string>();

        private readonly Dictionary<string, List<DynValue>> m_topicHandlers = new Dictionary<string, List<DynValue>>();

        private ScriptTcpConnection(Socket listener)
        {
            m_socket = listener;
        }

        public void Process()
        {
            //if (m_result == null || m_result.IsCompleted) StartReceiving();

            int avail = m_socket.Available;
            if (m_socket.Available > 0)
            {
                if (m_buffer.Length < avail)
                    m_buffer = new byte[avail];
                else m_buffer.Fill((byte)0);

                m_socket.Receive(m_buffer, 0, avail, SocketFlags.None);
                string data = Encoding.UTF8.GetString(m_buffer, 0, avail);
                string[] lines = data.Split('\n');

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    char k = line[0];
                    string lineData = line.Substring(1);

                    if (k == 1)
                    {
                        Logger.Log($"Received JSON Object: {lineData}");
                        m_messageData.Enqueue(lineData);
                    }
                }
            }

            while (m_messageData.TryDequeue(out string data))
            {
                if (!(JObject.Parse(data) is JObject jobj))
                {
                    Logger.Log($"{data} is not a JSON object.");
                    return;
                }

                if (!jobj.TryGetValue("topic", out var topicValue))
                {
                    Logger.Log($"{data} has no topic.");
                    return;
                }

                string topic = topicValue.Value<string>();
                if (m_topicHandlers.TryGetValue(topic, out var handlers))
                {
                    foreach (var callback in handlers)
                    {
                        var script = callback.Function.OwnerScript;
                        script.Call(callback, JObjectToTable(script, jobj));
                    }
                }
            }

            static DynValue JObjectToTable(Script script, JObject jobj)
            {
                var result = new Table(script);
                foreach (var pair in jobj)
                    result[pair.Key] = JTokenToValue(script, pair.Value);
                return DynValue.NewTable(result);
            }

            static DynValue JArrayToTable(Script script, JArray jarr)
            {
                var result = new Table(script);
                foreach (var value in jarr)
                    result.Append(JTokenToValue(script, value));
                return DynValue.NewTable(result);
            }

            static DynValue JTokenToValue(Script script, JToken token) => token switch
            {
                JObject jobj => JObjectToTable(script, jobj),
                JArray jarr => JArrayToTable(script, jarr),
                _ => token.Type switch
                {
                    JTokenType.String => DynValue.NewString(token.Value<string>()),
                    JTokenType.Boolean => token.Value<bool>() ? DynValue.True : DynValue.False,
                    JTokenType.Float => DynValue.NewNumber(token.Value<double>()),
                    JTokenType.Integer => DynValue.NewNumber(token.Value<long>()),
                    _ => DynValue.Nil,
                },
            };
        }

        public void ListenForTopic(string topic, DynValue callback)
        {
            if (!m_topicHandlers.TryGetValue(topic, out var handlers))
                m_topicHandlers[topic] = handlers = new List<DynValue>();
            handlers.Add(callback);
        }

        public void StartReceiving()
        {
            return;

            try
            {
                Logger.Log("TCP Connection waiting to receive");
                m_buffer.Fill((byte)0);
                m_result = m_socket.BeginReceive(m_buffer, 0, m_buffer.Length, SocketFlags.None, ReceiveCallback, null);
            }
            catch
            {
                Logger.Log("Failed to receive");
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            Logger.Log("TCP Reveive callback");

            try
            {
                if (m_socket.EndReceive(result) > 1)
                {
                    int length = Math.Max(m_buffer.IndexOf((byte)0), m_buffer.IndexOf((byte)'\n')) - 1;
                    string data = Encoding.UTF8.GetString(m_buffer, 1, length);

                    Logger.Log($"TCP Reveived {data}");

                    m_messageData.Enqueue(data.Trim());
                    StartReceiving();
                }
                else
                {
                    Close();
                }
            }
            catch
            {
                // if exeption is throw check if socket is connected because than you can startreive again else Dissconect
                if (!m_socket.Connected)
                {
                    Close();
                }
                else
                {
                    StartReceiving();
                }
            }
        }

        public void SendLine(string line)
        {
            Logger.Log($"Sending {line}");

            byte[] bytes = Encoding.UTF8.GetBytes(line);

            m_socket.Send(new byte[] { 1 });
            m_socket.Send(bytes.AsSpan());
            m_socket.Send(new byte[] { (byte)'\n' });
        }

        public string? TryDequeue() => m_messageData.TryDequeue(out string result) ? result : null;

        public void Close() => Dispose();
        protected override void DisposeManaged()
        {
            Logger.Log("Closing TCP Connection");
            m_socket.Close();
        }
    }
}
