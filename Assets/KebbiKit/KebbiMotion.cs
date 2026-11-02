using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// KebbiKit motion library: arm gestures as keyframes on Kebbi's eight arm motors. The head is never moved here (in apps
// where it follows the face it belongs to that). Angles are degrees from the arms' rest pose (all zero, arms down).
//   KebbiMotion.Install(transform);   once, at start-up
//   KebbiMotion.Play("wave");          Allowed = false stops any gesture and keeps the arms still
// Joints, as measured by moving Kebbi's arms by hand (both arms alike, not mirrored): ShoulderX lifts the arm out to
// the side (0 down, +87 level); with the arm lifted, ShoulderZ swings it forward (to about -95, pointing ahead) or
// back (to about +10); ShoulderY turns it the other way (to about -45); the elbow bends with negative angles (to
// about -90). Kebbi's arm goes no higher than level. Angles are the motors' own; speeds stay below
// 200 deg/s, each move eases in and out, and every gesture ends back at rest.
public class KebbiMotion : MonoBehaviour
{
    public enum Joint { RShoulderZ, RShoulderY, RShoulderX, RElbow, LShoulderZ, LShoulderY, LShoulderX, LElbow }

    private static readonly Nuwa.NuwaMotorType[] Motors =
    {
        Nuwa.NuwaMotorType.right_shoulder_z, Nuwa.NuwaMotorType.right_shoulder_y, Nuwa.NuwaMotorType.right_shoulder_x, Nuwa.NuwaMotorType.right_bow_y,
        Nuwa.NuwaMotorType.left_shoulder_z, Nuwa.NuwaMotorType.left_shoulder_y, Nuwa.NuwaMotorType.left_shoulder_x, Nuwa.NuwaMotorType.left_bow_y,
    };

    // each joint's range (right Z, Y, X, elbow, then left), a little inside the measured stops
    private static readonly float[] Min = { -95f, -45f, -15f, -90f, -95f, -45f, -15f, -90f };
    private static readonly float[] Max = { 10f, 5f, 87f, 5f, 10f, 5f, 87f, 5f };
    private const float MaxSpeed = 200f, MinSpeed = 12f;
    // every gesture is played this much faster than its times are written
    private const float Tempo = 1.25f;

    // A pose: the eight motor angles (right Z, Y, X, elbow, left Z, Y, X, elbow) reached at `At` seconds.
    public struct Pose
    {
        public float At;
        public float[] Deg;
        public Pose(float at, params float[] deg) { At = at; Deg = deg; }
    }

    private static float[] R(float z, float y, float x, float elbow) => new[] { z, y, x, elbow, 0f, 0f, 0f, 0f };
    private static float[] L(float z, float y, float x, float elbow) => new[] { 0f, 0f, 0f, 0f, z, y, x, elbow };
    private static float[] Both(float z, float y, float x, float elbow) => new[] { z, y, x, elbow, z, y, x, elbow };
    private static readonly float[] Rest = new float[8];

    // The gestures (x lifts, z swings the lifted arm forward, the elbow bends). Each starts from rest and ends at rest.
    public static readonly Dictionary<string, Pose[]> Gestures = new Dictionary<string, Pose[]>
    {
        // hello: the right arm up, a little forward, the forearm waved twice (wide), down
        { "wave", new[] { new Pose(1.1f, R(-35, 0, 85, -25)), new Pose(1.6f, R(-35, 0, 85, -75)), new Pose(2.1f, R(-35, 0, 85, -25)),
            new Pose(2.6f, R(-35, 0, 85, -75)), new Pose(3.1f, R(-35, 0, 85, -25)), new Pose(4.3f, Rest) } },
        // welcome: both arms open wide, a little forward, held, back
        { "welcome", new[] { new Pose(1.2f, Both(-50, 0, 70, -25)), new Pose(2.4f, Both(-50, 0, 70, -25)), new Pose(3.6f, Rest) } },
        // point to the robot's right / left (the person's left / right): the arm out to that side, level, straight
        { "point_right", new[] { new Pose(1.1f, R(-10, 0, 87, 0)), new Pose(2.4f, R(-10, 0, 87, 0)), new Pose(3.5f, Rest) } },
        { "point_left", new[] { new Pose(1.1f, L(-10, 0, 87, 0)), new Pose(2.4f, L(-10, 0, 87, 0)), new Pose(3.5f, Rest) } },
        // ok / got it: the right arm forward, one beat of the forearm
        { "ok", new[] { new Pose(0.7f, R(-70, 0, 40, -60)), new Pose(1.15f, R(-70, 0, 40, -15)), new Pose(1.9f, Rest) } },
        // thinking: the right arm forward, the hand bent in toward the chest
        { "think", new[] { new Pose(1.1f, R(-85, 0, 55, -90)), new Pose(2.5f, R(-85, 0, 55, -90)), new Pose(3.5f, Rest) } },
        // covering an eye (a rough demonstration): the right arm level ahead, the forearm bent in toward the face, held
        { "cover_eye", new[] { new Pose(1.4f, R(-95, 0, 80, -90)), new Pose(3.5f, R(-95, 0, 80, -90)), new Pose(4.7f, Rest) } },
        // comfort: both arms reach forward, slowly
        { "comfort", new[] { new Pose(1.4f, Both(-90, 0, 60, -30)), new Pose(2.7f, Both(-90, 0, 60, -30)), new Pose(4.0f, Rest) } },
        // cheer: both arms up, the forearms pumped twice
        { "cheer", new[] { new Pose(1.0f, Both(-25, 0, 87, -10)), new Pose(1.5f, Both(-25, 0, 60, -80)), new Pose(2.0f, Both(-25, 0, 87, -10)),
            new Pose(2.5f, Both(-25, 0, 60, -80)), new Pose(3.0f, Both(-25, 0, 87, -10)), new Pose(4.1f, Rest) } },
        // clap: both arms level ahead, swung together and apart twice
        { "clap", new[] { new Pose(1.0f, Both(-45, 0, 80, -10)), new Pose(1.4f, Both(-95, 0, 80, -10)), new Pose(1.8f, Both(-45, 0, 80, -10)),
            new Pose(2.2f, Both(-95, 0, 80, -10)), new Pose(3.3f, Rest) } },
        // oops: a small shrug
        { "oops", new[] { new Pose(0.6f, Both(-25, 0, 40, -50)), new Pose(1.2f, Both(-25, 0, 40, -50)), new Pose(2.0f, Rest) } },
        // goodbye: a wave, then the arm comes down slowly
        { "bye", new[] { new Pose(1.1f, R(-35, 0, 85, -25)), new Pose(1.6f, R(-35, 0, 85, -75)), new Pose(2.1f, R(-35, 0, 85, -25)),
            new Pose(2.6f, R(-35, 0, 85, -75)), new Pose(4.2f, Rest) } },
    };

