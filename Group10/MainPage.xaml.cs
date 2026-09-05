using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.ApplicationModel.Contacts;
using Windows.Security.Authentication.Web;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Group10
{
    public sealed partial class MainPage : Page
    {
        private readonly TokenStore tokenStore = new TokenStore();
        private readonly ObservableCollection<ChatGroup> groups = new ObservableCollection<ChatGroup>();
        private readonly ObservableCollection<ChatMessage> messages = new ObservableCollection<ChatMessage>();
        private GroupMeApiClient api;
        private PushClient push;
        private string currentUserId;

        public MainPage()
        {
            InitializeComponent();
            GroupsList.ItemsSource = groups;
            MessagesList.ItemsSource = messages;
            Loaded += async (sender, args) => await RestoreSessionAsync();
            Unloaded += (sender, args) => { if (push != null) push.Stop(); };
        }

        private async Task RestoreSessionAsync()
        {
            var token = tokenStore.GetToken();
            if (!string.IsNullOrWhiteSpace(token)) await StartSessionAsync(token);
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientConfiguration.GroupMeClientId == "REPLACE_WITH_GROUPME_CLIENT_ID")
            {
                SetStatus("GroupMe app registration has not been configured.");
                return;
            }

            var authorization = new Uri("https://oauth.groupme.com/oauth/authorize?client_id=" + Uri.EscapeDataString(ClientConfiguration.GroupMeClientId));
            var result = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, authorization, new Uri(ClientConfiguration.RedirectUri));
            string token;
            if (result.ResponseStatus == WebAuthenticationStatus.UserCancel)
            {
                SetStatus("Sign-in was cancelled.");
                return;
            }

            if (result.ResponseStatus != WebAuthenticationStatus.Success)
            {
                SetStatus("GroupMe could not complete sign-in.");
                return;
            }

            if (!TryGetToken(result.ResponseData, out token))
            {
                SetStatus("GroupMe did not return a token. Check the registered callback URL.");
                return;
            }

            tokenStore.SaveToken(token);
            await StartSessionAsync(token);
        }

        private async Task StartSessionAsync(string token)
        {
            try
            {
                api = new GroupMeApiClient(token);
                var user = await api.GetCurrentUserAsync();
                currentUserId = user.Id;
                groups.Clear();
                foreach (var group in await api.GetGroupsAsync()) groups.Add(group);
                AuthenticationPane.Visibility = Visibility.Collapsed;
                AppShell.Visibility = Visibility.Visible;
                SetStatus("Signed in as " + user.Name + ".");
                push = new PushClient(token);
                push.MessageReceived += Push_MessageReceived;
                _ = push.StartAsync(user.Id);
            }
            catch (HttpRequestException)
            {
                SetStatus("GroupMe could not be reached.");
            }
        }

        private async void GroupsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var group = GroupsList.SelectedItem as ChatGroup;
            if (group == null || api == null) return;
            try
            {
                messages.Clear();
                var loadedMessages = group.IsDirect
                    ? await api.GetDirectMessagesAsync(group.DirectUserId)
                    : await api.GetMessagesAsync(group.Id);
                foreach (var message in loadedMessages.Reverse()) messages.Add(message);
                ConversationTitle.Text = group.Name;
                SetStatus(group.Name);
            }
            catch (HttpRequestException)
            {
                SetStatus("Messages could not be loaded.");
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var group = GroupsList.SelectedItem as ChatGroup;
            var text = MessageBox.Text.Trim();
            if (group == null || api == null || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                if (group.IsDirect)
                {
                    await api.SendDirectMessageAsync(group.DirectUserId, text);
                }
                else
                {
                    await api.SendMessageAsync(group.Id, text);
                }
                MessageBox.Text = string.Empty;
            }
            catch (HttpRequestException)
            {
                SetStatus("Message could not be sent.");
            }
        }

        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (push != null) push.Dispose();
            push = null;
            tokenStore.ClearToken();
            api = null;
            currentUserId = null;
            groups.Clear();
            messages.Clear();
            ConversationTitle.Text = "Select a chat";
            AppShell.Visibility = Visibility.Collapsed;
            AuthenticationPane.Visibility = Visibility.Visible;
            SetStatus("Signed out.");
        }

        private async void Push_MessageReceived(object sender, ChatMessage message)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var selected = GroupsList.SelectedItem as ChatGroup;
                if (selected == null) return;
                if (!selected.IsDirect && selected.Id == message.GroupId)
                {
                    messages.Add(message);
                    return;
                }

                var otherUserId = string.Equals(message.SenderId, currentUserId, StringComparison.Ordinal)
                    ? message.RecipientId
                    : message.SenderId;
                if (selected.IsDirect && selected.DirectUserId == otherUserId) messages.Add(message);
            });
        }

        private void NewDirectMessageButton_Click(object sender, RoutedEventArgs e)
        {
            DirectUserIdBox.Text = string.Empty;
            SelectedContactText.Text = "No phone contact selected";
            DirectMessageStatusText.Text = string.Empty;
            DirectMessagePane.Visibility = Visibility.Visible;
        }

        private void NewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            GroupNameBox.Text = string.Empty;
            GroupDescriptionBox.Text = string.Empty;
            GroupShareToggle.IsOn = false;
            CreateGroupStatusText.Text = string.Empty;
            CreateGroupPane.Visibility = Visibility.Visible;
        }

        private async void ChooseContactButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new ContactPicker();
            picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.PhoneNumber);
            var contact = await picker.PickContactAsync();
            if (contact != null)
            {
                SelectedContactText.Text = contact.DisplayName;
            }
        }

        private void CancelDirectMessageButton_Click(object sender, RoutedEventArgs e)
        {
            DirectMessagePane.Visibility = Visibility.Collapsed;
        }

        private void CancelCreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            CreateGroupPane.Visibility = Visibility.Collapsed;
        }

        private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var name = GroupNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                SetStatus("Enter a group name.");
                return;
            }

            try
            {
                var group = await api.CreateGroupAsync(name, GroupDescriptionBox.Text.Trim(), GroupShareToggle.IsOn);
                groups.Insert(0, group);
                CreateGroupPane.Visibility = Visibility.Collapsed;
                GroupsList.SelectedItem = group;
            }
            catch (HttpRequestException)
            {
                SetStatus("The group could not be created.");
            }
        }

        private void StartDirectMessageButton_Click(object sender, RoutedEventArgs e)
        {
            var directUserId = DirectUserIdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(directUserId))
            {
                SetStatus("Enter the contact's GroupMe user ID.");
                return;
            }

            if (string.Equals(directUserId, currentUserId, StringComparison.Ordinal))
            {
                SetStatus("Choose another GroupMe user.");
                return;
            }

            var contactName = SelectedContactText.Text == "No phone contact selected"
                ? directUserId
                : SelectedContactText.Text;
            var conversation = groups.FirstOrDefault(item => item.DirectUserId == directUserId);
            if (conversation == null)
            {
                conversation = new ChatGroup
                {
                    Id = "direct:" + directUserId,
                    DirectUserId = directUserId,
                    Name = contactName,
                    Description = "Direct message"
                };
                groups.Insert(0, conversation);
            }

            DirectMessagePane.Visibility = Visibility.Collapsed;
            GroupsList.SelectedItem = conversation;
        }

        private static bool TryGetToken(string responseData, out string token)
        {
            token = null;
            Uri callback;
            if (!Uri.TryCreate(responseData, UriKind.Absolute, out callback)) return false;
            token = GetParameter(callback.Query) ?? GetParameter(callback.Fragment.TrimStart('#'));
            return !string.IsNullOrWhiteSpace(token);
        }

        private static string GetParameter(string parameters)
        {
            var tokenPart = parameters.TrimStart('?', '#').Split('&').FirstOrDefault(part => part.StartsWith("access_token=", StringComparison.Ordinal));
            if (tokenPart == null) return null;
            return Uri.UnescapeDataString(tokenPart.Substring("access_token=".Length));
        }

        private void SetStatus(string message)
        {
            if (CreateGroupPane.Visibility == Visibility.Visible)
            {
                CreateGroupStatusText.Text = message;
            }
            else if (DirectMessagePane.Visibility == Visibility.Visible)
            {
                DirectMessageStatusText.Text = message;
            }
            else if (AuthenticationPane.Visibility == Visibility.Visible)
            {
                StatusText.Text = message;
            }
            else
            {
                ConversationStatusText.Text = message;
            }
        }
    }
}
