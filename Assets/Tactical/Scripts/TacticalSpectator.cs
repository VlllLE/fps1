using System.Collections.Generic;
using UnityEngine;

namespace FpsTactical
{
    /// <summary>
    /// Third-person camera that follows a living bot once the local player is dead (teammates first, then enemies).
    /// Left click / Space / right arrow picks the next player, right click / left arrow the previous one; when the watched
    /// player dies the camera moves on by itself. <see cref="Tick"/> is driven by <see cref="TacticalMatch.Step"/>.
    /// </summary>
    public sealed class TacticalSpectator : MonoBehaviour
    {
        public const float StartDelaySeconds = 2.2f;

        private const float Distance = 3.4f;
        private const float Height = 1.9f;

        private readonly List<BotController> candidates = new List<BotController>();

        private TacticalMatch match;
        private Camera cam;
        private BotController target;
        private float deadFor;
        private Vector3 velocity;
        private bool snap;

        public bool IsSpectating => cam != null && cam.enabled;
        public Camera ViewCamera => cam;
        public BotController Target => target;
        public string TargetName
        {
            get
            {
                TacticalParticipant participant = match != null ? match.Stats.FindBot(target) : null;
                return participant != null ? participant.Name : string.Empty;
            }
        }

        public float TargetHealth01 => target != null ? target.Health01 : 0f;

        public void Bind(TacticalMatch owner)
        {
            match = owner;
        }

        private void OnDestroy()
        {
            if (cam != null)
                Destroy(cam.gameObject);
        }

        public void Tick(float dt)
        {
            PlayerHealth player = match != null ? match.Player : null;
            if (player == null || match.Model == null || match.Model.Phase == TacticalPhase.ClassSelect || !player.IsDead)
            {
                deadFor = 0f;
                Stop();
                return;
            }

            deadFor += dt;
            if (deadFor < StartDelaySeconds)
                return;

            Collect();
            if (target == null || target.IsDead || !candidates.Contains(target))
                Pick(0);

            if (target == null)
            {
                Stop();
                return;
            }

            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.RightArrow))
                Pick(1);
            else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.LeftArrow))
                Pick(-1);

            EnsureCamera();
            if (!cam.enabled)
            {
                cam.enabled = true;
                snap = true;
            }

            Follow(dt);
        }

        /// <summary>Moves the watched player on by <paramref name="step"/> places (0 keeps it when still valid). Public for tests.</summary>
        public void Pick(int step)
        {
            Collect();
            if (candidates.Count == 0)
            {
                target = null;
                return;
            }

            int index = candidates.IndexOf(target);
            if (index < 0)
                index = 0;
            else if (step != 0)
                index = ((index + step) % candidates.Count + candidates.Count) % candidates.Count;
            else if (target != null && !target.IsDead)
                return;

            if (target != candidates[index])
                snap = true;
            target = candidates[index];
        }

        private void Collect()
        {
            candidates.Clear();
            CombatTeam mine = match.PlayerSide;
            match.CollectAliveBots(mine, candidates);
            if (candidates.Count == 0)
                match.CollectAliveBots(CombatTeams.Opposite(mine), candidates);
        }

        private void Stop()
        {
            target = null;
            if (cam != null && cam.enabled)
                cam.enabled = false;
        }

        private void EnsureCamera()
        {
            if (cam != null)
                return;

            var go = new GameObject("TacticalSpectatorCamera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            cam.depth = (Camera.main != null ? Camera.main.depth : 0f) + 5f;
            cam.enabled = false;
        }

        private void Follow(float dt)
        {
            Vector3 head = target.transform.position + Vector3.up * 1.5f;
            Vector3 desired = head + Vector3.up * (Height - 1.5f) - target.transform.forward * Distance;
            if (Physics.Linecast(head, desired, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target.transform))
                desired = hit.point + (head - desired).normalized * 0.25f;

            Transform camTransform = cam.transform;
            if (snap)
            {
                camTransform.position = desired;
                velocity = Vector3.zero;
                snap = false;
            }
            else
            {
                camTransform.position = Vector3.SmoothDamp(camTransform.position, desired, ref velocity, 0.12f, float.MaxValue, Mathf.Max(0.001f, dt));
            }

            Vector3 look = head + target.transform.forward * 4f;
            Quaternion wanted = Quaternion.LookRotation((look - camTransform.position).normalized, Vector3.up);
            camTransform.rotation = Quaternion.Slerp(camTransform.rotation, wanted, 1f - Mathf.Exp(-12f * Mathf.Max(0.001f, dt)));
        }
    }
}
