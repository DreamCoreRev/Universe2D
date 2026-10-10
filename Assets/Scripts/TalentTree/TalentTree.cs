using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TalentTree : MonoBehaviour
{

    private int points = 20;

    [SerializeField]
    private Talent[] talents;

    [SerializeField]
    private Talent[] unlockedByDefault;

    [SerializeField]
    private Text talentPointText;

    public int MyPoints
    {
        get
        {
            return points;
        }

        set
        {
            points = value;
            UpdateTalentPointText();
        }
    }

    // Empeche Start() d'ecraser l'etat remis en place par ApplySavedTalents
    // s'il s'execute apres (voir ApplySavedTalents -- l'ordre entre les
    // Start() de deux GameObjects differents n'est pas garanti par Unity).
    private bool externallyInitialized = false;



    // Start is called before the first frame update
    void Start()
    {
        if (!externallyInitialized)
        {
            ResetTalents();
        }
    }

    public void TryUseTalent(Talent talent)
    {
        if (MyPoints > 0 && talent.Click())
        {
            MyPoints--;
        }
        if (MyPoints == 0)
        {
            foreach (Talent t in talents)
            {
                if (t.MyCurrentCount == 0)
                {
                    t.Lock();
                }
            }
        }
    }


    private void ResetTalents()
    {
        UpdateTalentPointText();

        foreach (Talent talent in talents)
        {
            talent.Lock();
        }

        foreach (Talent talent in unlockedByDefault)
        {
            talent.Unlock();
        }
    }

    private void UpdateTalentPointText()
    {
        talentPointText.text = points.ToString();
    }

    /// <summary>
    /// Pour SaveManager.SaveTalents : un TalentData par talent appris (rang
    /// > 0), identifie par le nom de son GameObject (Fire_1, Ice_2, ... voir
    /// Demo.unity) puisque Talent n'a pas de champ titre comme Item.
    /// </summary>
    public List<TalentData> GetSaveData()
    {
        List<TalentData> result = new List<TalentData>();

        foreach (Talent talent in talents)
        {
            if (talent.MyCurrentCount > 0)
            {
                result.Add(new TalentData(talent.gameObject.name, talent.MyCurrentCount));
            }
        }

        return result;
    }

    /// <summary>
    /// Pour SaveManager.LoadTalents : reconstruit l'etat des talents a
    /// partir d'une sauvegarde. Remet tout a zero (ResetTalents) puis
    /// rejoue les clics necessaires pour chaque talent sauvegarde -- en
    /// passant par Talent.Click() (pas en ecrivant MyCurrentCount
    /// directement) pour que les effets du talent (ex: ImprovedFireball qui
    /// reduit le temps de cast, Blizzard qui apprend le sort...) soient
    /// bien ré-appliqués, exactement comme lors d'un vrai clic.
    ///
    /// Un talent enfant ne se debloque qu'une fois son parent au rang max
    /// (voir Talent.Click()/childTalent) : on fait donc plusieurs passes
    /// sur talents[] jusqu'a ce qu'aucun progres ne soit plus possible, pour
    /// ne pas dependre de l'ordre du tableau.
    /// </summary>
    public void ApplySavedTalents(List<TalentData> savedTalents)
    {
        externallyInitialized = true;
        ResetTalents();

        if (savedTalents == null || savedTalents.Count == 0)
        {
            return;
        }

        Dictionary<string, int> targets = new Dictionary<string, int>();
        foreach (TalentData data in savedTalents)
        {
            targets[data.MyName] = data.MyCount;
        }

        bool progress = true;
        int safety = 0;
        while (progress && safety < 50)
        {
            progress = false;
            safety++;

            foreach (Talent talent in talents)
            {
                int target;
                if (!targets.TryGetValue(talent.gameObject.name, out target))
                {
                    continue;
                }

                while (talent.MyCurrentCount < target && talent.Click())
                {
                    MyPoints--;
                    progress = true;
                }
            }
        }

        // Meme comportement de fin de points qu'un vrai joueur en jeu (voir
        // TryUseTalent) : une fois les 20 points depenses, les talents
        // jamais pris restent verrouilles visuellement.
        if (MyPoints == 0)
        {
            foreach (Talent t in talents)
            {
                if (t.MyCurrentCount == 0)
                {
                    t.Lock();
                }
            }
        }
    }
}
