using UnityEngine;
using EXFIL.Player;

namespace EXFIL.Characters
{
    /// <summary>
    /// Gives the local player a real 3D body: spawned from the operator definition when the
    /// raid / hideout session starts. The body renders shadows only (the camera sits inside
    /// the head), so you see your own silhouette on the ground and your rigged animation in
    /// mirrors and on other clients without the head clipping into the view.
    /// </summary>
    public class PlayerBodyMount : MonoBehaviour
    {
        [Tooltip("Body is spawned as a child of this transform.")]
        public Transform Anchor;

        public bool ShadowsOnly = true;

        public PlayerActor Owner;
        public EquipmentController Equipment;

        private CharacterBody3D _body;

        public CharacterBody3D Body { get { return _body; } }

        public CharacterBody3D Build(CharacterDefinition definition)
        {
            if (_body != null) return _body;
            Transform parent = Anchor != null ? Anchor : transform;
            _body = CharacterBody3D.Spawn(definition, parent, parent.position, parent.rotation);
            _body.transform.localPosition = Vector3.zero;
            _body.transform.localRotation = Quaternion.identity;
            if (ShadowsOnly) _body.SetLocalPlayerView(true);

            if (Owner != null) Owner.Body = _body;
            if (Equipment != null)
            {
                Equipment.Body = _body;
                Equipment.Rebuild();
            }
            return _body;
        }
    }
}
