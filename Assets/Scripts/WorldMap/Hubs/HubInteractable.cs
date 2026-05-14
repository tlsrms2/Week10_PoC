using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Linq;

namespace Overlap.WorldMap
{
    /// <summary>
    /// 거점 오브젝트에 부착하여 플레이어의 클릭 상호작용을 처리합니다.
    /// 마우스로 거점을 클릭했을 때 상태에 따라 적절한 UI 이벤트나 로직을 호출합니다.
    /// </summary>
    [RequireComponent(typeof(HubNode))]
    [RequireComponent(typeof(Collider2D))] // 클릭 감지를 위해 콜라이더 필수
    public class HubInteractable : MonoBehaviour
    {
        // ── 전역 상호작용 이벤트 (UI 매니저가 구독) ──────────────────────────
        
        /// <summary>거점 관리가 필요할 때 (인자: 대상 거점, 공장 컴포넌트)</summary>
        public static event Action<HubNode, Factory.HubFactory> OnHubManagementRequested;
        
        /// <summary>블록 생산이 완료되어 3선택지 수확이 필요할 때</summary>
        public static event Action<HubNode, Factory.HubFactory> OnHarvestRequested;

        /// <summary>빈 거점을 점령하고자 할 때</summary>
        public static event Action<HubNode> OnOccupationRequested;

        /// <summary>침략 대상 거점을 방어하고자 할 때</summary>
        public static event Action<HubNode> OnInvasionRequested;

        private HubNode hubNode;
        private Factory.HubFactory hubFactory;
        private TurnManager turnManager;

        private void Awake()
        {
            hubNode = GetComponent<HubNode>();
            hubFactory = GetComponent<Factory.HubFactory>();
        }

        private void Start()
        {
            turnManager = FindAnyObjectByType<TurnManager>();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                // UI 위를 클릭한 경우 무시
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                Vector2 mousePos = mouse.position.ReadValue();
                Vector2 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

                var col = GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(worldPos))
                {
                    // 디펜스 페이즈 중에는 클릭 무시
                    if (turnManager != null && turnManager.Phase != TurnPhase.Planning)
                    {
                        Debug.Log("[HubInteractable] Cannot interact during Defense Phase.");
                        return;
                    }

                    HandleInteraction();
                }
            }
        }

        private void HandleInteraction()
        {
            // 침략 상태 우선 확인
            if (turnManager != null && turnManager.ActiveInvasions.Contains(hubNode))
            {
                Debug.Log($"[HubInteractable] Requesting invasion defense for {hubNode.HubName}");
                OnInvasionRequested?.Invoke(hubNode);
                return;
            }

            if (hubNode.IsEmpty)
            {
                // 연결성 검사: 인접한 거점 중 내 소유(Main 또는 Occupied)가 하나라도 있어야 함
                bool isConnectedToOwned = false;
                var allHubs = FindObjectsByType<HubNode>(FindObjectsSortMode.None);
                foreach (var owned in allHubs)
                {
                    if (owned != null && (owned.IsMain || owned.State == HubState.Occupied))
                    {
                        // 쌍방향 확인: owned의 ConnectedHubs에 내가 있거나, 내 ConnectedHubs에 owned가 있거나
                        if (owned.ConnectedHubs.Contains(hubNode) || hubNode.ConnectedHubs.Contains(owned))
                        {
                            isConnectedToOwned = true;
                            break;
                        }
                    }
                }

                if (!isConnectedToOwned)
                {
                    Debug.Log($"[HubInteractable] Cannot occupy {hubNode.HubName}. It is not connected to any owned hub.");
                    return; // 연결되지 않았으므로 클릭 무시
                }

                // 빈 거점: 점령 시도
                Debug.Log($"[HubInteractable] Requesting occupation of {hubNode.HubName}");
                OnOccupationRequested?.Invoke(hubNode);
                // (선택) 여기서 바로 TurnManager.TryOccupyHub()를 호출할 수도 있지만, 
                // 보통 '정말 점령하시겠습니까?' UI를 띄우는 것이 좋습니다.
                // turnManager?.TryOccupyHub(hubNode);
            }
            else if (hubNode.IsMain || hubNode.State == HubState.Occupied)
            {
                // 내 거점: 수확 또는 관리
                if (hubFactory != null)
                {
                    if (hubFactory.IsReadyToHarvest)
                    {
                        Debug.Log($"[HubInteractable] Requesting harvest at {hubNode.HubName}");
                        OnHarvestRequested?.Invoke(hubNode, hubFactory);
                    }
                    else
                    {
                        Debug.Log($"[HubInteractable] Requesting management for {hubNode.HubName}");
                        OnHubManagementRequested?.Invoke(hubNode, hubFactory);
                    }
                }
                else
                {
                    Debug.LogWarning($"[HubInteractable] {hubNode.HubName} is owned but has no HubFactory!");
                }
            }
        }
    }
}
