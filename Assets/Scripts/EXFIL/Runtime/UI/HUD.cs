using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EXFIL.Characters;
using EXFIL.Core;
using EXFIL.Player;
using EXFIL.Raid;

namespace EXFIL.UI
{
    /// <summary>In-raid HUD: body part health, stamina, ammo, timer, crosshair, prompts.</summary>
    public class HUD : MonoBehaviour
    {
        private Canvas _canvas;
        private readonly Dictionary<BodyPart, Image> _partBars = new Dictionary<BodyPart, Image>();
        private Image _staminaBar;
        private Text _ammoText;
        private Text _timerText;
        private Text _promptText;
        private Text _effectText;
        private Image _damageFlash;
        private GameObject _crosshair;
        private float _flashTimer;

        private HealthController _health;
        private PlayerActor _player;

        public void Build(Transform parent)
        {
            _canvas = UIFactory.Canvas("HUD");
            _canvas.transform.SetParent(parent, false);
            RectTransform root = _canvas.GetComponent<RectTransform>();

            // bottom-left: body part health
            RectTransform healthPanel = UIFactory.Panel(root, "Health", new Color(0f, 0f, 0f, 0.35f),
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            healthPanel.anchorMin = new Vector2(0.01f, 0.02f);
            healthPanel.anchorMax = new Vector2(0.16f, 0.42f);
            healthPanel.offsetMin = Vector2.zero;
            healthPanel.offsetMax = Vector2.zero;

            BodyPart[] order =
            {
                BodyPart.Head, BodyPart.Thorax, BodyPart.Stomach,
                BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg
            };
            for (int i = 0; i < order.Length; i++)
            {
                float y = 1f - (i + 0.5f) / order.Length;
                Image bar = CreateBar(healthPanel, order[i].ToString(), y);
                _partBars[order[i]] = bar;
            }

            // bottom-right: stamina
            RectTransform staminaPanel = UIFactory.Panel(root, "Stamina", new Color(0f, 0f, 0f, 0.35f),
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            staminaPanel.anchorMin = new Vector2(0.82f, 0.03f);
            staminaPanel.anchorMax = new Vector2(0.99f, 0.06f);
            staminaPanel.offsetMin = Vector2.zero;
            staminaPanel.offsetMax = Vector2.zero;
            _staminaBar = CreateBar(staminaPanel, "STAMINA", 0.5f);

            // top-right: timer
            Text timerLabel = UIFactory.Text(root, "TIME", 18, TextAnchor.UpperRight);
            timerLabel.rectTransform.anchorMin = new Vector2(0.85f, 0.92f);
            timerLabel.rectTransform.anchorMax = new Vector2(0.99f, 0.99f);
            timerLabel.rectTransform.offsetMin = Vector2.zero;
            timerLabel.rectTransform.offsetMax = Vector2.zero;
            _timerText = timerLabel;

            // bottom-center: ammo
            Text ammo = UIFactory.Text(root, "- / -", 24, TextAnchor.LowerRight, new Color(1f, 0.85f, 0.4f));
            ammo.rectTransform.anchorMin = new Vector2(0.72f, 0.08f);
            ammo.rectTransform.anchorMax = new Vector2(0.99f, 0.14f);
            ammo.rectTransform.offsetMin = Vector2.zero;
            ammo.rectTransform.offsetMax = Vector2.zero;
            _ammoText = ammo;

            // center prompt
            Text prompt = UIFactory.Text(root, string.Empty, 20, TextAnchor.MiddleCenter);
            prompt.rectTransform.anchorMin = new Vector2(0.3f, 0.28f);
            prompt.rectTransform.anchorMax = new Vector2(0.7f, 0.34f);
            prompt.rectTransform.offsetMin = Vector2.zero;
            prompt.rectTransform.offsetMax = Vector2.zero;
            _promptText = prompt;

            // status effects
            Text effects = UIFactory.Text(root, string.Empty, 15, TextAnchor.MiddleLeft, new Color(1f, 0.6f, 0.5f));
            effects.rectTransform.anchorMin = new Vector2(0.18f, 0.03f);
            effects.rectTransform.anchorMax = new Vector2(0.6f, 0.2f);
            effects.rectTransform.offsetMin = Vector2.zero;
            effects.rectTransform.offsetMax = Vector2.zero;
            _effectText = effects;

            // crosshair
            GameObject crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            crosshair.transform.SetParent(root, false);
            RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.sizeDelta = new Vector2(3f, 3f);
            crosshair.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
            _crosshair = crosshair;

            // damage flash
            RectTransform flash = UIFactory.Panel(root, "Flash", new Color(0.7f, 0f, 0f, 0f), Vector2.zero, Vector2.one);
            flash.offsetMin = Vector2.zero;
            flash.offsetMax = Vector2.zero;
            _damageFlash = flash.GetComponent<Image>();

            GameEvents.Subscribe<DamageAppliedEvent>(OnDamage);
        }

        private Image CreateBar(Transform parent, string label, float normalizedY)
        {
            GameObject row = new GameObject(label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, normalizedY - 0.05f);
            rect.anchorMax = new Vector2(0.95f, normalizedY + 0.05f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(row.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.32f, 0.15f);
            fillRect.anchorMax = new Vector2(0.99f, 0.85f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.75f, 0.22f, 0.18f);

            Text text = UIFactory.Text(row.transform, label, 11, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.32f, 1f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            return fillImage;
        }

        private void OnDamage(DamageAppliedEvent evt)
        {
            _flashTimer = 0.35f;
        }

        public void Bind(PlayerActor player)
        {
            _player = player;
            _health = player != null ? player.GetComponent<HealthController>() : null;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                Color color = _damageFlash.color;
                color.a = Mathf.Clamp01(_flashTimer / 0.35f) * 0.35f;
                _damageFlash.color = color;
            }

            if (_health != null)
            {
                foreach (KeyValuePair<BodyPart, Image> pair in _partBars)
                {
                    BodyPartState state = _health.GetPart(pair.Key);
                    if (state == null) continue;
                    pair.Value.fillAmount = state.Ratio;
                    pair.Value.color = state.Destroyed
                        ? new Color(0.18f, 0.18f, 0.18f)
                        : Color.Lerp(new Color(0.6f, 0.12f, 0.1f), new Color(0.2f, 0.6f, 0.25f), state.Ratio);
                }

                string effects = string.Empty;
                for (int i = 0; i < _health.Effects.Count; i++)
                    effects += _health.Effects[i].Type + "  ";
                _effectText.text = effects;
            }

            if (_player != null)
            {
                FpsController motor = _player.Motor;
                if (motor != null) _staminaBar.fillAmount = motor.Stamina / Mathf.Max(1f, motor.MaxStamina);

                PlayerLoadout loadout = _player.Loadout;
                if (loadout != null && loadout.ActiveWeapon != null && loadout.ActiveWeapon.Weapon != null)
                {
                    Combat.WeaponHandler handler = loadout.ActiveWeapon;
                    int capacity = 0;
                    Items.MagazineItemDefinition magDef = null;
                    Items.ItemInstance mag = handler.Weapon.GetMagazine();
                    if (mag != null) magDef = mag.Def as Items.MagazineItemDefinition;
                    if (magDef != null) capacity = magDef.Capacity;
                    _ammoText.text = handler.Weapon.AmmoCount + " / " + capacity +
                                     (handler.IsReloading ? "  RELOADING" : string.Empty) +
                                     (handler.IsJammed ? "  JAMMED!" : string.Empty);
                }
                else if (_ammoText != null) _ammoText.text = string.Empty;

                InteractionSystem interaction = _player.Interaction;
                if (interaction != null)
                {
                    _promptText.text = string.IsNullOrEmpty(interaction.CurrentPrompt)
                        ? string.Empty
                        : "[E] " + interaction.CurrentPrompt + (interaction.Progress > 0f
                            ? "  " + (interaction.Progress * 100f).ToString("0") + "%"
                            : string.Empty);
                }

                if (_crosshair != null)
                    _crosshair.SetActive(loadout == null || !loadout.IsBusy);
            }

            RaidManager raid = RaidManager.Instance;
            if (raid != null && _timerText != null)
            {
                float time = Mathf.Max(0f, raid.TimeLeft);
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);
                _timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
            }
        }
    }
}
