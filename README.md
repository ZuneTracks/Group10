# Group10

A native UWP client for the GroupMe service on Windows 10 Mobile, targeting build 15063 (Creators Update). Group10 supports OAuth sign-in, secure access-token storage, group and message browsing, sending messages, and live message updates.

## Configure

1. Register an app at [GroupMe Developers](https://dev.groupme.com/applications/new) with `https://localhost/` as its HTTPS callback URL.
2. Open the project in Visual Studio with the Windows 10 SDK (10.0.15063.0 targeting pack) installed.
3. Enter the app's Client ID, then select **Sign in**.

Access tokens are saved in the Windows Password Vault and are sent to GroupMe only in the `X-Access-Token` request header.
