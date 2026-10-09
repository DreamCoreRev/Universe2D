using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpellBook : MonoBehaviour
{
    private static SpellBook instance;

    public static SpellBook MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SpellBook>();
            }

            return instance;
        }
    }

    /// <summary>
    /// A reference to the players casting bar
    /// </summary>
    [SerializeField]
    private Image castingBar;

    /// <summary>
    /// A reference to the spell name on the casting bar
    /// </summary>
    [SerializeField]
    private Text currentSpell;

    /// <summary>
    /// A reference to the casting time on the casting bar
    /// </summary>
    [SerializeField]
    private Text castTime;

    /// <summary>
    /// A reference to the icon on the casting bar
    /// </summary>
    [SerializeField]
    private Image icon;

    /// <summary>
    /// A canvas group that is sitting on the casting bar, so that we can fade the whole bar
    /// </summary>
    [SerializeField]
    private CanvasGroup canvasGroup;

    /// <summary>
    /// All spells in the spellbook
    /// </summary>
    [SerializeField]
    private Spell[] spells;

    /// <summary>
    /// A reference to the coroutine that throws spells
    /// </summary>
    private Coroutine spellRoutine;

    /// <summary>
    /// A reference to the coroutine that fades out the bar
    /// </summary>
    private Coroutine fadeRoutine;

    [SerializeField]
    private GameObject[] obtainableSpells;


    /// <summary>
    /// Cast a spell at an enemy
    /// </summary>
    /// <param name="index">The index of the spell you would like to cast, the first spells is index 0</param>
    /// <returns></returns>
    public void Cast(ICastable castable)
    {
        //Resets the fillamount on the bar
        castingBar.fillAmount = 0;

        //Changes the bars color to fit the current spell
        castingBar.color = castable.MyBarColor;

        //Changes the text on the bar so that we can read what spell we are casting
        currentSpell.text = castable.MyTitle;

        //Changes the icon on the casting bar
        icon.sprite = castable.MyIcon;

        //Starts casting the spells
        spellRoutine = StartCoroutine(Progress(castable));

        //Starts fading the bar
        fadeRoutine = StartCoroutine(FadeBar());
    }

    public IEnumerator CastCooldown(ICastable castable)
    {
        castable.OnCastDone();
        castable.OnCooldown = true;
        yield return new WaitForSeconds(castable.MyCooldown);
        castable.OnCooldown = false;
    }

    /// <summary>
    /// Updates the castingbar accoring to the current progress of the cast
    /// </summary>
    /// <param name="index">Index of the spell to cast</param>
    /// <returns></returns>
    private IEnumerator Progress(ICastable castable)
    {
        //How much time has passed since we started casting the spell
        float timePassed = Time.deltaTime;

        //How fast are we going to move the bar
        float rate = 1.0f / castable.MyCastTime;

        //The current progress of the cast 
        float progress = 0.0f;

        while (progress <= 1.0)//As long as we are not done casting
        {
            //Updates the bar based on the progress
            castingBar.fillAmount = Mathf.Lerp(0, 1, progress);

            //Increases the progress
            progress += rate * Time.deltaTime;

            //Updates the time passed
            timePassed += Time.deltaTime;

            //Updates the cast time text
            castTime.text = (castable.MyCastTime - timePassed).ToString("F2");

            if (castable.MyCastTime - timePassed < 0) //Makes sure that the time doesn't go below 0
            {
                castTime.text = "0.00";

                if (castable.MyCooldown > 0)
                {
                    StartCoroutine(CastCooldown(castable));
                }
            }

            yield return null;
        }

        StopCating();//Stops our cast when we are done
    }

    /// <summary>
    /// Fades the bar in on the screen when we start casting
    /// </summary>
    /// <returns></returns>
    private IEnumerator FadeBar()
    {
        //How fast are we going to fade the bar
        float rate = 1.0f / 0.50f;

        //The current fade progress
        float progress = 0.0f;

        while (progress <= 1.0)//As long as we are not done fading
        {
            //Updates the alpha on the canvasgroup
            canvasGroup.alpha = Mathf.Lerp(0, 1, progress);

            //Updates the progress
            progress += rate * Time.deltaTime;

            yield return null;
        }
    }

    /// <summary>
    /// Stops a cast
    /// </summary>
    public void StopCating()
    {
        //Stops the faderoutine
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            canvasGroup.alpha = 0;
            fadeRoutine = null;
        }
        //Stops the spellroutine
        if (spellRoutine != null)
        {
            StopCoroutine(spellRoutine);
            spellRoutine = null;
        }
    }

    
    public void LearnSpell(string name)
    {
        switch (name.ToLower())
        {
            case "rain of fire":
                obtainableSpells[0].SetActive(true);
                break;
            case "blizzard":
                obtainableSpells[1].SetActive(true);
                break;
            case "chainlightning":
                obtainableSpells[2].SetActive(true);
                break;

        }
    }

    public Spell GetSpell(string spellName)
    {
        Spell spell = Array.Find(spells, x => x.MyTitle == spellName);

        return spell;
    }
}
