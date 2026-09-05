using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace Group10
{
    public sealed class GroupMeApiClient
    {
        private const string BaseAddress = "https://api.groupme.com/v3/";
        private readonly HttpClient client = new HttpClient();

        public GroupMeApiClient(string accessToken)
        {
            client.DefaultRequestHeaders.Add("X-Access-Token", accessToken);
        }

        public async Task<GroupMeUser> GetCurrentUserAsync()
        {
            var response = await GetAsync("users/me");
            var user = response.GetNamedObject("response");
            return new GroupMeUser { Id = user.GetNamedString("id"), Name = user.GetNamedString("name") };
        }

        public async Task<IReadOnlyList<ChatGroup>> GetGroupsAsync()
        {
            var response = await GetAsync("groups");
            var groups = new List<ChatGroup>();
            foreach (var item in response.GetNamedArray("response"))
            {
                var value = item.GetObject();
                groups.Add(new ChatGroup
                {
                    Id = value.GetNamedString("id"),
                    Name = value.GetNamedString("name"),
                    Description = value.GetNamedString("description", string.Empty)
                });
            }
            return groups;
        }

        public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string groupId)
        {
            var response = await GetAsync("groups/" + Uri.EscapeDataString(groupId) + "/messages?limit=100");
            var messages = new List<ChatMessage>();
            foreach (var item in response.GetNamedObject("response").GetNamedArray("messages"))
            {
                messages.Add(CreateMessage(item.GetObject()));
            }
            return messages;
        }

        public async Task<IReadOnlyList<ChatMessage>> GetDirectMessagesAsync(string otherUserId)
        {
            var response = await GetAsync("direct_messages?other_user_id=" + Uri.EscapeDataString(otherUserId));
            var payload = response.GetNamedObject("response", response);
            var messages = new List<ChatMessage>();
            foreach (var item in payload.GetNamedArray("direct_messages"))
            {
                messages.Add(CreateMessage(item.GetObject()));
            }
            return messages;
        }

        public async Task SendMessageAsync(string groupId, string text)
        {
            var message = new JsonObject
            {
                ["source_guid"] = JsonValue.CreateStringValue(Guid.NewGuid().ToString()),
                ["text"] = JsonValue.CreateStringValue(text)
            };
            var body = new JsonObject { ["message"] = message };
            var response = await client.PostAsync(
                BaseAddress + "groups/" + Uri.EscapeDataString(groupId) + "/messages",
                new StringContent(body.Stringify(), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }

        public async Task<ChatGroup> CreateGroupAsync(string name, string description, bool createShareLink)
        {
            var body = new JsonObject
            {
                ["name"] = JsonValue.CreateStringValue(name),
                ["description"] = JsonValue.CreateStringValue(description),
                ["share"] = JsonValue.CreateBooleanValue(createShareLink)
            };
            var response = await client.PostAsync(
                BaseAddress + "groups",
                new StringContent(body.Stringify(), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            var payload = JsonObject.Parse(await response.Content.ReadAsStringAsync());
            var group = payload.GetNamedObject("response", payload);
            return new ChatGroup
            {
                Id = group.GetNamedString("id"),
                Name = group.GetNamedString("name"),
                Description = group.GetNamedString("description", string.Empty)
            };
        }

        public async Task SendDirectMessageAsync(string recipientId, string text)
        {
            var directMessage = new JsonObject
            {
                ["source_guid"] = JsonValue.CreateStringValue(Guid.NewGuid().ToString()),
                ["recipient_id"] = JsonValue.CreateStringValue(recipientId),
                ["text"] = JsonValue.CreateStringValue(text)
            };
            var body = new JsonObject { ["direct_message"] = directMessage };
            var response = await client.PostAsync(
                BaseAddress + "direct_messages",
                new StringContent(body.Stringify(), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }

        public async Task LikeMessageAsync(string conversationId, string messageId)
        {
            var response = await client.PostAsync(
                BaseAddress + "messages/" + Uri.EscapeDataString(conversationId) + "/" + Uri.EscapeDataString(messageId) + "/like",
                new StringContent(string.Empty));
            response.EnsureSuccessStatusCode();
        }

        private static ChatMessage CreateMessage(JsonObject value)
        {
            return new ChatMessage
            {
                Id = value.GetNamedString("id"),
                GroupId = value.GetNamedString("group_id", string.Empty),
                SenderId = value.GetNamedString("user_id", string.Empty),
                RecipientId = value.GetNamedString("recipient_id", string.Empty),
                SenderName = value.GetNamedString("name", "Unknown"),
                Text = value.GetNamedString("text", string.Empty),
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds((long)value.GetNamedNumber("created_at", 0))
            };
        }

        private async Task<JsonObject> GetAsync(string path)
        {
            var response = await client.GetAsync(BaseAddress + path);
            response.EnsureSuccessStatusCode();
            return JsonObject.Parse(await response.Content.ReadAsStringAsync());
        }
    }
}
