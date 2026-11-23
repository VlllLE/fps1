using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodMovementShooter.UI
{
    /// <summary>
    /// Optional floating damage / hit confirmation numbers near screen center.
    /// Disabled by default on the prefab; enable via FpsHudController.
    /// </summary>
    public sealed class DamageNumberView : MonoBehaviour
    {
        [SerializeField] private RectTransform spawnRoot;
        [SerializeField] private Font font;
        [SerializeField] private int poolSize = 8;
        [SerializeField] private float lifetime = 0.7f;
        [SerializeField] private float riseDistance = 48f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color headshotColor = new Color(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private int fontSize = 22;

        private readonly List<Entry> _pool = new List<Entry>();
        private bool _built;

        private struct Entry
        {
            public RectTransform Rect;
            public Text Label;
            public CanvasGroup Group;
            public float SpawnTime;
            public Vector2 StartPos;
            public bool Active;
        }

        public void Bind(RectTransform root, Font uiFont)
        {
            spawnRoot = root;
            font = uiFont;
            EnsurePool();
        }

        public void Show(float damage, bool headshot = false)
        {
            if (!isActiveAndEnabled)
                return;

            EnsurePool();

            int index = -1;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].Active)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                index = 0;

            Entry entry = _pool[index];
            entry.Active = true;
            entry.SpawnTime = Time.unscaledTime;
            entry.StartPos = new Vector2(Random.Range(-24f, 24f), Random.Range(20f, 40f));
            entry.Rect.anchoredPosition = entry.StartPos;
            entry.Label.text = Mathf.RoundToInt(Mathf.Max(0f, damage)).ToString();
            entry.Label.color = headshot ? headshotColor : normalColor;
            entry.Group.alpha = 1f;
            entry.Rect.gameObject.SetActive(true);
            _pool[index] = entry;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _pool.Count; i++)
            {
                Entry entry = _pool[i];
                if (!entry.Active)
                    continue;

                float t = (now - entry.SpawnTime) / Mathf.Max(0.0001f, lifetime);
                if (t >= 1f)
                {
                    entry.Active = false;
                    entry.Rect.gameObject.SetActive(false);
                    _pool[i] = entry;
                    continue;
                }

                entry.Rect.anchoredPosition = entry.StartPos + new Vector2(0f, riseDistance * t);
                entry.Group.alpha = 1f - t;
                _pool[i] = entry;
            }
        }

        private void EnsurePool()
        {
            if (_built || spawnRoot == null)
                return;

            _built = true;
            Font resolved = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = new GameObject("DamageNumber_" + i, typeof(RectTransform), typeof(CanvasGroup), typeof(Text));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(spawnRoot, false);
                rect.sizeDelta = new Vector2(80f, 32f);

                Text text = go.GetComponent<Text>();
                text.font = resolved;
                text.fontSize = fontSize;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;

                CanvasGroup group = go.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                go.SetActive(false);
                _pool.Add(new Entry
                {
                    Rect = rect,
                    Label = text,
                    Group = group
                });
            }
        }
    }
}
