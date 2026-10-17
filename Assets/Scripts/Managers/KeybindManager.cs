using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class KeybindManager : MonoBehaviour
{
    /// <summary>
    /// A reference to the singleton instance
    /// </summary>
    private static KeybindManager instance;

    /// <summary>
    /// Property for accessing the singleton instance
    /// </summary>
    public static KeybindManager MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<KeybindManager>();
            }

            return instance;
        }
    }

    /// <summary>
    /// A dictionary for all movement keybinds
    /// </summary>
    public Dictionary<string, KeyCode> Keybinds { get; private set; }

    /// <summary>
    /// A dictionary for all actionKeybinds
    /// </summary>
    public Dictionary<string, KeyCode> ActionBinds { get; private set; }

    /// <summary>
    /// Boutons de MANETTE assignes a chaque sort (ACT1 a ACT6), separes
    /// de ActionBinds (clavier) ci-dessus -- voir GamepadInput.
    /// GetActionButtonDown(), qui lit maintenant ce dictionnaire au lieu
    /// d'un tableau fixe A/B/X/Y/LB/RB, et UIManager.
    /// BuildGamepadKeybindSection() pour le panneau "GamePad" du menu
    /// Keybinds qui permet de les reassigner.
    /// </summary>
    public Dictionary<string, KeyCode> GamepadActionBinds { get; private set; }

    /// <summary>
    /// Previent les abonnes (voir UIManager.BuildGamepadKeybindSection())
    /// qu'un bouton manette vient d'etre (re)assigne, pour mettre a jour
    /// le texte affiche sans dependre d'un tableau de boutons mis en
    /// cache a un instant precis (contrairement a UpdateKeyText(), qui
    /// suppose que keybindButtons a deja ete rempli -- voir
    /// UIManager.Awake()).
    /// </summary>
    public event System.Action<string, KeyCode> OnGamepadBindChanged;

    /// <summary>
    /// The name of the keybind we are trying to set or change
    /// </summary>
    private string bindName;

    /// <summary>
    /// Meme principe que bindName ci-dessus, mais pour un bouton de
    /// manette en cours d'assignation (voir GamepadKeyBindOnClick() et
    /// Update() plus bas, qui ecoute les boutons manette au lieu du
    /// clavier -- OnGUI()/Event ne remonte pas les boutons manette de
    /// facon fiable, voir Update() pour le detail).
    /// </summary>
    private string gamepadBindName = string.Empty;

    /// <summary>
    /// Prefixe des cles PlayerPrefs utilisees pour sauvegarder les
    /// keybinds -- une preference d'appareil (AZERTY/QWERTY, gout du
    /// joueur...), independante du personnage/compte, donc jamais
    /// remise a zero par une deconnexion/reconnexion (contrairement a
    /// avant : les touches redevenaient WASD/Alpha1-3 par defaut a
    /// chaque chargement de scene, rien n'etait jamais sauvegarde).
    /// </summary>
    private const string KeybindPrefPrefix = "Keybind_";

    /// <summary>
    /// Prefixe PlayerPrefs distinct de KeybindPrefPrefix : les touches
    /// clavier et les boutons manette partagent les memes cles logiques
    /// ("ACT1".."ACT6"), il faut donc des cles PlayerPrefs differentes
    /// pour ne pas que l'une ecrase l'autre.
    /// </summary>
    private const string GamepadKeybindPrefPrefix = "GamepadKeybind_";

    private void Awake()
    {
        // GamepadActionBinds doit etre pret AVANT le Start() de n'importe
        // quel autre script (notamment UIManager.BuildGamepadKeybindSection(),
        // qui lit ce dictionnaire pour afficher les boutons actuellement
        // assignes) -- Awake() s'execute toujours avant TOUS les Start()
        // de la scene, contrairement a l'ordre entre Start() de scripts
        // differents, qui n'est pas garanti. Keybinds/ActionBinds
        // (clavier) restent inities dans Start() ci-dessous comme avant,
        // pour ne rien changer a ce qui fonctionne deja.
        GamepadActionBinds = new Dictionary<string, KeyCode>();

        // Meme mapping par defaut que l'ancien tableau fixe de
        // GamepadInput.cs (A, B, X, Y, LB, RB) -- desormais reassignable
        // depuis le panneau "GamePad" du menu Keybinds.
        BindGamepadKey("ACT1", LoadGamepadKeyOrDefault("ACT1", KeyCode.JoystickButton0));
        BindGamepadKey("ACT2", LoadGamepadKeyOrDefault("ACT2", KeyCode.JoystickButton1));
        BindGamepadKey("ACT3", LoadGamepadKeyOrDefault("ACT3", KeyCode.JoystickButton2));
        BindGamepadKey("ACT4", LoadGamepadKeyOrDefault("ACT4", KeyCode.JoystickButton3));
        BindGamepadKey("ACT5", LoadGamepadKeyOrDefault("ACT5", KeyCode.JoystickButton4));
        BindGamepadKey("ACT6", LoadGamepadKeyOrDefault("ACT6", KeyCode.JoystickButton5));
    }

    // Use this for initialization
    void Start ()
    {
        Keybinds = new Dictionary<string, KeyCode>();

        ActionBinds = new Dictionary<string, KeyCode>();

        //Generates the default keybinds (ou la touche sauvegardee par le joueur)
        BindKey("UP", LoadKeyOrDefault("UP", KeyCode.W));
        BindKey("LEFT", LoadKeyOrDefault("LEFT", KeyCode.A));
        BindKey("DOWN", LoadKeyOrDefault("DOWN", KeyCode.S));
        BindKey("RIGHT", LoadKeyOrDefault("RIGHT", KeyCode.D));

        // 6 sorts dans la barre d'action (ACT1 a ACT6, voir Demo.unity) :
        // il faut donc 6 touches, pas seulement 3.
        BindKey("ACT1", LoadKeyOrDefault("ACT1", KeyCode.Alpha1));
        BindKey("ACT2", LoadKeyOrDefault("ACT2", KeyCode.Alpha2));
        BindKey("ACT3", LoadKeyOrDefault("ACT3", KeyCode.Alpha3));
        BindKey("ACT4", LoadKeyOrDefault("ACT4", KeyCode.Alpha4));
        BindKey("ACT5", LoadKeyOrDefault("ACT5", KeyCode.Alpha5));
        BindKey("ACT6", LoadKeyOrDefault("ACT6", KeyCode.Alpha6));
    }

    private static KeyCode LoadKeyOrDefault(string key, KeyCode defaultValue)
    {
        string prefName = KeybindPrefPrefix + key;
        return PlayerPrefs.HasKey(prefName) ? (KeyCode)PlayerPrefs.GetInt(prefName) : defaultValue;
    }

    private static void SaveKeyPref(string key, KeyCode keyBind)
    {
        PlayerPrefs.SetInt(KeybindPrefPrefix + key, (int)keyBind);
        PlayerPrefs.Save();
    }

    private static KeyCode LoadGamepadKeyOrDefault(string key, KeyCode defaultValue)
    {
        string prefName = GamepadKeybindPrefPrefix + key;
        return PlayerPrefs.HasKey(prefName) ? (KeyCode)PlayerPrefs.GetInt(prefName) : defaultValue;
    }

    private static void SaveGamepadKeyPref(string key, KeyCode keyBind)
    {
        PlayerPrefs.SetInt(GamepadKeybindPrefPrefix + key, (int)keyBind);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Binds a specific key
    /// </summary>
    /// <param name="key">Key to bind</param>
    /// <param name="keyBind">Keybind to set</param>
    public void BindKey(string key, KeyCode keyBind)
    {
        //Sets the default dictionary to the keybinds
        Dictionary<string, KeyCode> currentDictionary = Keybinds;

        if (key.Contains("ACT")) //If we are trying to bind an actionbutton, then we use the actionbinds dictionary instead
        {
            currentDictionary = ActionBinds;
        }
        if (!currentDictionary.ContainsKey(key))//Checks if the key is new
        {
            //If the key is new we add it
            currentDictionary.Add(key, keyBind);

            //We update the text on the button
            UIManager.MyInstance.UpdateKeyText(key, keyBind);
        }
        else if (currentDictionary.ContainsValue(keyBind)) //If we already have the keybind, then we need to change it to the new bind
        {
            //Looks for the old keybind
            string myKey = currentDictionary.FirstOrDefault(x => x.Value == keyBind).Key;

            //Unassigns the old keybind
            currentDictionary[myKey] = KeyCode.None;
            // Corrige : c'est le bouton de l'ANCIENNE touche (myKey) qu'il
            // faut remettre a None a l'ecran, pas celui qu'on est en train
            // d'assigner (key) -- avant, le bouton de l'ancienne touche
            // restait affiche avec la touche qu'on venait juste de
            // reassigner ailleurs.
            UIManager.MyInstance.UpdateKeyText(myKey, KeyCode.None);
            SaveKeyPref(myKey, KeyCode.None);
        }

        //Makes sure the new key is bound
        currentDictionary[key] = keyBind;
        UIManager.MyInstance.UpdateKeyText(key, keyBind);
        SaveKeyPref(key, keyBind);
        bindName = string.Empty;
    }

    /// <summary>
    /// Function for setting a keybind, this is called whenever a keybind button is clicked on the keybind menu
    /// </summary>
    /// <param name="bindName"></param>
    public void KeyBindOnClick(string bindName)
    {
        this.bindName = bindName;
    }

    /// <summary>
    /// Assigne un bouton de MANETTE (plutot qu'une touche clavier) a un
    /// emplacement de sort -- equivalent de BindKey()/BindGamepadKey()
    /// ci-dessous, mais pour GamepadActionBinds. Gere les doublons de la
    /// meme facon que BindKey() : si le bouton manette demande est deja
    /// utilise ailleurs, l'ancien emplacement est libere (None) en
    /// premier pour qu'un seul emplacement reponde a ce bouton a la fois.
    /// </summary>
    public void BindGamepadKey(string key, KeyCode keyBind)
    {
        if (!GamepadActionBinds.ContainsKey(key))
        {
            GamepadActionBinds.Add(key, keyBind);
        }
        else if (keyBind != KeyCode.None && GamepadActionBinds.ContainsValue(keyBind))
        {
            string oldKey = GamepadActionBinds.FirstOrDefault(x => x.Value == keyBind).Key;

            if (oldKey != null && oldKey != key)
            {
                GamepadActionBinds[oldKey] = KeyCode.None;
                SaveGamepadKeyPref(oldKey, KeyCode.None);
                OnGamepadBindChanged?.Invoke(oldKey, KeyCode.None);
            }
        }

        GamepadActionBinds[key] = keyBind;
        SaveGamepadKeyPref(key, keyBind);
        OnGamepadBindChanged?.Invoke(key, keyBind);
        gamepadBindName = string.Empty;
    }

    /// <summary>
    /// Appelee par le bouton manette qu'on clique dans le nouveau panneau
    /// "GamePad" du menu Keybinds (voir UIManager.
    /// BuildGamepadKeybindSection()) -- equivalent de KeyBindOnClick()
    /// ci-dessus, mais fait ensuite ecouter un bouton de MANETTE (voir
    /// Update() plus bas) au lieu d'une touche clavier (voir OnGUI()).
    /// </summary>
    public void GamepadKeyBindOnClick(string bindName)
    {
        gamepadBindName = bindName;
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(gamepadBindName))
        {
            return;
        }

        // On balaie les boutons manette generiques (0 a 19, voir
        // KeyCode.JoystickButtonN) plutot que de passer par OnGUI()/Event
        // comme pour le clavier ci-dessous : les boutons de manette ne
        // remontent pas de maniere fiable dans le systeme d'evenements
        // IMGUI utilise par OnGUI(), alors qu'Input.GetKeyDown() les
        // detecte normalement -- c'est d'ailleurs deja comme ca que
        // GamepadInput.cs lit tous les autres boutons manette du jeu.
        for (int i = 0; i < 20; i++)
        {
            KeyCode candidate = KeyCode.JoystickButton0 + i;

            if (Input.GetKeyDown(candidate))
            {
                BindGamepadKey(gamepadBindName, candidate);
                break;
            }
        }
    }

    private void OnGUI()
    {
        if (bindName != string.Empty)//Checks if we are going to save a keybind
        {
            Event e = Event.current; //Listens for an event

            if (e.isKey) //If the event is a key, then we change the keybind
            {
                BindKey(bindName, e.keyCode);
            }
        }
    }
}
