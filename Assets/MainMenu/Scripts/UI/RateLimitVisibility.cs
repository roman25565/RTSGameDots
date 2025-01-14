﻿using UnityEngine;

namespace MainMenu.UI
{
    /// <summary>
    /// Observes the Lobby request rate limits and changes the visibility of a UIPanelBase to suit.
    /// E.g. the refresh button on the Join menu should be inactive after a refresh for long enough to avoid the lobby query rate limit.
    /// </summary>
    public class RateLimitVisibility : MonoBehaviour
    {
        [SerializeField]
        UIPanelBase m_target;
        [SerializeField]
        float m_alphaWhenHidden = 0.5f;
        [SerializeField]
        LobbyManager.RequestType m_requestType;

        void Start()
        {
            MainMenuManager.Instance.LobbyManager.GetRateLimit(m_requestType).onCooldownChange += UpdateVisibility;
        }

        void OnDestroy()
        {
            if (MainMenuManager.Instance == null || MainMenuManager.Instance.LobbyManager == null)
                return;
            MainMenuManager.Instance.LobbyManager.GetRateLimit(m_requestType).onCooldownChange -= UpdateVisibility;
        }

        void UpdateVisibility(bool isCoolingDown)
        {
            if (isCoolingDown)
                m_target.Hide(m_alphaWhenHidden);
            else
                m_target.Show();
        }
    }
}