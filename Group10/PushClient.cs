using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace Group10
{
    public sealed class PushClient : IDisposable
    {
        private const string Endpoint = "https://push.groupme.com/faye";
        private readonly HttpClient client = new HttpClient();
        private readonly string token;
        private CancellationTokenSource cancellation;
        private string clientId;
        private int requestId;

        public event EventHandler<ChatMessage> MessageReceived;

        public PushClient(string token)
        {
            this.token = token;
        }

        public async Task StartAsync(string userId)
        {
            Stop();
            cancellation = new CancellationTokenSource();
            var handshake = await SendAsync(new JsonObject
            {
                ["channel"] = JsonValue.CreateStringValue("/meta/handshake"),
                ["version"] = JsonValue.CreateStringValue("1.0"),
                ["supportedConnectionTypes"] = ConnectionTypes(),
                ["id"] = JsonValue.CreateStringValue(NextId())
            }, cancellation.Token);

            clientId = handshake.GetArray()[0].GetObject().GetNamedString("clientId");
            await SendAsync(new JsonObject
            {
                ["channel"] = JsonValue.CreateStringValue("/meta/subscribe"),
                ["clientId"] = JsonValue.CreateStringValue(clientId),
                ["subscription"] = JsonValue.CreateStringValue("/user/" + userId),
                ["id"] = JsonValue.CreateStringValue(NextId()),
                ["ext"] = Authentication()
            }, cancellation.Token);

            await PollAsync(cancellation.Token);
        }

        private async Task PollAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var events = await SendAsync(new JsonObject
                {
                    ["channel"] = JsonValue.CreateStringValue("/meta/connect"),
                    ["clientId"] = JsonValue.CreateStringValue(clientId),
                    ["connectionType"] = JsonValue.CreateStringValue("long-polling"),
                    ["id"] = JsonValue.CreateStringValue(NextId())
                }, cancellationToken);

                foreach (var item in events.GetArray())
                {
                    var pushEvent = item.GetObject();
                    var data = pushEvent.GetNamedObject("data", null);
                    if (data == null || data.GetNamedString("type", string.Empty) != "line.create") continue;
                    var subject = data.GetNamedObject("subject");
                    MessageReceived?.Invoke(this, new ChatMessage
                    {
                        Id = subject.GetNamedString("id"),
                        GroupId = subject.GetNamedString("group_id", string.Empty),
                        SenderId = subject.GetNamedString("user_id", string.Empty),
                        RecipientId = subject.GetNamedString("recipient_id", string.Empty),
                        SenderName = subject.GetNamedString("name", "Unknown"),
                        Text = subject.GetNamedString("text", string.Empty),
                        CreatedAt = DateTimeOffset.FromUnixTimeSeconds((long)subject.GetNamedNumber("created_at", 0))
                    });
                }
            }
        }

        private async Task<JsonValue> SendAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var payload = new JsonArray();
            payload.Add(message);
            var response = await client.PostAsync(Endpoint, new StringContent(payload.Stringify(), Encoding.UTF8, "application/json"), cancellationToken);
            response.EnsureSuccessStatusCode();
            return JsonValue.Parse(await response.Content.ReadAsStringAsync());
        }

        private JsonObject Authentication()
        {
            return new JsonObject
            {
                ["access_token"] = JsonValue.CreateStringValue(token),
                ["timestamp"] = JsonValue.CreateNumberValue(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };
        }

        private static JsonArray ConnectionTypes()
        {
            var types = new JsonArray();
            types.Add(JsonValue.CreateStringValue("long-polling"));
            return types;
        }

        private string NextId()
        {
            requestId++;
            return requestId.ToString();
        }

        public void Stop()
        {
            if (cancellation == null) return;
            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = null;
        }

        public void Dispose()
        {
            Stop();
            client.Dispose();
        }
    }
}
