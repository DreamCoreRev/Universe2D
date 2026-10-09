using UnityEngine;
using UnityEditor;

// Outil d'édition (menu Unity uniquement, ne fait pas partie du jeu) pour
// ajouter 5 squelettes dans la scène actuellement ouverte.
//
// Pourquoi dupliquer "EnemyParent" plutôt que de glisser Enemy.prefab
// depuis le Project : le prefab Enemy.prefab a sa référence d'Animator
// Controller cassée (elle ne pointe plus vers aucun asset du projet), donc
// un nouvel exemplaire posé depuis le Project n'aurait pas d'animation.
// "EnemyParent", lui, est déjà présent dans Demo.unity et a été "déconnecté"
// du prefab (Unity dit "unpacked") avec son propre Animator Controller
// correctement réglé (celui du squelette classique) -- le dupliquer avec
// Instantiate copie tout tel quel : apparence, script d'IA, collision.
public static class AjouterSquelettes
{
	[MenuItem("Universe2D/Ajouter 5 Squelettes")]
	public static void Ajouter()
	{
		GameObject original = GameObject.Find("EnemyParent");
		if (original == null)
		{
			Debug.LogError("AjouterSquelettes : impossible de trouver \"EnemyParent\" dans la scène ouverte. Ouvre Demo.unity puis réessaie.");
			return;
		}

		Vector3 basePos = original.transform.position;

		// Décalages autour du groupe d'ennemis existant (en unités du monde,
		// pas en pixels). Modifie ces valeurs si tu veux les placer ailleurs.
		Vector3[] offsets = new Vector3[]
		{
			new Vector3( 4f,  3f, 0f),
			new Vector3(-4f,  3f, 0f),
			new Vector3( 4f, -3f, 0f),
			new Vector3(-4f, -3f, 0f),
			new Vector3( 0f,  5f, 0f),
		};

		for (int i = 0; i < offsets.Length; i++)
		{
			GameObject clone = (GameObject)Object.Instantiate(original, basePos + offsets[i], original.transform.rotation);
			clone.name = "EnemyParent (Squelette " + (i + 1) + ")";
			Undo.RegisterCreatedObjectUndo(clone, "Ajouter Squelette");
		}

		Debug.Log("AjouterSquelettes : 5 squelettes ajoutés autour de (" + basePos.x.ToString("0.0") + ", " + basePos.y.ToString("0.0") + "). N'oublie pas de faire Ctrl+S pour sauvegarder la scène.");
	}
}