    private static KebbiMotion s_instance;
    private static bool s_allowed = true;
    private static bool s_reading;   // the read-out is on: no gestures
    private Coroutine _running;
    private readonly float[] _now = new float[8];

    // While false (a test, a calibration...), no gesture plays and the arms go back to rest.
    public static bool Allowed
    {
        get => s_allowed;
        set
        {
            if (s_allowed == value)
                return;
            s_allowed = value;
            if (!value)
                Stop();
        }
    }

    public static void Install(Transform parent)
    {
        if (s_instance != null)
            return;
        var go = new GameObject("KebbiMotion");
        go.transform.SetParent(parent, false);
        s_instance = go.AddComponent<KebbiMotion>();
    }

    public static void Play(string name)
    {
        if (s_instance == null || !s_allowed || s_reading || !Gestures.TryGetValue(name, out var poses))
            return;
        if (s_instance._running != null)
            s_instance.StopCoroutine(s_instance._running);
        Debug.Log("[KebbiMotion] " + name);
        s_instance._running = s_instance.StartCoroutine(s_instance.Run(poses));
    }

    private static readonly string[] JointNames =
        { "右手・肩膀 Z", "右手・肩膀 Y", "右手・肩膀 X", "右手・手肘", "左手・肩膀 Z", "左手・肩膀 Y", "左手・肩膀 X", "左手・手肘" };

    // While the joint check runs: the step on screen (null otherwise), for the app to show large.
    public static string CheckStep { get; private set; }

    // A check of the arm motors' directions: each motor in turn, named aloud, to + and back, then
    // to - and back - raw motor angles, nothing mirrored (40 deg; ShoulderZ -90 / +40). Where the motor really got to
    // is read back and shown with the command.
    public static void TestJoints()
    {
        if (s_instance == null)
            return;
        if (s_instance._running != null)
            s_instance.StopCoroutine(s_instance._running);
        s_instance._running = s_instance.StartCoroutine(s_instance.JointCheck());
    }

    private IEnumerator JointCheck()
    {
        MoveTo(Rest, 0.8f);
        yield return new WaitForSecondsRealtime(1f);
        int step = 0;
        for (int j = 0; j < Motors.Length; j++)
            foreach (float sign in new[] { 1f, -1f })
            {
                step++;
                bool z = j % 4 == 0;
                float deg = sign > 0f ? 40f : z ? -90f : -40f;
                string head = $"第 {step} / {Motors.Length * 2} 步\n{JointNames[j]}\n";
                string way = (sign > 0f ? "+" : "−") + Mathf.Abs(deg) + (sign > 0f ? "°（正方向）" : "°（負方向）");
                CheckStep = head + way;
                Nuwa.startTTS(JointNames[j] + (sign > 0f ? "，正方向" : "，負方向"));
                yield return new WaitForSecondsRealtime(2.2f);
                float from = Nuwa.getMotorPresentPossitionInDegree(Motors[j]);
                Nuwa.setMotorPositionInDegree(Motors[j], deg, 30f);
                // about 3 s held there to look at
                yield return new WaitForSecondsRealtime(4.5f);
                float got = Nuwa.getMotorPresentPossitionInDegree(Motors[j]);
                CheckStep = head + way + $"\n<size=60%>實際 {got:0}°（起點 {from:0}°）</size>";
                Debug.Log($"[KebbiMotion] joint check {Motors[j]} command {deg} from {from:0.0} reached {got:0.0}");
                yield return new WaitForSecondsRealtime(1.5f);
                Nuwa.setMotorPositionInDegree(Motors[j], 0f, 30f);
                yield return new WaitForSecondsRealtime(2f);
            }
        CheckStep = null;
        _running = null;
    }

