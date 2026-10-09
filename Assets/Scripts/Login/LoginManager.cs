using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ecran de connexion : parle en HTTP/JSON au petit serveur separe
/// (Server/AuthServer, voir Server/README.md), qui est le seul a toucher
/// la base MySQL. Ce script ne voit jamais la base, ni les mots de passe
/// des autres joueurs -- seulement la reponse success/message du serveur.
/// </summary>
public class LoginManager : MonoBehaviour
{
    [SerializeField]
    private string serverUrl = "http://192.168.42.100:8080";

    [SerializeField]
    private InputField usernameInput;

    [SerializeField]
    private InputField passwordInput;

    [SerializeField]
    private Button loginButton;

    [SerializeField]
    private Button registerButton;

    [SerializeField]
    private Text statusText;

    [SerializeField]
    private string gameSceneName = "Demo";

    // Called by UI element LoginButton.OnClick
    public void OnLoginClicked()
    {
        TrySend("/login", OnLoginDone);
    }

    // Called by UI element RegisterButton.OnClick
    public void OnRegisterClicked()
    {
        TrySend("/register", OnRegisterDone);
    }

    private void TrySend(string path, System.Action<bool, string> onDone)
    {
        string username = usernameInput != null ? usernameInput.text.Trim() : "";
        string password = passwordInput != null ? passwordInput.text : "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowStatus("Remplis le nom d'utilisateur et le mot de passe", false);
            return;
        }

        SetBusy(true);
        StartCoroutine(SendRequest(path, username, password, onDone));
    }

    private void OnLoginDone(bool success, string message)
    {
        SetBusy(false);
        ShowStatus(message, success);

        if (success)
        {
            StartCoroutine(LoadGameAfterDelay());
        }
    }

    private void OnRegisterDone(bool success, string message)
    {
        SetBusy(false);
        ShowStatus(message, success);
    }

    private IEnumerator LoadGameAfterDelay()
    {
        // Petite pause pour laisser le temps de lire le message "Connecte"
        // avant que la scene change.
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene(gameSceneName);
    }

    private void SetBusy(bool busy)
    {
        if (loginButton != null)
        {
            loginButton.interactable = !busy;
        }
        if (registerButton != null)
        {
            registerButton.interactable = !busy;
        }
    }

    private void ShowStatus(string message, bool success)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.color = success ? new Color(0.17f, 0.55f, 0.2f) : new Color(0.8f, 0.15f, 0.15f);
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    [System.Serializable]
    private class CredentialsPayload
    {
        public string username;
        public string password;
    }

    [System.Serializable]
    private class AuthResponse
    {
        public bool success;
        public string message;
    }

    private IEnumerator SendRequest(string path, string username, string password, System.Action<bool, string> onDone)
    {
        CredentialsPayload payload = new CredentialsPayload { username = username, password = password };
        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(serverUrl + path, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            string responseText = req.downloadHandler != null ? req.downloadHandler.text : null;
            AuthResponse response = null;

            if (!string.IsNullOrEmpty(responseText))
            {
                try
                {
                    response = JsonUtility.FromJson<AuthResponse>(responseText);
                }
                catch
                {
                    response = null;
                }
            }

            if (response != null)
            {
                onDone(response.success, response.message);
            }
            else if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError)
            {
                onDone(false, "Impossible de contacter le serveur. Verifie qu'il est lance (dotnet run dans Server/AuthServer).");
            }
            else
            {
                onDone(false, "Reponse invalide du serveur.");
            }
        }
    }
}
