using UnityEngine;

/// <summary>
/// The enmy's follow state
/// </summary>
class FollowState : IState
{
    /// <summary>
    /// A reference to the parent
    /// </summary>
    private Enemy parent;

    private Vector3 offset;

    /// <summary>
    /// This is called whenever we enter the state
    /// </summary>
    /// <param name="parent">The parent enemy</param>
    public void Enter(Enemy parent)
    {
        this.parent = parent;
    }

    /// <summary>
    /// This is called whenever we exit the state
    /// </summary>
    public void Exit()
    {
        parent.Direction = Vector2.zero;
    }

    /// <summary>
    /// This is called as long as we are inside the state
    /// </summary>
    public void Update()
    {
        if (parent.MyTarget != null)//As long as we have a target, then we need to keep moving
        {
            //Find the target's direction
            parent.Direction = ((parent.MyTarget.transform.position+ offset) - parent.transform.position).normalized;

            // AttackState and PathState both measure range off the wrapper
            // transform (transform.parent), not the Rigidbody-driven child
            // transform used above for Direction. Measuring it differently
            // here let the two states disagree on whether we were actually
            // in range, which could make an enemy flicker between
            // Follow/Attack right at the edge of its attack range.
            float distance = Vector2.Distance(parent.MyTarget.transform.parent.position, parent.transform.parent.position);

            string animName = parent.MySpriteRenderer.sprite.name;

            if (animName.Contains("right"))
            {
                offset = new Vector3(0.5f, 0.8f);
            }
            else if (animName.Contains("left"))
            {
                offset = new Vector3(-0.5f, 0.8f);
            }
            else if (animName.Contains("up"))
            {
                offset = new Vector3(0f, 1.2f);
            }
            else if (animName.Contains("down"))
            {
                offset = new Vector3(0, 0);
            }

            if (distance <= parent.MyAttackRange)
            {
                parent.ChangeState(new AttackState());
            }

        }
        if (!parent.InRange)
        {
            parent.ChangeState(new EvadeState());
        } //if we don't have a target, then we need to go back to idle.
       
    }
}
