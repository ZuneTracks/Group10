using System.Linq;
using System.Runtime.InteropServices;
using Windows.Security.Credentials;
using Windows.Storage;

namespace Group10
{
    public sealed class TokenStore
    {
        private const string TokenResource = "Group10.GroupMe.AccessToken";
        private readonly PasswordVault vault = new PasswordVault();

        public string GetToken()
        {
            try
            {
                var credential = vault.FindAllByResource(TokenResource).FirstOrDefault();
                if (credential == null) return null;
                credential.RetrievePassword();
                return credential.Password;
            }
            catch (COMException)
            {
                return null;
            }
        }

        public void SaveToken(string token)
        {
            ClearToken();
            vault.Add(new PasswordCredential(TokenResource, "Group10", token));
        }

        public void ClearToken()
        {
            try
            {
                foreach (var credential in vault.FindAllByResource(TokenResource))
                {
                    vault.Remove(credential);
                }
            }
            catch (COMException)
            {
            }
        }
    }
}
