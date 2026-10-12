using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathState : IState
{
    private Vector3 destination;

    private Vector3 current;

    private Vector3 goal;

    private Transform transform;

    private Enemy parent;

    private Vector3Int targetCell;

    /// <summary>
    /// Case de la tilemap qui contient cette position du monde. Remplace
    /// MyCurrentTile (rempli seulement au contact physique d'une tuile) :
    /// sur le serveur, ce champ pouvait designer une tuile lointaine ou
    /// perimee, et le monstre partait alors en courant vers elle au lieu de
    /// vers le joueur.
    /// </summary>
    private Vector3Int CellOf(Vector3 worldPosition)
    {
        return parent.MyAstar.MyTilemap.WorldToCell(worldPosition);
    }

    public void Enter(Enemy parent)
    {
        this.parent = parent;

        this.transform = parent.transform.parent;

        // MyTarget est le Character (potentiellement n'importe quel joueur
        // connecte, pas forcement Player.MyInstance) qui est entre dans la
        // Range du monstre -- voir Range.cs/Enemy.SetTarget. En reseau,
        // seul le serveur execute cet etat (voir Enemy.ShouldRunAI).
        //
        // MyCurrentTile n'est rempli qu'au premier contact physique avec une
        // tuile (voir CurrentTileScript) : un joueur tout juste connecte/
        // spawn (ou le monstre lui-meme) peut donc encore valoir null ici,
        // le temps d'un ou deux FixedUpdate. On evite plutot que de planter.
        if (parent.MyTarget == null)
        {
            parent.Evade("PathState : plus de cible");
            return;
        }

        if (!parent.InRange)
        {
            parent.Evade("PathState : cible hors de portee d'aggro");
            return;
        }

        Vector3Int startCell = CellOf(parent.transform.position);
        targetCell = CellOf(parent.MyTarget.transform.position);

        // Meme case que la cible : pas de chemin a calculer, on la suit
        // directement (avant, MyPath restait null et le monstre fuyait).
        if (startCell == targetCell)
        {
            parent.ChangeState(new FollowState());
            return;
        }

        parent.MyPath = parent.MyAstar.Algorithm(parent.transform.position, parent.MyTarget.transform.position);

        if (parent.MyPath == null)
        {
            // Pas de chemin A* (cible sur une case bloquee / hors tilemap,
            // etc.) : au lieu de fuir des le debut du combat, on fonce droit
            // sur la cible. FollowState gere deja la sortie en Evade si la
            // cible quitte vraiment la portee d'aggro.
            Debug.Log("[Enemy] " + parent.name + " : aucun chemin A*, poursuite directe");
            parent.ChangeState(new FollowState());
            return;
        }

        current = parent.MyPath.Pop();
        destination = parent.MyPath.Count > 0 ? parent.MyPath.Pop() : current;
        this.goal = parent.transform.position;
    }

    public void Exit()
    {
        parent.MyPath = null;
    }

    public void Update()
    {
        if (parent.MyTarget == null)
        {
            parent.Evade("PathState : la cible a disparu");
            return;
        }

        if (parent.MyPath != null)
        {
            // IMPORTANT : dans Demo.unity, l'enfant "Enemy" est tres decale
            // de son wrapper (transform.parent, qui porte le Rigidbody2D).
            // Les cases A* sont en coordonnees MONDE de l'enfant : il faut
            // donc avancer l'enfant vers la destination, et deplacer le
            // wrapper de la meme quantite -- sinon le monstre est
            // teleporte de l'ecart wrapper/enfant et part au loin.
            Vector3 childPos = parent.transform.position;
            Vector2 newChildPos = Vector2.MoveTowards(childPos, destination, parent.CurrentSpeed * Time.deltaTime);
            transform.position += new Vector3(newChildPos.x - childPos.x, newChildPos.y - childPos.y, 0f);

            parent.ActivateLayer("WalkLayer");
           
            Vector3Int dest = parent.MyAstar.MyTilemap.WorldToCell(destination);
            Vector3Int cur = parent.MyAstar.MyTilemap.WorldToCell(current);

            float distance = Vector2.Distance(destination, parent.transform.position);

            float totalDistance = Vector2.Distance(parent.MyTarget.transform.position, parent.transform.position);

            if (cur.y > dest.y)
            {
                parent.Direction = Vector2.down;
            }
            else if (cur.y < dest.y)
            {
                parent.Direction = Vector2.up;
            }
            if (cur.y == dest.y)
            {
                if (cur.x > dest.x)
                {
                    parent.Direction = Vector2.left;
                }
                else if (cur.x < dest.x)
                {
                    parent.Direction = Vector2.right;
                }
            }
            if (totalDistance <= parent.MyAttackRange)
            {
                parent.ChangeState(new AttackState());
            }
            else if (CellOf(parent.MyTarget.transform.position) == CellOf(parent.transform.position))
            {
                parent.ChangeState(new FollowState());
            }

            if (distance <= 0.02f)
            {
                if (parent.MyPath.Count > 0)
                {
                    current = destination;
                    destination = parent.MyPath.Pop();

                    if (targetCell != CellOf(parent.MyTarget.transform.position)) //Then the player has moved
                    {
                        parent.ChangeState(new PathState());
                    }
                }
                else
                {
                    parent.MyPath = null;
                    parent.ChangeState(new PathState());
                }
            }
        }
    }
}
