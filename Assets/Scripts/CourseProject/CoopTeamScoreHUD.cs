using Unity.BossRoom.Gameplay.GameplayObjects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.BossRoom.CourseProject
{
    public class CoopTeamScoreHUD : MonoBehaviour
    {
        static CoopTeamScoreHUD s_Instance;

        Canvas m_Canvas;
        Text m_Text;

        int m_LastScore = int.MinValue;
        string m_LastSceneName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (s_Instance != null) return;
            var go = new GameObject("CoopTeamScoreHUD");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<CoopTeamScoreHUD>();
        }

        void EnsureUI()
        {
            if (m_Canvas != null) return;

            m_Canvas = new GameObject("HUDCanvas").AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            DontDestroyOnLoad(m_Canvas.gameObject);

            var scaler = m_Canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            m_Canvas.gameObject.AddComponent<GraphicRaycaster>();

            var textGO = new GameObject("TeamScoreText");
            textGO.transform.SetParent(m_Canvas.transform, false);
            m_Text = textGO.AddComponent<Text>();
            m_Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_Text.fontSize = 20;
            m_Text.alignment = TextAnchor.UpperLeft;
            m_Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            m_Text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = m_Text.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -12);
            rt.sizeDelta = new Vector2(600, 120);
        }

        void Update()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (m_LastSceneName != sceneName)
            {
                m_LastSceneName = sceneName;
                m_LastScore = int.MinValue;
            }

            EnsureUI();

            var players = FindObjectsByType<PersistentPlayer>(FindObjectsSortMode.None);
            if (players == null || players.Length == 0)
            {
                m_Text.text = "Team Score: --";
                return;
            }

            var score = players[0].TeamScore.Value;
            if (score != m_LastScore)
            {
                m_LastScore = score;
                m_Text.text = $"Team Score: {score}\n(Score increases when enemies die)";
            }
        }
    }
}
