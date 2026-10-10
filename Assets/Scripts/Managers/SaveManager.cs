using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveManager : MonoBehaviour
{
    [SerializeField]
    private Item[] items;

    private Chest[] chests;

    private CharButton[] equipment;

    [SerializeField]
    private ActionButton[] actionButtons;

    [SerializeField]
    private SavedGame[] saveSlots;

    [SerializeField]
    private GameObject dialogue;

    [SerializeField]
    private Text dialogueText;

    private SavedGame current;

    private string action;

    /// <summary>
    /// Sauvegarde automatique periodique (comme sur Wow) pendant qu'on
    /// joue un personnage du nouveau systeme compte -> personnages (voir
    /// CharacterSelectManager) -- avant ca, la seule sauvegarde avait lieu
    /// a la fermeture du jeu (OnApplicationQuit), donc tout etait perdu en
    /// cas de crash/coupure reseau/alt-tab mobile entre-temps.
    /// </summary>
    [SerializeField]
    private float autoSaveIntervalSeconds = 120f;

    private Coroutine autoSaveCoroutine;

    // Use this for initialization
    void Awake()
    {
        chests = FindObjectsOfType<Chest>();
        equipment = FindObjectsOfType<CharButton>();

        foreach (SavedGame saved in saveSlots)
        {
            //We need to show the saved files here
            ShowSavedFiles(saved);
        }

    }

    private bool playerInitialized = false;

    private void Start()
    {
        TryInitializePlayer();
    }

    /// <summary>
    /// En solo, Player.MyInstance existe deja au chargement de Demo (place
    /// a la main dans la scene), donc ca marche directement depuis Start().
    /// En reseau, le Player local n'existe pas encore a cet instant (Mirror
    /// le cree juste apres la connexion) -- Player.cs nous rappelle alors
    /// lui-meme des qu'il est pret. Le flag evite de tout initialiser 2 fois
    /// si les deux appels finissent par arriver (cas solo).
    /// </summary>
    public void TryInitializePlayer()
    {
        if (playerInitialized || Player.MyInstance == null)
        {
            return;
        }

        playerInitialized = true;

        // Nouveau systeme (compte -> personnages, voir CharacterSelectManager)
        // : prioritaire sur l'ancien systeme de slots locaux ci-dessous, qui
        // reste utilise si jamais cette scene est chargee sans passer par
        // l'ecran de selection (le menu principal solo historique, par ex).
        if (Session.HasSelectedCharacter)
        {
            if (Session.IsNewCharacter || !LoadCharacter(Session.SelectedCharacterId))
            {
                // Personnage tout juste cree (ou sauvegarde introuvable,
                // ce qui ne devrait arriver que si la creation a echoue a
                // sauvegarder) : on part sur des valeurs par defaut et on
                // sauvegarde tout de suite, pour que la prochaine
                // reconnexion retrouve bien ce personnage.
                Player.MyInstance.SetDefaultValues();
                SaveCharacter(Session.SelectedCharacterId);
            }

            StartAutoSave();

            return;
        }

        if (PlayerPrefs.HasKey("Load"))
        {
            Load(saveSlots[PlayerPrefs.GetInt("Load")]);
            PlayerPrefs.DeleteKey("Load");
        }
        else
        {
            Player.MyInstance.SetDefaultValues();
        }
    }

    /// <summary>
    /// Sauvegarde automatique a la fermeture du jeu, pour le personnage
    /// choisi sur l'ecran de selection (voir CharacterSelectManager). Les
    /// anciens slots locaux (saveSlots) ne sont eux sauvegardes qu'a la
    /// demande, via ShowDialogue/ExecuteAction -- comportement inchange.
    /// </summary>
    private void OnApplicationQuit()
    {
        if (Session.HasSelectedCharacter && Player.MyInstance != null)
        {
            SaveCharacter(Session.SelectedCharacterId);
        }
    }

    /// <summary>
    /// Filet de securite mobile : sur Android/iOS, l'app passe en pause
    /// (bouton Accueil, appel, changement d'app) bien plus souvent qu'elle
    /// ne quitte vraiment -- OnApplicationQuit n'est alors pas garanti
    /// d'etre appele a temps. On sauvegarde donc aussi des qu'on part en
    /// pause (pauseStatus == true), pas quand on revient (false).
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && Session.HasSelectedCharacter && Player.MyInstance != null)
        {
            SaveCharacter(Session.SelectedCharacterId);
        }
    }

    private void StartAutoSave()
    {
        if (autoSaveCoroutine != null)
        {
            StopCoroutine(autoSaveCoroutine);
        }

        autoSaveCoroutine = StartCoroutine(AutoSaveLoop());
    }

    private IEnumerator AutoSaveLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(autoSaveIntervalSeconds);

        while (true)
        {
            yield return wait;

            if (Session.HasSelectedCharacter && Player.MyInstance != null)
            {
                SaveCharacter(Session.SelectedCharacterId);
            }
        }
    }

    public void ShowDialogue(GameObject clickButton)
    {
        action = clickButton.name;

        switch (action)
        {
            case "Load":
                dialogueText.text = "Load game?";
                break;
            case "Save":
                dialogueText.text = "Save game?";
                break;
            case "Delete":
                dialogueText.text = "Delete savefile?";
                break;
        }

        current = clickButton.GetComponentInParent<SavedGame>();
        dialogue.SetActive(true);
    }

    public void ExecuteAction()
    {
        switch (action)
        {
            case "Load":
                LoadScene(current);
                break;
            case "Save":
                Save(current);
                break;
            case "Delete":
                Delete(current);
                break;
        }

        CloseDialogue();

    }

    private void LoadScene(SavedGame savedGame)
    {
        string path = Application.persistentDataPath + "/" + savedGame.gameObject.name + ".dat";

        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            SaveData data;
            BinaryFormatter bf = new BinaryFormatter();
            using (FileStream file = File.Open(path, FileMode.Open))
            {
                data = (SaveData)bf.Deserialize(file);
            }

            PlayerPrefs.SetInt("Load", savedGame.MyIndex);
            SceneManager.LoadScene(data.MyScene);
        }
        catch (System.Exception e)
        {
            // A corrupt/unreadable save file should never crash the main
            // menu -- just report it and leave the file alone.
            Debug.LogError("Impossible de lire la sauvegarde (" + savedGame.gameObject.name + ") : " + e);
        }
    }

    public void CloseDialogue()
    {
        dialogue.SetActive(false);
    }

    private void Delete(SavedGame savedGame)
    {
        File.Delete(Application.persistentDataPath + "/" + savedGame.gameObject.name + ".dat");
        savedGame.HideVisuals();
    }

    private void ShowSavedFiles(SavedGame savedGame)
    {
        string path = Application.persistentDataPath + "/" + savedGame.gameObject.name + ".dat";

        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            SaveData data;
            BinaryFormatter bf = new BinaryFormatter();
            using (FileStream file = File.Open(path, FileMode.Open))
            {
                data = (SaveData)bf.Deserialize(file);
            }
            savedGame.ShowInfo(data);
        }
        catch (System.Exception e)
        {
            // Called from Awake() for every save slot -- a corrupt file
            // here must not crash the game on startup.
            Debug.LogError("Impossible de lire la sauvegarde (" + savedGame.gameObject.name + ") : " + e);
        }
    }

   public void Save(SavedGame savedGame)
    {
        try
        {
            SaveData data = BuildSaveData();
            WriteSaveFile(savedGame.gameObject.name, data);
            ShowSavedFiles(savedGame);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Echec de la sauvegarde (" + savedGame.gameObject.name + ") : " + e);
        }
    }

    /// <summary>
    /// Equivalent de Save(SavedGame) pour un personnage du nouveau systeme
    /// compte -> personnages (voir CharacterSelectManager) : meme format de
    /// sauvegarde (SaveData/BinaryFormatter), mais le fichier est nomme
    /// directement d'apres l'id du personnage cote serveur (AuthServer),
    /// sans passer par les GameObject saveSlots de l'ancien menu solo.
    /// </summary>
    public void SaveCharacter(int characterId)
    {
        try
        {
            SaveData data = BuildSaveData();
            WriteSaveFile(CharacterFileBaseName(characterId), data);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Echec de la sauvegarde du personnage " + characterId + " : " + e);
        }
    }

    private static string CharacterFileBaseName(int characterId)
    {
        return "character_" + characterId;
    }

    private SaveData BuildSaveData()
    {
        SaveData data = new SaveData();

        data.MyScene = SceneManager.GetActiveScene().name;

        SaveEquipment(data);

        SaveBags(data);

        SaveInventory(data);

        SavePlayer(data);

        SaveChests(data);

        SaveActionButtons(data);

        SaveQuests(data);

        SaveQuestGivers(data);

        return data;
    }

    private static void WriteSaveFile(string fileBaseName, SaveData data)
    {
        // Serialize to memory first. If building/serializing the save
        // data throws partway through, the .dat file already on disk
        // is never touched, so a failed save can no longer wipe out a
        // previously good one.
        BinaryFormatter bf = new BinaryFormatter();
        using (MemoryStream memory = new MemoryStream())
        {
            bf.Serialize(memory, data);

            using (FileStream file = File.Open(Application.persistentDataPath + "/" + fileBaseName + ".dat", FileMode.Create))
            {
                memory.WriteTo(file);
            }
        }
    }

    private void SavePlayer(SaveData data)
    {
        data.MyPlayerData = new PlayerData(Player.MyInstance.MyLevel,
            Player.MyInstance.MyXp.MyCurrentValue, Player.MyInstance.MyXp.MyMaxValue,
            Player.MyInstance.MyHealth.MyCurrentValue, Player.MyInstance.MyHealth.MyMaxValue,
            Player.MyInstance.MyMana.MyCurrentValue, Player.MyInstance.MyMana.MyMaxValue,
            Player.MyInstance.transform.position);
    }

    private void SaveChests(SaveData data)
    {
        for (int i = 0; i < chests.Length; i++)
        {
            data.MyChestData.Add(new ChestData(chests[i].name));

            foreach (Item item in chests[i].MyItems)
            {
                if (chests[i].MyItems.Count > 0)
                {
                    data.MyChestData[i].MyItems.Add(new ItemData(item.MyTitle, item.MySlot.MyItems.Count, item.MySlot.MyIndex));
                }
            }
        }
    }

    private void SaveBags(SaveData data)
    {
        for (int i = 1; i < InventoryScript.MyInstance.MyBags.Count; i++)
        {
            data.MyInventoryData.MyBags.Add(new BagData(InventoryScript.MyInstance.MyBags[i].MySlotCount, InventoryScript.MyInstance.MyBags[i].MyBagButton.MyBagIndex));

        }
    }

    private void SaveEquipment(SaveData data)
    {

        foreach (CharButton charButton in equipment)
        {
            if (charButton.MyEquippedArmor != null)
            {
                data.MyEquipmentData.Add(new EquipmentData(charButton.MyEquippedArmor.MyTitle, charButton.name));
            }
        }
    }

    private void SaveActionButtons(SaveData data)
    {
        for (int i = 0; i < actionButtons.Length; i++)
        {
            if (actionButtons[i].MyUseable != null)
            {
                ActionButtonData action;

                if (actionButtons[i].MyUseable is Spell)
                {
                    action = new ActionButtonData((actionButtons[i].MyUseable as Spell).MyTitle, false, i);
                }
                else
                {
                    action = new ActionButtonData((actionButtons[i].MyUseable as Item).MyTitle, true, i);
                }

                data.MyActionButtonData.Add(action);
            }
        }
    }

    private void SaveInventory(SaveData data)
    {
        List<SlotScript> slots = InventoryScript.MyInstance.GetAllItems();

        foreach (SlotScript slot in slots)
        {
            data.MyInventoryData.MyItems.Add(new ItemData(slot.MyItem.MyTitle, slot.MyItems.Count, slot.MyIndex, slot.MyBag.MyBagIndex));
        }

    }

    private void SaveQuests(SaveData data)
    {
        foreach (Quest quest in Questlog.MyInstance.MyQuests)
        {
            data.MyQuestData.Add(new QuestData(quest.MyTitle, quest.MyDescription, quest.MyCollectObjectives, quest.MyKillObjectives,quest.MyQuestGiver.MyQuestGiverID));
        }
    }

    private void SaveQuestGivers(SaveData data)
    {
        QuestGiver[] questGivers = FindObjectsOfType<QuestGiver>();

        foreach (QuestGiver questGiver in questGivers)
        {
            data.MyQuestGiverData.Add(new QuestGiverData(questGiver.MyQuestGiverID, questGiver.MyCompltedQuests));
        }

    }


    private void Load(SavedGame savedGame)
    {
        try
        {
            SaveData data = ReadSaveFile(savedGame.gameObject.name);
            ApplySaveData(data);
        }
        catch (System.Exception e)
        {
            // A failed load must never delete the save file: the data on
            // disk is almost certainly fine, the problem is in applying it
            // to the current scene. Just report it and bail back to the
            // main menu instead of silently destroying the player's save.
            Debug.LogError("Echec du chargement (" + savedGame.gameObject.name + ") : " + e);
            PlayerPrefs.DeleteKey("Load");
            SceneManager.LoadScene(0);
        }
    }

    /// <summary>
    /// Equivalent de Load(SavedGame) pour un personnage du nouveau systeme
    /// compte -> personnages -- voir SaveCharacter. Renvoie false si aucune
    /// sauvegarde n'existe encore pour ce personnage (TryInitializePlayer
    /// retombe alors sur SetDefaultValues()) plutot que de lancer une
    /// erreur, puisque ce cas est normal pour un personnage jamais encore
    /// sauvegarde.
    /// </summary>
    public bool LoadCharacter(int characterId)
    {
        string fileBaseName = CharacterFileBaseName(characterId);
        string path = Application.persistentDataPath + "/" + fileBaseName + ".dat";

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            SaveData data = ReadSaveFile(fileBaseName);
            ApplySaveData(data);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Echec du chargement du personnage " + characterId + " : " + e);
            return false;
        }
    }

    private static SaveData ReadSaveFile(string fileBaseName)
    {
        BinaryFormatter bf = new BinaryFormatter();
        using (FileStream file = File.Open(Application.persistentDataPath + "/" + fileBaseName + ".dat", FileMode.Open))
        {
            return (SaveData)bf.Deserialize(file);
        }
    }

    private void ApplySaveData(SaveData data)
    {
        LoadEquipment(data);

        LoadBags(data);

        LoadInventory(data);

        LoadPlayer(data);

        LoadChests(data);

        LoadActionButtons(data);

        LoadQuests(data);

        LoadQuestGiver(data);
    }

    private void LoadPlayer(SaveData data)
    {
        Player.MyInstance.MyLevel = data.MyPlayerData.MyLevel;
        Player.MyInstance.UpdateLevel();
        Player.MyInstance.MyHealth.Initialize(data.MyPlayerData.MyHealth, data.MyPlayerData.MyMaxHealth);
        Player.MyInstance.MyMana.Initialize(data.MyPlayerData.MyMana, data.MyPlayerData.MyMaxMana);
        Player.MyInstance.MyXp.Initialize(data.MyPlayerData.MyXp, data.MyPlayerData.MyMaxXP);
        Player.MyInstance.transform.position = new Vector2(data.MyPlayerData.MyX, data.MyPlayerData.MyY);

    }

    private void LoadChests(SaveData data)
    {
        foreach (ChestData chest in data.MyChestData)
        {
            Chest c = Array.Find(chests, x => x.name == chest.MyName);

            foreach (ItemData itemData in chest.MyItems)
            {
                Item item = Instantiate(Array.Find(items, x => x.MyTitle == itemData.MyTitel));
                item.MySlot = c.MyBag.MySlots.Find(x => x.MyIndex == itemData.MySlotIndex);
                c.MyItems.Add(item);
            }
        }

    }

    private void LoadBags(SaveData data)
    {
        foreach (BagData bagData in data.MyInventoryData.MyBags)
        {
            Bag newBag = (Bag)Instantiate(items[0]);

            newBag.Initialize(bagData.MySlotCount);

            InventoryScript.MyInstance.AddBag(newBag, bagData.MyBagIndex);
        }
    }

    private void LoadEquipment(SaveData data)
    {
        foreach (EquipmentData equipmentData in data.MyEquipmentData)
        {
            CharButton cb = Array.Find(equipment, x => x.name == equipmentData.MyType);

            cb.EquipArmor(Array.Find(items, x => x.MyTitle == equipmentData.MyTitle) as Armor);
        }
    }

    private void LoadActionButtons(SaveData data)
    {
        foreach (ActionButtonData buttonData in data.MyActionButtonData)
        {
            if (buttonData.IsItem)
            {
                actionButtons[buttonData.MyIndex].SetUseable(InventoryScript.MyInstance.GetUseable(buttonData.MyAction));
            }
            else
            {
                actionButtons[buttonData.MyIndex].SetUseable(SpellBook.MyInstance.GetSpell(buttonData.MyAction));
            }
        }
    }

    private void LoadInventory(SaveData data)
    {
        foreach (ItemData itemData in data.MyInventoryData.MyItems)
        {
            Item item = Instantiate(Array.Find(items, x => x.MyTitle == itemData.MyTitel));

            for (int i = 0; i < itemData.MyStackCount; i++)
            {
                InventoryScript.MyInstance.PlaceInSpecific(item, itemData.MySlotIndex, itemData.MyBagIndex);
            }
        }
    }

    private void LoadQuests(SaveData data)
    {
        QuestGiver[] questGivers = FindObjectsOfType<QuestGiver>();

        foreach (QuestData questData in data.MyQuestData)
        {
            QuestGiver qg = Array.Find(questGivers, x => x.MyQuestGiverID == questData.MyQuestGiverID);
            Quest q = Array.Find(qg.MyQuests, x => x.MyTitle == questData.MyTitle);
            q.MyQuestGiver = qg;
            q.MyKillObjectives = questData.MyKillObjectives;
            Questlog.MyInstance.AcceptQuest(q);
        }
    }

    private void LoadQuestGiver(SaveData data)
    {
        QuestGiver[] questGivers = FindObjectsOfType<QuestGiver>();

        foreach (QuestGiverData questGiverData in data.MyQuestGiverData)
        {
            QuestGiver questGiver = Array.Find(questGivers, x => x.MyQuestGiverID == questGiverData.MyQuestGiverID);
            questGiver.MyCompltedQuests = questGiverData.MyCompletedQuests;
            questGiver.UpdateQuestStatus();
        }
    }
}
