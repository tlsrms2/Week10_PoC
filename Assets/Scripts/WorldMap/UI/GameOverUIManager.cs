using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlap.WorldMap.UI
{
    /// <summary>
    /// 게임 오버 상태를 감지하여 패널을 띄우고, 재시작 및 종료 기능을 제공합니다.
    /// </summary>
    public class GameOverUIManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("게임 오버 시 활성화할 패널 오브젝트")]
        [SerializeField] private GameObject gameOverPanel;
        
        [Tooltip("다시하기 버튼")]
        [SerializeField] private Button restartButton;
        
        [Tooltip("나가기 버튼")]
        [SerializeField] private Button quitButton;

        private TurnManager turnManager;

        private void Awake()
        {
            // 시작 시에는 숨김
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        private void Start()
        {
            // TurnManager의 GameOver 이벤트 구독
            turnManager = FindAnyObjectByType<TurnManager>();
            if (turnManager != null)
            {
                turnManager.GameOver += ShowGameOverUI;
            }
            else
            {
                Debug.LogWarning("[GameOverUIManager] TurnManager not found in scene.");
            }

            // 버튼 리스너 등록
            if (restartButton != null)
                restartButton.onClick.AddListener(RestartGame);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.GameOver -= ShowGameOverUI;
            }
        }

        private void ShowGameOverUI()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                // 게임 오버 시 다른 UI가 클릭되는 것을 방지하기 위해 
                // 필요하다면 타임스케일을 0으로 만들거나 입력을 차단할 수 있습니다.
                // Time.timeScale = 0f; 
            }
        }

        /// <summary>
        /// 현재 씬을 처음부터 다시 로드합니다.
        /// </summary>
        public void RestartGame()
        {
            Time.timeScale = 1f; // 재시작 시 타임스케일 초기화
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// 게임을 종료합니다.
        /// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
