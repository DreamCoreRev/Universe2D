using UnityEngine;
using UnityEngine.EventSystems;

// Petits utilitaires partagés pour que les taps fonctionnent comme les clics
// souris. EventSystem.IsPointerOverGameObject() sans argument ne regarde que
// le pointeur "souris" (id -1), qu'un vrai doigt tactile ne met jamais à
// jour -- du coup toute vérification "est-ce qu'on est sur une UI ?" sans
// cette classe répond presque toujours "non" sur téléphone, même en tapant
// sur un bouton. Et Input.mousePosition peut ne pas suivre le bon doigt
// quand on touche l'écran à deux endroits à la fois (ex: joystick tactile +
// un tap ailleurs) -- GetPointerPosition() prend alors la position du
// premier doigt réel plutôt que la souris simulée.
public static class TouchInput
{
    public static bool IsPointerOverUI()
    {
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                {
                    return true;
                }
            }

            return false;
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    public static Vector3 GetPointerPosition()
    {
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).position;
        }

        return Input.mousePosition;
    }
}
