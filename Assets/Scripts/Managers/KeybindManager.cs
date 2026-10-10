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
    /// The name of the keybind we are trying to set or change
    /// </summary>
    private string bindName;

    /// <summary>
    /// Prefixe des cles PlayerPrefs utilisees pour sauvegarder les
    /// keybinds -- une preference d'appareil (AZERTY/QWERTY, gout du
    /// joueur...), independante du personnage/compte, donc jamais
    /// remise a zero par une deconnexion/reconnexion (contrairement a
    /// avant : les touches redevenaient WASD/Alpha1-3 par defaut a
    /// chaque chargement de scene, rien n'etait jamais sauvegarde).
    /// </summary>
    private const string KeybindPrefPrefix = "Keybind_";

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
