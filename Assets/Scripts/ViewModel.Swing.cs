using UnityEngine;

namespace VoidFlow
{
    // Knife swing effects: if a cut reaches a surface it bites in with sparks and a clink.
    public partial class ViewModel
    {
        const float SlashReach = 2.4f;

        void SwingHit()
        {
            if (Physics.Raycast(transform.position, transform.forward, out var hit, SlashReach, player ? player.collisionMask : (LayerMask)~0, QueryTriggerInteraction.Ignore)
                && !(player && hit.collider.transform.IsChildOf(player.transform)))
            {
                FxLibrary.Impact(hit.point, hit.normal);
                Play(WeaponSounds.Dry, 0.9f);
            }
        }
    }
}
