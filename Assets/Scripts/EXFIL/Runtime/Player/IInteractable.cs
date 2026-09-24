using UnityEngine;

namespace EXFIL.Player
{
    /// <summary>Anything in the world the player can press E on: loot, doors, stations, bodies.</summary>
    public interface IInteractable
    {
        string GetPrompt(GameObject actor);
        bool CanInteract(GameObject actor);
        void Interact(GameObject actor);
        float GetInteractTime(GameObject actor);
        Transform Transform { get; }
    }
}
