using UnityEngine;

namespace EXFIL.Core
{
    /// <summary>
    /// One input facade that works with both the new Input System and the legacy
    /// Input Manager (Unity defines ENABLE_INPUT_SYSTEM / ENABLE_LEGACY_INPUT_MANAGER
    /// depending on the active input handling in Player Settings).
    /// </summary>
    public static class EXFILInput
    {
        public struct Frame
        {
            public Vector2 Move;        // x = strafe, y = forward
            public Vector2 Look;        // mouse / stick delta
            public bool Sprint;
            public bool Crouch;
            public bool Prone;
            public bool Fire;
            public bool FirePressed;
            public bool Aim;
            public bool Reload;
            public bool Interact;
            public bool InteractPressed;
            public bool Inventory;
            public bool Heal;
            public int WeaponSlot;      // 0 = none, 1..4
            public bool NextWeapon;
            public bool Grenade;
            public bool ToggleFireMode;
            public bool CheckChamber;
        }

        private static bool _fireDown;
        private static bool _interactDown;
        private static Vector2 _lookAccum;

        public static bool CursorLocked { get; private set; }

        public static void SetCursorLock(bool locked)
        {
            CursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public static Frame Sample()
        {
            Frame f = new Frame();

#if ENABLE_INPUT_SYSTEM
            f.Look = SampleLookNew();
            f.Move = SampleMoveNew();
            f.Sprint = KeyNew(UnityEngine.InputSystem.Key.LeftShift);
            f.Crouch = KeyNew(UnityEngine.InputSystem.Key.C);
            f.Prone = KeyNew(UnityEngine.InputSystem.Key.X);
            f.Fire = MouseNew(0);
            f.Aim = MouseNew(1);
            f.Reload = KeyNew(UnityEngine.InputSystem.Key.R);
            f.Interact = KeyNew(UnityEngine.InputSystem.Key.E);
            f.Inventory = KeyNewDown(UnityEngine.InputSystem.Key.Tab) || KeyNewDown(UnityEngine.InputSystem.Key.I);
            f.Heal = KeyNewDown(UnityEngine.InputSystem.Key.H);
            f.Grenade = KeyNewDown(UnityEngine.InputSystem.Key.G);
            f.ToggleFireMode = KeyNewDown(UnityEngine.InputSystem.Key.B);
            f.CheckChamber = KeyNewDown(UnityEngine.InputSystem.Key.T);
            f.NextWeapon = KeyNewDown(UnityEngine.InputSystem.Key.Q);
            f.WeaponSlot = DigitNew();
#else
            f.Look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            f.Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            f.Sprint = Input.GetKey(KeyCode.LeftShift);
            f.Crouch = Input.GetKey(KeyCode.C);
            f.Prone = Input.GetKey(KeyCode.X);
            f.Fire = Input.GetMouseButton(0);
            f.Aim = Input.GetMouseButton(1);
            f.Reload = Input.GetKeyDown(KeyCode.R);
            f.Interact = Input.GetKey(KeyCode.E);
            f.Inventory = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I);
            f.Heal = Input.GetKeyDown(KeyCode.H);
            f.Grenade = Input.GetKeyDown(KeyCode.G);
            f.ToggleFireMode = Input.GetKeyDown(KeyCode.B);
            f.CheckChamber = Input.GetKeyDown(KeyCode.T);
            f.NextWeapon = Input.GetKeyDown(KeyCode.Q);
            f.WeaponSlot = DigitLegacy();
#endif
            f.FirePressed = f.Fire && !_fireDown;
            f.InteractPressed = f.Interact && !_interactDown;
            _fireDown = f.Fire;
            _interactDown = f.Interact;
            return f;
        }

        public static void ResetEdges()
        {
            _fireDown = false;
            _interactDown = false;
            _lookAccum = Vector2.zero;
        }

#if ENABLE_INPUT_SYSTEM
        private static Vector2 SampleLookNew()
        {
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return Vector2.zero;
            Vector2 delta = mouse.delta.ReadValue();
            return new Vector2(delta.x, -delta.y) * 0.06f;
        }

        private static Vector2 SampleMoveNew()
        {
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = 0f, y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }

        private static bool KeyNew(UnityEngine.InputSystem.Key key)
        {
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb[key].isPressed;
        }

        private static bool KeyNewDown(UnityEngine.InputSystem.Key key)
        {
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }

        private static bool MouseNew(int button)
        {
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return false;
            switch (button)
            {
                case 0: return mouse.leftButton.isPressed;
                case 1: return mouse.rightButton.isPressed;
                default: return mouse.middleButton.isPressed;
            }
        }

        private static int DigitNew()
        {
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return 0;
            if (kb.digit1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame) return 3;
            if (kb.digit4Key.wasPressedThisFrame) return 4;
            return 0;
        }
#else
        private static int DigitLegacy()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
            return 0;
        }
#endif
    }
}
