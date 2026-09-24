using UnityEngine;
using EXFIL.Core;
using EXFIL.Player;

namespace EXFIL.Hideout
{
    public enum StationKind { Stash, Workbench, Medstation, NutritionUnit, WaterCollector, Generator, Lavatory, Greenhouse, ShootingRange, Rest }

    /// <summary>An interactable object inside the 3D bunker.</summary>
    public class HideoutStation : MonoBehaviour, IInteractable
    {
        public StationKind Kind = StationKind.Workbench;
        public string StationName = "Workbench";
        public string LinkedModuleId = "workbench";
        public float InteractTime = 0f;

        public Transform Transform { get { return transform; } }

        public string GetPrompt(GameObject actor)
        {
            switch (Kind)
            {
                case StationKind.Stash: return "Open stash";
                case StationKind.Greenhouse: return "Tend the greenhouse";
                default: return "Use " + StationName;
            }
        }

        public bool CanInteract(GameObject actor)
        {
            return true;
        }

        public float GetInteractTime(GameObject actor)
        {
            return InteractTime;
        }

        public void Interact(GameObject actor)
        {
            switch (Kind)
            {
                case StationKind.Stash:
                    UI.GameUI.Instance?.OpenStash();
                    break;
                case StationKind.Workbench:
                    UI.GameUI.Instance?.OpenCrafting(LinkedModuleId);
                    break;
                case StationKind.Medstation:
                case StationKind.NutritionUnit:
                case StationKind.Lavatory:
                    UI.GameUI.Instance?.OpenCrafting(LinkedModuleId);
                    break;
                case StationKind.Greenhouse:
                    UI.GameUI.Instance?.OpenGarden();
                    break;
                case StationKind.WaterCollector:
                    UI.GameUI.Instance?.OpenCrafting(LinkedModuleId);
                    break;
                case StationKind.Generator:
                    UI.GameUI.Instance?.OpenFuel();
                    break;
                default:
                    UI.GameUI.Instance?.OpenHideout();
                    break;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.35f);
        }
    }
}
