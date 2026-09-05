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
                var value = item.GetObject();
                messages.Add(new ChatMessage
                {
                    Id = value.GetNamedString("id"),
                    GroupId = value.GetNamedString("group_id"),
                    SenderName = value.GetNamedString("name", "Unknown"),
                    Text = value.GetNamedString("text", string.Empty),
                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds((long)value.GetNamedNumber("created_at", 0))
                });
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

        private async Task<JsonObject> GetAsync(string path)
        {
            var response = await client.GetAsync(BaseAddress + path);
            response.EnsureSuccessStatusCode();
            return JsonObject.Parse(await response.Content.ReadAsStringAsync());
        }
    }
}