    // A read-out of the arm motors (for measuring poses by moving the arms by hand): no motor is driven; the eight angles
    // are shown in CheckStep and logged twice a second until called again.
    public static void ToggleReadout()
    {
        if (s_instance == null)
            return;
        if (s_instance._running != null)
        {
            s_instance.StopCoroutine(s_instance._running);
            s_instance._running = null;
            CheckStep = null;
            s_reading = false;
            return;
        }
        s_reading = true;
        s_instance._running = s_instance.StartCoroutine(s_instance.Readout());
    }

    private IEnumerator Readout()
    {
        string[] names = { "右Z", "右Y", "右X", "右肘", "左Z", "左Y", "左X", "左肘" };
        var deg = new float[Motors.Length];
        for (int n = 0; ; n++)
        {
            for (int j = 0; j < Motors.Length; j++)
                deg[j] = Nuwa.getMotorPresentPossitionInDegree(Motors[j]);
            var line = new System.Text.StringBuilder();
            for (int j = 0; j < Motors.Length; j++)
                line.Append(j == 4 ? "\n" : j > 0 ? "   " : "").Append(names[j]).Append(' ').Append(deg[j].ToString("0"));
            CheckStep = "讀角度中（再連點三下結束）\n" + line;
            if (n % 2 == 0)
                Debug.Log("[KebbiMotion] readout " + string.Join(" ", System.Array.ConvertAll(deg, d => d.ToString("0.0"))));
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    // Stop a gesture and bring the arms back to rest.
    public static void Stop()
    {
        if (s_instance == null)
            return;
        if (s_instance._running != null)
            s_instance.StopCoroutine(s_instance._running);
        s_instance._running = null;
        CheckStep = null;
        s_instance.MoveTo(Rest, 0.8f);
    }

    // Pose to pose with ease-in-out: along a smoothstep curve the motors are sent a waypoint a little ahead of the
    // curve's present point, at the speed that keeps them on it (the motors only take a target and a constant speed).
    private IEnumerator Run(Pose[] poses)
    {
        const float Step = 0.08f, Ahead = 0.16f;
        var from = new float[Motors.Length];
        var to = new float[Motors.Length];
        float t = 0f;
        foreach (var pose in poses)
        {
            float span = Mathf.Max(0.2f, (pose.At - t) / Tempo);
            for (int j = 0; j < Motors.Length; j++)
            {
                from[j] = _now[j];
                to[j] = Mathf.Clamp(pose.Deg[j], Min[j], Max[j]);
            }
            for (float e = 0f; e < span; e += Step)
            {
                float k0 = Ease(e / span), k1 = Ease((e + Ahead) / span);
                for (int j = 0; j < Motors.Length; j++)
                {
                    float at = Mathf.Lerp(from[j], to[j], k1);
                    if (Mathf.Abs(at - _now[j]) < 0.5f && at != to[j])
                        continue;
                    if (at == _now[j])
                        continue;
                    float speed = Mathf.Clamp(Mathf.Abs(at - Mathf.Lerp(from[j], to[j], k0)) / Ahead, MinSpeed, MaxSpeed);
                    Nuwa.setMotorPositionInDegree(Motors[j], at, speed);
                    _now[j] = at;
                }
                yield return new WaitForSecondsRealtime(Step);
            }
            var got = new string[Motors.Length];
            for (int j = 0; j < Motors.Length; j++)
                got[j] = Nuwa.getMotorPresentPossitionInDegree(Motors[j]).ToString("0") + "/" + _now[j].ToString("0");
            Debug.Log("[KebbiMotion] at " + pose.At + " s, reached/sent " + string.Join(" ", got));
            t = pose.At;
        }
        _running = null;
    }

    private static float Ease(float k)
    {
        k = Mathf.Clamp01(k);
        return k * k * (3f - 2f * k);
    }

    // Send each arm joint toward its angle at the speed that gets it there in `seconds`.
    private void MoveTo(float[] deg, float seconds)
    {
        for (int j = 0; j < Motors.Length; j++)
        {
            float to = Mathf.Clamp(deg[j], Min[j], Max[j]);
            float speed = Mathf.Clamp(Mathf.Abs(to - _now[j]) / Mathf.Max(0.1f, seconds), MinSpeed, MaxSpeed);
            Nuwa.setMotorPositionInDegree(Motors[j], to, speed);
            _now[j] = to;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Stop();
    }

    private void OnApplicationQuit() => Stop();
}
