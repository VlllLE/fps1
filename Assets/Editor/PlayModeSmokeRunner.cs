using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
using System.Text;

public static class PlayModeSmokeRunner
{
    private static StringBuilder report;
    private static bool running;
    
    [MenuItem("Tools/Run Play Mode Smoke Check")]
    public static void RunSmokeCheck()
    {
        if (running) return;
        
        report = new StringBuilder();
        report.AppendLine("=== Play Mode Smoke Check ===");
        
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/TestRange.unity");
        report.AppendLine($"Opened scene: {scene.path}");
        
        running = true;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.isPlaying = true;
    }
    
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.update += TickSmoke;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            running = false;
        }
    }
    
    private static int frames;
    private static bool checkedSpawn;
    private static bool checkedMove;
    private static bool checkedHit;
    private static Vector3 startPos;
    
    private static void TickSmoke()
    {
        frames++;
        
        if (!checkedSpawn && frames > 10)
        {
            checkedSpawn = true;
            var player = GameObject.Find("Player");
            var cc = player != null ? player.GetComponent<CharacterController>() : null;
            
            if (player == null)
            {
                report.AppendLine("FAIL: Player not found");
            }
            else
            {
                report.AppendLine($"OK: Player at {player.transform.position}");
                report.AppendLine($"  grounded={cc != null && cc.isGrounded}");
                report.AppendLine($"  MovementController={(player.GetComponent<MovementController>() != null)}");
                report.AppendLine($"  InputHandler={(player.GetComponent<InputHandler>() != null)}");
                report.AppendLine($"  Weapon={(player.GetComponentInChildren<WeaponController>() != null)}");
                report.AppendLine($"  Camera={(Camera.main != null)}");
                startPos = player.transform.position;
                
                if (cc != null)
                    cc.Move(Vector3.forward * 2f);
            }
        }
        
        if (checkedSpawn && !checkedMove && frames > 20)
        {
            checkedMove = true;
            var player = GameObject.Find("Player");
            if (player != null)
            {
                float dist = Vector3.Distance(
                    new Vector3(startPos.x, 0, startPos.z),
                    new Vector3(player.transform.position.x, 0, player.transform.position.z));
                report.AppendLine(dist > 0.5f
                    ? $"OK: Player moved {dist:F2}m after CharacterController.Move"
                    : $"FAIL: Player did not move (dist={dist:F2})");
            }
        }
        
        if (checkedMove && !checkedHit && frames > 30)
        {
            checkedHit = true;
            var cam = Camera.main;
            var target = GameObject.Find("Target_Near");
            var targetComp = target != null ? target.GetComponent<Target>() : null;
            
            if (cam == null || targetComp == null)
            {
                report.AppendLine("FAIL: Camera or Target_Near missing for hit check");
            }
            else
            {
                float healthBefore = targetComp.CurrentHealth;
                var head = target.transform.Find("Head");
                Vector3 aim = head != null ? head.position : target.transform.position + Vector3.up;
                Vector3 dir = (aim - cam.transform.position).normalized;
                
                if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, 100f))
                {
                    var hitTarget = hit.collider.GetComponentInParent<Target>();
                    if (hitTarget != null)
                    {
                        hitTarget.TakeDamage(new DamageInfo
                        {
                            damage = 25f,
                            hitPoint = hit.point,
                            hitNormal = hit.normal,
                            hitDirection = dir
                        });
                        report.AppendLine($"OK: Hitscan hit {hitTarget.name}, health {healthBefore} -> {hitTarget.CurrentHealth}");
                    }
                    else
                    {
                        report.AppendLine($"FAIL: Raycast hit {hit.collider.name}, not a Target");
                    }
                }
                else
                {
                    report.AppendLine("FAIL: Raycast missed entirely");
                }
            }
            
            EditorApplication.update -= TickSmoke;
            Debug.Log(report.ToString());
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "../PlayModeSmokeReport.txt"),
                report.ToString());
            
            EditorApplication.isPlaying = false;
        }
    }
}
