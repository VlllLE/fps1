using UnityEngine;
using UnityEngine.UI;

namespace CodMovementShooter.UI
{
    /// <summary>
    /// Builds the first-pass HUD hierarchy under a Canvas when references are missing.
    /// Keeps the shipped prefab usable even before Unity re-serializes child wiring.
    /// </summary>
    public static class FpsHudHierarchyBuilder
    {
        private static Sprite _whiteSprite;

        public static void EnsureBuilt(FpsHudController controller)
        {
            if (controller == null)
                return;

            Canvas canvas = controller.GetComponent<Canvas>();
            if (canvas == null)
                canvas = controller.gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            if (controller.GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler = controller.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (controller.GetComponent<GraphicRaycaster>() == null)
                controller.gameObject.AddComponent<GraphicRaycaster>();

            CrosshairView crosshair = controller.Crosshair ?? controller.GetComponentInChildren<CrosshairView>(true);
            AmmoCounterView ammo = controller.AmmoCounter ?? controller.GetComponentInChildren<AmmoCounterView>(true);
            HitMarkerView hitMarker = controller.HitMarker ?? controller.GetComponentInChildren<HitMarkerView>(true);
            DamageNumberView damage = controller.DamageNumbers ?? controller.GetComponentInChildren<DamageNumberView>(true);

            if (crosshair == null)
                crosshair = BuildCrosshair(controller.transform);
            if (hitMarker == null)
                hitMarker = BuildHitMarker(controller.transform);
            if (ammo == null)
                ammo = BuildAmmo(controller.transform);
            if (damage == null)
                damage = BuildDamageNumbers(controller.transform);

            controller.BindViews(crosshair, ammo, hitMarker, damage);

            if (controller.GetComponent<WeaponHudBridge>() == null)
            {
                WeaponHudBridge bridge = controller.gameObject.AddComponent<WeaponHudBridge>();
                bridge.Hud = controller;
            }
        }

        public static GameObject CreateRootCanvas(string name = "FpsHudCanvas")
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(FpsHudController), typeof(WeaponHudBridge));
            FpsHudController controller = root.GetComponent<FpsHudController>();
            WeaponHudBridge bridge = root.GetComponent<WeaponHudBridge>();
            bridge.Hud = controller;
            EnsureBuilt(controller);
            return root;
        }

        private static CrosshairView BuildCrosshair(Transform parent)
        {
            GameObject root = CreateUiObject("Crosshair", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchCenter(rootRect, 80f, 80f);

            RectTransform top = CreateArm(root.transform, "Top");
            RectTransform bottom = CreateArm(root.transform, "Bottom");
            RectTransform left = CreateArm(root.transform, "Left");
            RectTransform right = CreateArm(root.transform, "Right");

            CrosshairView view = root.AddComponent<CrosshairView>();
            view.BindArms(top, bottom, left, right);
            return view;
        }

        private static HitMarkerView BuildHitMarker(Transform parent)
        {
            GameObject root = CreateUiObject("HitMarker", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchCenter(rootRect, 40f, 40f);

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            Image a = CreateDiagonal(root.transform, "ArmA", 45f);
            Image b = CreateDiagonal(root.transform, "ArmB", -45f);

            HitMarkerView view = root.AddComponent<HitMarkerView>();
            view.Bind(group, a, b);
            return view;
        }

        private static AmmoCounterView BuildAmmo(Transform parent)
        {
            GameObject root = CreateUiObject("AmmoCounter", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-36f, 36f);
            rootRect.sizeDelta = new Vector2(220f, 64f);

            GameObject nameGo = CreateUiObject("WeaponName", root.transform);
            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(1f, 1f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(0f, 22f);

            Text nameText = nameGo.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleRight;
            nameText.color = new Color(0.9f, 0.95f, 1f, 0.95f);
            nameText.raycastTarget = false;
            nameText.text = "M4A1";

            GameObject ammoGo = CreateUiObject("AmmoText", root.transform);
            RectTransform ammoRect = ammoGo.GetComponent<RectTransform>();
            ammoRect.anchorMin = new Vector2(0f, 0f);
            ammoRect.anchorMax = new Vector2(1f, 1f);
            ammoRect.pivot = new Vector2(1f, 0f);
            ammoRect.offsetMin = new Vector2(0f, 0f);
            ammoRect.offsetMax = new Vector2(0f, -18f);

            Text text = ammoGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleRight;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "30 | 90";

            Outline outline = ammoGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);

            AmmoCounterView view = root.AddComponent<AmmoCounterView>();
            view.Bind(text, nameText);
            return view;
        }

        private static DamageNumberView BuildDamageNumbers(Transform parent)
        {
            GameObject root = CreateUiObject("DamageNumbers", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchCenter(rootRect, 200f, 200f);

            DamageNumberView view = root.AddComponent<DamageNumberView>();
            view.Bind(rootRect, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            root.SetActive(true);
            return view;
        }

        private static RectTransform CreateArm(Transform parent, string name)
        {
            GameObject go = CreateUiObject(name, parent);
            Image image = go.AddComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = Color.white;
            image.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image CreateDiagonal(Transform parent, string name, float zRotation)
        {
            GameObject go = CreateUiObject(name, parent);
            Image image = go.AddComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = Color.white;
            image.raycastTarget = false;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(3f, 18f);
            rect.localRotation = Quaternion.Euler(0f, 0f, zRotation);
            return image;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchCenter(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Sprite WhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;

            Texture2D tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _whiteSprite.name = "UIWhite";
            return _whiteSprite;
        }
    }
}
