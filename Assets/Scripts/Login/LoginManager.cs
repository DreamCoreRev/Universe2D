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
    private string serverUrl = "http://192.168.42.90:8080";

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

    [SerializeField]
    private string characterSelectSceneName = "CharacterSelect";

    private void Start()
    {
        CreateQuitButton();
    }

    // Bouton "Quitter" facon WoW, ajoute au runtime (comme pour
    // CharacterSelectManager) plutot qu'a la main dans Login.unity, pour ne
    // pas risquer la scene existante -- se greffe sous le meme Canvas que
    // les boutons deja presents (via loginButton.transform.parent), coin
    // inferieur gauche.
    private void CreateQuitButton()
    {
        if (loginButton == null)
        {
            return;
        }

        Transform parent = loginButton.transform.parent;

        GameObject go = new GameObject("QuitButton");
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(120, 34);
        rect.anchoredPosition = new Vector2(24, 24);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.82f, 0.62f, 0.16f, 1f);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.22f, 0.14f, 0.03f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button quitButton = go.AddComponent<Button>();
        quitButton.targetGraphic = image;

        GameObject textGO = new GameObject("Text");
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "QUITTER";
        text.fontSize = 13;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(0.20f, 0.11f, 0.02f, 1f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;

        quitButton.onClick.AddListener(OnQuitClicked);
    }

    // Appele par le QuitButton cree dans CreateQuitButton.
    private void OnQuitClicked()
    {
        Debug.Log("[LoginManager] Quitter clique -- fermeture de l'application.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

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

        // Garde le pseudo tape ici pour le reste de la session (voir
        // Session.cs) -- utilise ensuite en jeu, par exemple par le chat
        // (voir PlayerChatSync). Aucun risque a le faire aussi pour une
        // tentative d'inscription : il ne sert jamais avant qu'on entre
        // effectivement en jeu.
        Session.Username = username;

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

        if (Mirror.NetworkManager.singleton != null)
        {
            // Avant d'entrer en jeu : ecran de selection de personnage (voir
            // CharacterSelectManager), qui appellera lui-meme StartClient()
            // une fois un personnage choisi -- le NetworkManager (objet
            // "NetworkManager" dans cette scene, persiste via
            // DontDestroyOnLoad) chargera alors lui-meme Demo.unity.
            SceneManager.LoadScene(characterSelectSceneName);
        }
        else
        {
            // Filet de securite si le NetworkManager n'est pas present dans
            // la scene (ex: test solo rapide) -- comportement d'avant,
            // sans ecran de selection (pas de compte/personnages en solo).
            SceneManager.LoadScene(gameSceneName);
        }
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
